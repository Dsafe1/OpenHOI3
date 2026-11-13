# Savegames: the original's `.hoi3`, both ways

A game here is saved in the original's own format and the original's saves load here - the maintainer's choice of
2026-11-04, over a format of our own and over a replay. Read out of the original the same day
(`reversing/findings/FINDINGS-savegame.md`; the value writers `reversing/findings/FINDINGS-save.md`), and set against
the maintainer's 45 saves. `confirmed` unless marked.

**[exe]** marks a rule the original's executable holds, **[mod: ...]** what a file may change.

## 1. The stages

1. **The reading** - done: `FINDINGS-savegame.md`, and for the battles `FINDINGS-savedbattle.md` (2026-11-05).
2. **The loader** - in increments, each set against the maintainer's saves (sections 4 and 5). **The first is built**:
   the clock, the generator, the played country, every province and country's figures below, the factions. **So is the
   second**: the units, the leaders in service, the queue, what waits to be placed and the templates. **And the third**:
   the wars, the pacts, the trade routes, the convoys and mobilisation (section 6), and the battles, the finished
   battles and the sunk ships (section 7).
3. **The writer** - a save loaded and written straight back set beside the file, block by block. **Its first increment
   is built** (section 8): the layout, the top level's head, every province, each country's figures the loader's first
   increment reads, and the factions. **So is its second** (section 9): each country's military part - what the loader's
   second increment reads. **And its third** (section 10): what the loader's third reads - the pacts, the wars, the
   convoys and the pools, the battles, the finished battles and the sunk ships.
4. **The save dialog** (`ingamemenu.md`, section 6) and the autosave at the settings' interval - **both built**
   (section 11).
5. **The fourth increment**, loader and writer together, what neither held: **its first piece is built** (section 12),
   each country's politics and research queue, **its second** (section 13), its spies, **and its third** (section 14),
   the top level's counters, flags, victory conditions, fired events, fronts and faction progress, **and its fourth**
   (section 15), exile, the goods pools, battles fought, strategic warfare, alignment and lend-lease, **and its fifth**
   (section 16), the AI's strategy and theatres; the rest of section 4's list - the statistics, the gameplay settings,
   the figures section 16 lists as not held - is still to come.

## 2. What a load is

As the original's [exe], but where *Ours* says otherwise:

1. **The world** is built as for a new game - the map, every database, the defines, the leaders and ministers of the
   files, the events - and its history folded in (`CGameStateLoader.BuildWorld`); the original's objects are its
   startup's too, filled again in place.
2. **The save is read over it**, one key at a time. Every figure the save gives is set; **one it leaves out is set to
   what leaving it out means** - nought, none, the define's pressure - and not left as the history had it. **What is not
   loaded yet is cleared and reported**, never answered from the history (`CLAUDE.md`'s rule on what is not built);
   a new game's battles, finished battles and sunk ships are cleared before the save's are read.
3. **The played country** is the one asked for (`--country=`), else the one the save names. The original never takes
   it from the save - its lobby chooses, by default the save's own (*inferred*) - so the two come to the same.
4. **The generator** is seeded with the save's `seed`, where the original throws `seed` and `count` away
   (`docs/compatibility.md`, entry 15). The generator is then moved on by the save's `count`, so a game saved here goes on with the draws it would have made.
   **The AI's own generator is not saved**, as in the original: a load seeds it from the save's `seed` at its first draw,
   so the AI's rolls after a load are not the saved game's, and a save loaded twice still plays the same (`enhanced.md`).
5. **What the original works out again**, before the first hour: the provinces' and countries' modifiers, the strategic
   resources, IC, manpower, leadership, every country's effective neutrality, every country brought up to date with its
   wars - each pair at war stamping `last_war` the hour loaded, so a peace in the next hundred days leaves a truce
   (`war.md`, section 4; built 2026-11-12, found by `OpenHOI3.SaveCheck reload`) - and **a fresh AI for every country** - the
   ministers' first hours; the strategy a save gave is kept until its ten days are up, and one never made is made
   (section 16). With the
   units (section 5): each unit's leader put on, each brigade's figures made from its type and its levels, the queue's
   cost, the officer ratio, and what an intercept watches.
6. **None of a new game's two days**, the filling, the month or the first hour: a loaded game's first hour is the hour
   after its `date` [exe]. Nor are the laws moved, technology bought, the order-of-battle files read, history replayed or
   history revolts carried out.

**The supply network is made again once a save is read**, and each country's victory points counted (2026-11-09). Reading the
theatres builds it while the pacts are yet to come, and nothing would make it again until a province changed hands, so a puppet
stood apart from its master for as long as no province did. Found setting a loaded game beside the running original
(`reversing/findings/FINDINGS-balancing.md`).

**A loaded game is past its AI's first pass.** A game the original has loaded and stands paused at its hour already has every
agent made and bound and its first pass marked done (read in the running game, 2026-11-09), and it balances from its first hour.
So a load clears each strategy's "theatres to remake" and marks the pass done and waiting over. **Entering a loaded game runs two AI passes
at the save's hour, before its first hour** [exe, counted in the running game, 2026-11-10, `reversing/findings/FINDINGS-liveread.md`, section 12]: every agent
is there when the first begins, each agent ticks in both and runs its land block once - **in the second**: the agents tick in the first with no
unit, that pass binds them, and they tick bound with their full pass pending in the second, whatever the hour (read live 2026-11-11, `reversing/findings/FINDINGS-liveread.md`,
section 39). Here the agents are made unbound first (`CUnitAiPass.MakeAgentsOfLoad`), a pass runs and its orders are carried out, and a second pass the same (`CSimulation`). So a save the original writes straight after entering already holds those orders.
Where the original makes the agents was not read. **Each theatre is refreshed as it enters** - the provinces its country holds that no theatre of its has placed, those it lost dropped, and its fronts made again: a save written straight after has the fronts in another order, and the positions of one score follow that order, and China's Beiping theatre of July 1941 grows from 124 provinces to 134, the one its headquarters stands in among them - its agents' and headquarters' upkeep left out (`CTheatreRefresh.FrontsOfLoad`; `reversing/findings/FINDINGS-liveread.md`, sections 18 and 44; which routine does it was not read). So a theatre headquarters is picked its own province by the theatre's chooser, and does not march.

## 3. How a save writes its values

[exe], from the original's writers (`FINDINGS-save.md`; `FINDINGS-savegame.md`, sections B and C): a figure is thousandths
written with three places (`55.000`); a share, a research progress and a leader's experience have fifteen binary places,
written with five (`0.32000`); a date has its hour (`1936.1.7.22`); an id is `{ id = N type = T }`, the serial first; a
flag is `yes` or `no`; a tag or a name is quoted. A building is a pair, **the level in working order first and the level
built second** - in a 1938 save 12,572 pairs are equal and the 32 that differ have the first lower. A relation is
written as a whole number, its thousandths cut. The top level is `date`, `player` and the rest in a fixed order, the
provinces by id and then the countries in `common/countries.txt`'s order between `fired_events` and `sunk_ships`
[mod: common/countries.txt].

## 4. What the first increment reads

| block | keys read |
| --- | --- |
| top | `date` (with the hour), `player`, `seed`, `faction` (each `country` a member) |
| province | `owner`, `controller`, every `core`, `manpower`, `leadership`, `revolt_risk`, `points`, `nationalism`, `strategic_resource`, `pool`, `current_producing`, `max_producing`, every building's pair, `weather` (each figure, the pressure its define where none), `flags`, every `modifier` an event gave (its name and the hour it lasts to) |
| country | `capital`, `acting_capital` (the capital where nought), every law group, every post (a minister of the country's by id), `ideology` (`noIdeology` none), `technology` (level and progress), every category's ability, `distribution` and `leadership` (the shares), `manpower`, `officers`, `diplo_influence`, `national_unity`, `dissent`, `neutrality`, `effective_neutrality`, `highest_threat` (the country it fears most; `---` none), `auto_assign_leaders`, `flags`, `variables`, every `modifier` an event gave, and every other country's block - `value` the relation, `threat`, `occupation_policy` (none written, `no_occupation`) |

A country's stockpile is its capital's `pool`, so it comes with the provinces.

**Checked**: `OpenHOI3.SaveCheck load <install> <save> [hours]` loads a save, lists every key not loaded yet by its block,
sets the owners, the laws and the technology levels beside the save's and runs the hours given. On 2026-11-04,
`Germany1936_01_07_22.hoi3`: loaded in 1.7 seconds, **owners 14,168 of 14,168, laws 707 of 707, technology levels
28,987 of 28,987**, and 24 hours run on. The list of what is left is the next increments' work:

- ~~**the units**~~ - the second increment, section 5;
- ~~**the wars** and **the pacts**~~ - the third increment, section 6; ~~**the battles** (`combat`) and `sunk_ships`~~ -
  section 7;
- ~~the four id counters and the `id` blocks, the weather's fronts, the victory conditions and the faction's
  `progress`, the game's flags~~ - section 14 - and its gameplay settings;
- ~~a country's own pools (`home`, `usage`, `to`, `back`, the traded ones)~~ - section 15 - ~~its politics (`organization`,
  `popularity`, `cabinet`, `election`, `last_election`, `duration`, `ministers`), its research queue (`research`)~~ -
  section 12 - ~~its spies and their missions~~ - section 13; its active missions, ~~alignment~~ - section 15 - war exhaustion, ~~AI (`ai`, `ai_hard_strategy`, `ai_event_strategy`)~~ - section 16 - statistics and the rest the
  report lists; a province's `out_of_supply_days` and `capital`, which the day makes again, and its `history`.

## 5. What the second increment reads: the units

From `FINDINGS-savegame.md`, section C, [exe] but where marked.

**Ids.** A unit's, a brigade's and a line's id is a pair (section 3). What the history made is type 41, and its serials
are the numbers a new game here gives them - one count over units, brigades and lines - so they are kept as they are;
the save's top-level `unit` is the next of them. What the player minted in play is type 4713, numbered here from that
counter on in the order it was minted - serial 1 is the counter itself, so the history's numbers and the player's run on
with no gap - and a unit's place among its country's units, which reinforcement and the shares go by, is the original's
(*Ours*: the numbers themselves are this game's). Any other type - another peer's - goes after both. A leader's id is
his own (type 37), the number his file gives him. The counter is kept (`CGameState.HistoryUnitNext`), which is how the
writer gives each number back as its pair (section 9).

**The tree.** A country's top-level `theatre`, `armygroup`, `army`, `corps`, `division`, `navy` and `air` blocks, each
with everything under it:

| block | keys read |
| --- | --- |
| unit | `id`, `name`, `base`, `leader`, `previous`, `movement_progress`, `path`, `location`, `retreat`, `dig_in`, `attack_delay`, `combat_hours`, `is_prioritized`, `can_reinforce`, `can_upgrade`, `fuel` and `supplies` (what it was given), `start_date` and `end_date` (the order's two hours, `landorders.md` section 5), `disengage`, `possible_retreat`, `track` (shadowed by a patrol), `was_paradropped`, `is_reserve`, `plan`, `target` and `staging_province` (the AI's sealift and an airborne army's staging), `at_sea` (the hours before a fleet starts another battle), the order, every brigade, every unit under it |
| brigade (`regiment`, `ship`, `wing`) | `id`, `name`, `type`, `organisation`, `strength`, `highest`, `experience`, `is_reserve`, `pride`, `current_distance`, every `<technology>={level progress}`, and a carrier's `air` groups |
| order | `province`, `target`, `stance`, `start_date`, `death_date` (the end hour; `1.1.1` none), `return_to_base`, `time`, `priority`; a patrol's and an intercept's `provinces` and a join's `join_fleet` (the unit aimed at), an air order's `provinces` (its area), a sortie's `start` and `current`, a raid's `attack` (its hours to the next try at the convoys - *inferred* from the field it is written from), an escort's `convoy` and second `province`, a support attack's `combat`, an air supply's `supplies` and `fuel`, an invasion's `transport` (the sea the landing is made from, `FINDINGS-transport.md` B.1) |
| plan | `active`, `allow_hq_reorg`, the three stances, `our_power`, `their_power`, `embark`, `debark`, `path`, `path2`, `ops_area`, every `objective`, `min` |

- **A unit with no `location` is thrown away**, as the original deletes one - but the unit of a `unit_deployment`,
  which never has one.
- **A carrier's air group is read inside its ship**, the only place it is written, and based where the fleet is.
- **A land unit aboard a fleet or an aeroplane is read inside its carrier**, and once every unit is read it is put back
  under its land superior by `parent` - at the end of its children - or at the top of its country's units where it has
  none, and aboard; a fleet's passenger is dug in nowhere and stages nowhere, as the original's fleet zeroes both.
- **An order goes on from where the save has it**: nothing in it is started again. A target is found once every unit is
  read, and a patrol's marks the fleet shadowed.
- **No brigade takes its historical model**: a save never writes one, so a brigade's figures are its type's and its
  levels' and nothing else, and the strength and organisation are as written.

**The rest of a country's military part:**

| block | read |
| --- | --- |
| `active_leaders` | each `<id>=` a leader of the files', in service in that order: `rank`, `skill` (only where not the file's), `current_experience`, `gainable_trait` (`duration`, `current`, each trait's progress) and `trait` - one held already not added twice; then his skill and experience raised to their floors. A number of no file's leader is passed over |
| `military_construction`, `building_construction`, `convoy_construction` | in queue order, interleaved as written, each with `id`, `size`, `cost`, `duration`, `progress`, `status`, `location` - **never priced again**; a unit's line its `name`, `unit`, `manpower`, `is_reserve` and `factor`, `accumulated_experiance` and `accumulated_progress`, and each brigade's `id`, `name`, `experience` and `model`; a building line its `building`, counted from one in the file's order; a convoy line escorts where its second `status` is `yes` |
| `unit_deployment`, `building_deployment` | a finished unit with no location, with the deployment's own `id` (`CUnit.DeploymentId`), and a finished building, waiting to be placed |
| `auto_maintain_convoy`, `auto_supply_convoy`, `auto_resource_convoy` | the three switches |
| `saved_templates` | in place of the country file's; the original's empty `PW_TEMPLATE_` padding left out (*Ours*) |

**Checked** with `OpenHOI3.SaveCheck load <install> <save> [hours] [later save]`, which finds every unit, brigade,
leader in service and line of the save by its number and sets it beside the save - and, given a later save of the same
game, runs to its hour and does it again, counting what differs by country. On 2026-11-04:

- `Germany1936_01_07_22.hoi3` read: **2,001 of 2,001 units** - location, path, progress, leader and order - **4,072
  of 4,072 brigades** in strength, organisation and experience, **3,303 of 3,303 leaders in service** in order, rank and
  experience, and **179 of 179 lines** in number and progress. `Ireland1938_08_25_03.hoi3`, with carriers, passengers,
  deployments and two years of the player's units: 2,304, 5,127, 4,623 and 253, all of them, and 24 hours run on.
- From `_01_07_22` to `_01_08_02`, four hours over a midnight: locations 2,000 of 2,001, paths 1,980, orders 1,993,
  strength 3,945 of 4,072, **organisation 3,100 of 4,072 - Germany's every brigade the save's** - and the lines'
  progress 173 of 179. What differs is the AI and one thing not settled: Japan's 319 come from a theatre headquarters
  its fresh AI makes in the first hour (the save's theatres are in its `ai` block, not read), which raises the officers
  it needs; Britain's 219 are each a brigade reaching the ceiling the night's officers raise an hour after the save's
  does (*Not established*).

The first run found one fault, mended: the officer ratio is made in the hour, so a loaded game's first hour held every
brigade to its type's own organisation until the ratio was made as the game is entered.

## 6. What the third increment reads: wars, pacts, trade and convoys

The first half of the third increment. Read off the saves and the history's own readers, whose blocks are the same
shape; nothing in it needed the executable read again.

| block | read |
| --- | --- |
| `active_war`, `previous_war` | the name, `limited`, every `attacker` and `defender` in order, `action` (the hour of its last action), and each `war_goal` - `casus_belli`, `actor`, `receiver`, `country`, `region` - filed under its actor, as the history's goals are; a finished war onto the list of finished wars. A war has no start date in a save |
| `diplomacy` | `guarantee` (its start; its end is twenty years on, `agreements.md` section 6), `alliance`, `vassal` (the second a puppet of the first), `nap`, `embargo`, `influence` (for its influencer's faction) and `align` (for its leader's), each `first`, `second` and `start_date`; `trade` - `from`, `to`, `start_date`, `trade_from` and `trade_to` in the goods' order (`trade.md`, section 1), and its `convoy`, found among its owner's once every country is read |
| a country's `convoy` | its number (type 42 - its own count - whose serial is its number here, as a new game numbers convoys), `start`, `end`, `path`, `convoys` (its transports), `escorts`, `start_date`, `last_attack`, `daily`, `trade`, `ship` (a flag for each good it carries, in the goods' order), `lend_lease` and `lendlease_targets` (the giver it serves) |
| a country | `convoys` and `escorts` (its pool), `mobilize` (written only while mobilised), `slider_locks` (the production screen's slider locks, written for the played country alone and read from any block; `sliders.md`, section 3 - since 2026-11-13) |
| a province | `drawn`, `last_drawn`, `throughput`, `last_throughput` - what was asked of it and what passed, today and yesterday, which the day's carrying works from (`supply.md`, section 12) |
| top | `convoy`, the next convoy's number |

What a convoy would have and what its loading land yields are worked out by the day, as in a new game. The Allies'
copies of a guarantee (`factions.md`, section 5) come back as plain guarantees, which act the same.

**The reading found a fault in the Clausewitz reader**, mended: a block with no key - a save's list of finished battles
is a run of them - was skipped while its closing brace closed the block around it, so everything after it in a save
was read a level too high. Now it holds its own entries. Nothing a new game reads changed: the script check, the
technology levels and the AI's strategies are as they were.

**Checked** with `OpenHOI3.SaveCheck load`, which now also sets the wars by name and sides, the goals, every kind of
pact, the routes with their convoys, every convoy - harbours, transports, escorts, goods, way - and each country's pool
and mobilisation beside the save. On 2026-11-05: `Germany1936_01_07_22.hoi3` - 1 war and its goal, 22 guarantees, 9
alliances, 9 puppets, 1 influence, 43 routes, 241 convoys and 101 pools, all the save's; `Ireland1938_08_25_03.hoi3` -
4 wars, 21 guarantees, 10 alliances, 11 puppets, 2 NAPs, 3 influences, 4 alignments, 580 routes and 547 convoys, all
of them. Four hours on to `_01_08_02`: the pacts the same, one trade route the AI's own, and with Italy's mobilisation
read and yesterday's pools (*Ours*) **organisation 3,203 of 4,072 and strength 4,011** (3,100 and 3,945 in section 5).
Two of Britain's and Japan's supply convoys are dropped at the first midnight where the original keeps them
(*Not established*).

## 7. What the third increment reads: battles, finished battles and sunk ships

The second half of the third increment, read out of the original for it (`reversing/findings/FINDINGS-savedbattle.md`).

**`sunk_ships`** [exe] is every ship that has left the game this session - sunk at sea, finished by bombs, or gone with
its fleet when the fleet was disbanded or taken out of the game - in the order they went, each under its owner's tag,
which is all that says whose it was. Nothing is ever taken off it. It is written as a ship in a fleet is, with `sunk_by`
(its sinker's own id) where it was sunk. Here it is `CGameState.SunkShips` (`Sim/Units/CSunkShips`), kept in play from
now on: a ship sunk in a battle at sea or by bombs, and every ship still in a fleet `CCombatRetreat.RemoveFromGame`
takes out. It comes before `combat` in the file, so a battle's ships are found in it.

**`combat`** holds each battle being fought under its kind's key - `land_combat`, `naval_combat`, `air_combat`,
`ground_bombing`, `land_bombing`, `naval_bombing` - and `history`. A battle is made blank and put on the list of battles,
**a bombing at its front and any other at its back** [exe] - in play as on a load, so **a save's bombings come back in
the opposite order**, and a bombing's hour is run first. Then [exe]:

| key | read |
| --- | --- |
| `location` | the province (`CCombat.Province`) |
| `day` | the rounds fought, -1 before the first hour (`Hour`) |
| `duration` | the hours begun (`Duration`); the two are equal after any whole hour |
| `terrain` | checked against the terrains; the province's is what a battle here fights on |
| a side's `unit` | each taken on as a unit joining in play is (`CCombatSide.Add`, `CNavalBattle.Join`, `CAirBattle.Join`, `CBombing.Join`): its owner on the side, its brigades' targets forgotten, what it brings counted into `size`, and **a fleet's ships put at their distances again**, a hundred further out since the battle has had a round - the saved `current_distance`s do not survive. Nobody goes into reserve: the original loads a save before any game is on screen |
| `dice`, `losses` | as written |
| `sunk_ship` | the ships the side has lost, or a bomber's has finished, found in `sunk_ships` by their own ids |
| `size` | one figure for each unit type, in the original's database order [mod: units/*.txt] - its empty type first, then the `units` files in the byte order of their names, upper case first (`CSubUnitType.DatabaseIndex`) - overwriting what the joins counted. A side counts **a land brigade's strength a hundred times over** - men, at a constant hundred to a point [exe] - **and one for a ship or a wing**, whatever its strength, every time a unit joins, so one that joins twice counts twice; a bombing's sides count nothing (`CCombatSide.Size`) |
| `tactic` | the tactic's place in `common/combat_tactics.txt` from 1 [mod: common/combat_tactics.txt]; written only where the side has one, which is every land side |
| `front`, `reserves` | a land side's lists, in their order, as written |
| `positioning` | a naval side's, overwriting what the joins worked out without the roll; the roll is not saved and never made again |
| a bomber's `strength`, `building`, `pool`, `current_producing` | what its hits put on their targets, counted as they land (`StrengthDealt`); the levels it took off each building, the empty building first and then `common/buildings.txt`'s order [mod: common/buildings.txt]; what it took of the stockpile and of the day's production, in the goods' order |

As a battle's block ends [exe]: it has begun, so its first hour is an ordinary one - no start, no naval roll; each unit
holds it again; **a land battle sets up its attacker's front where it is not, asking twice, and never its defender's**,
and sums its tactics; a ground bombing's side with nobody on it is the province's holder's. A land side's front counts as
set up unless it has units and none of them on a list - a side saved before its battle's first hour, which no save of
the maintainer's has, nor can a player's attack make one: ordered while paused and saved at once, the save holds the
march and not the battle, which the hour makes (`Soviet_Union1941_07_16_19` and `_20`, province 1985); such a defender would stand with an empty front and lose at the fourth hour without a shot, which
is the original's. Entering the game, every unit in a battle starts its count towards breaking off again from nought.

**`history`** is the battles finished in the past week: each entry's `location`, `attacker` and `defender` (`---`
none), `end_date`, `convoy` (written only for a raid on a convoy) and `type`, the kind from 1 - land, naval, air,
ground, land and naval bombing [exe]. They are put at the end of the list as written; a load prunes nothing, and the
first hour prunes to a week as every hour does.

**Checked** with `OpenHOI3.SaveCheck load`, which now sets every battle - kind, province, hours, both sides' units - each
side's dice, losses, tactic, lists, sunk ships, positioning, `size` and a bomber's tallies, the order of the list of
battles, whether each unit holds its battle, every finished battle and every sunk ship beside the save. On 2026-11-05,
all of them in five saves: `Ireland1938_08_03_14.hoi3` - 4 land battles and 3 land bombings, 56 finished, 30 sunk;
`Ireland1937_11_02_21` - 2 land and a naval battle, 22 and 27; `Ireland1936_02_02_04` - a land and an air battle and a
land bombing; `Germany1936_09_02_21` - a land battle and a ground bombing, 22 finished; `Ireland1937_07_05_02` - a naval
battle, 18 sunk. A day runs on from each without fault.

## 8. The writer, its first increment

`CSaveWriter` writes a game as the original's file, `CSaveTextWriter` with the original's layout [exe]
(`FINDINGS-save.md`; `FINDINGS-savegame.md`, B.1 and B.2): lines end in CR LF; a key is indented a tab a level, the top
level at column 0; a block's brace stands on its own line at its key's indentation; the root opens no brace and still
closes one, so the file ends with one too many; a list of numbers stands at column 0, each followed by a space, its
brace closing on the same line; a building's pair puts a space before each figure; a country's shares are a list
indented at its block's depth, with five places; `faction={` is written with no line's end, so the first faction's
key follows it on the line. The text goes out byte for byte as it was read: the readers take Windows-1252 as Latin-1,
which keeps every byte.

**What it writes**, in the original's order and each key where the original writes it:

| block | written |
| --- | --- |
| top | `date`, `player`, `convoy` (the next convoy's number), `seed`; every province by id; every country in `common/countries.txt`'s order; `faction`, each with its members |
| province | `flags`, `owner`, `controller`, every `core`, `manpower`, `leadership` and `revolt_risk` above nought, `strategic_resource`, every building built or working (its pair), `points`, `nationalism` where not nought, the seven pools each where any good is not nought - `pool`, `throughput`, `last_throughput`, `drawn`, `last_drawn`, `current_producing`, `max_producing`, a pool's goods supplies, money, crude oil, fuel, metal, energy, rare materials - `weather`, each figure not nought and the pressure where not the define's, and every `modifier` an event gave |
| country | `human` for a played country, `flags`, `variables`, `capital`, `acting_capital`, `technology` (every technology's level and progress), every category's ability, `diplo_influence`, `distribution`, `leadership`, `neutrality`, `officers`, every filled post, every law, `ideology`, every `modifier` an event gave, `manpower`, the four `auto_*`, its relation, threat and occupation policy with every country that owns land, itself among them, `mobilize` while mobilised, `dissent`, `national_unity`, `effective_neutrality`, `highest_threat` |

**What is not held is not written** - no stand-in for it, as nothing here answers from one. A technology's third figure,
the leadership put into it, is written nought, and `notech`, the database's empty first entry, not at all.

**Checked** with `OpenHOI3.SaveCheck write <install> <save> [written file]`, which loads a save, writes it, sets every
province block beside the save's byte for byte and every block key by key - what the save has that was not written,
what was written that it has not, values that differ and keys out of its order. On 2026-11-05, `Germany1936_01_07_22.hoi3`:
**9,372 of 14,167 province blocks byte for byte, every other differing only by `out_of_supply_days` (4,400), `capital`
(402) and one `history`**, which the game does not hold; every country's keys in the save's order, the written ones the
same but each technology's third figure; `Ireland1939_09_06_18.hoi3` the same, with 8,548 provinces byte for byte and 22
countries' effective neutrality a few hundredths off, the threat having moved within the day. **The check found a gap in
the loader**, mended: `highest_threat`, the country each fears most, was not read, so every loaded country had its whole
neutrality as its effective neutrality - 98 countries of the 1939 save, Britain 68 where the save has 0. **And a second**:
the modifiers events have given a country or a province, `modifier = { modifier = .. date = .. }` - 22 in the save of
16 July 1941 - were reported and not read; they are read and written now.

## 9. The writer, its second increment: the military part

What the loader's second increment reads (section 5), written as the original writes it [exe]
(`FINDINGS-savegame.md`, sections C.1 to C.8): `CSaveUnitWriter`, and a `Write` beside each reader - `CSaveOrders`,
`CSaveLeaders`, `CSaveLines`.

**The top level** gains `unit`, the history's counter, after `convoy`, and after `seed` the `id` block, the last serial
of type 4713. **Every number goes back as its pair** (section 5): below the counter the history's own serial of type
41, from it on type 4713 counted from one. The `id` block is the game's own count of what it has minted, where the
original gathers the highest serial of what it holds; after a load they are the save's, and the original numbers on from
it either way. **A new game counts as the history's everything it made before the game is entered**, its two days among
it: the original's start save holds nothing of type 4713, its `unit` counter is 5,608, and a new game here comes to the
same; its `id` of 106 counts what the original minted in those days and never writes, and here nothing is, so none is
written.

**A country's military part**, after `manpower` and in this order: the queue, each line under its kind's key; the
four `auto_*` switches; `active_leaders`; the units at the top of its tree; what waits to be placed; then the relation
blocks, and `saved_templates` after them.

| block | written |
| --- | --- |
| line | `id`, `size`, `cost`, `duration`, `progress`, `status`, `location` where one, `country`; a unit's `name` where of two letters or more, `unit`, `manpower`, `is_reserve` and `factor` for a reserve, each brigade under its type (`id`, `name` where one, `model` - an indented list - and `experience` above nought), `accumulated_experiance`, `accumulated_progress`; a building line's `building` counted from one; a convoy line's second `status`, whether it builds escorts |
| leader | `<id>=` at column 0, `rank`, `skill` where not his file's, `current_experience` with five places, `gainable_trait` - each trait's progress, `duration`, `current` - and `trait`, a list of words at column 0, where he has any |
| unit | `id`, `name`, `base` for a fleet or an air unit not on a carrier, `leader`, `previous` above nought, `movement_progress`, `path` (an indented list) where it has one, `location` where it has one, `retreat`, `dig_in`, `attack_delay` and `combat_hours` above nought, `is_prioritized`, `can_reinforce`, `can_upgrade`, `fuel`, `supplies`, the brigades, the units under it but those aboard, the order, `start_date`, `disengage` and `possible_retreat` while the count runs, `end_date`, `track`, `was_paradropped`, the `plan` of an active corps or above; a land unit's `target`, `staging_province` and, aboard, `parent`; a fleet's or an aeroplane's passengers, each under its level; a fleet's `at_sea`, last |
| brigade | `id`, `name`, `type`, `organisation`, `strength`, `highest`, `experience`, `is_reserve`, `pride`, `current_distance` while its unit is in a battle - not a bombing - `sunk_by`, a `<technology>={level progress}` line for each of its type's technologies, and a carrier's `air` groups |
| order | `province`, `target` for an intercept (the fleet its order names) or a join, `stance`, `start_date` and `death_date` (`1.1.1.0` none), `return_to_base`, `time`, `priority`, `stop`; an air order's `return_to_base` and `priority` again, a naval order's own - its `return_to_base` always `no` - and its `capital`, `sub_unit` and `other`, always nought; an air order's `provinces`, a line each, and a nought for each air type in the database's order; a patrol's fleet (the null pair for none) and an intercept's once its search has found one, each followed by an empty line; a join's `join_fleet`, or its second `target` and an empty line; a raid's `attack`, a sortie's `start`, `provinces` and `current`, an escort's `province` and `convoy`, an invasion's `transport`, an air supply's `supplies` and `fuel`, a support attack's `combat` once it has joined |
| plan | `active`, `allow_hq_reorg`, the three stances, `path` and `path2` where any, `our_power`, `their_power`, `embark`, `debark`, `ops_area` (a list at column 0), each `objective`, `min` |
| templates | each template its types at column 0, then empty `PW_TEMPLATE_<n>` up to twelve, as the original's list always is |

A unit's `disengage` comes between its two dates in every save - the fact base had the dates together, and is corrected.

**The order keys the game does not use**, read for the writer (`FINDINGS-savegame.md`, section C.11) [exe]:

- **`stop`, the halt figure**, is -1.000 from every order's constructor; an order given by a command takes the stance's
  `AGGRESSIVE_`, `DEFENSIVE_` or `PASSIVE_ORGANISATION_LIMIT` [mod: common/defines.lua], and one the engine makes itself
  - a null order, its own move order, and the carrier protection an air unit on a carrier is given unasked - keeps
  -1.000. Nothing in the original was found reading it but its writer, and nothing here reads it: it is kept on the
  order (`CMission.Stop`), set where `CUnitOrders.Give` gives an order and left at -1.000 by `CCarriers.GiveDuty`, so
  that a save says what the original's would.
- **A naval order's `capital`, `sub_unit` and `other` are nought for good**: the routine meant to count the fleet's
  capital ships, submarines and others into them counts into a copy and keeps nothing. Written nought; a save's other
  than nought is reported.
- **An air order's figure for each air type** is one of a list of noughts its constructor makes; written nought for
  each air type in the database's order, and a save's other than nought reported.
- **A naval order's two `return_to_base`** are the order's, set when it is told to return, and the naval order's own,
  which nothing in play sets; a load puts both into the naval order's own, so **being told to return is lost on a load,
  in the original as here** - which `Ireland1937_10_03_21` shows, an interceptor told to return going on with its
  intercept once loaded. Written the order's and then `no`; a save's naval order's own set is reported.

**An intercept's `target` and `provinces` are two things** [exe]: `target` is the fleet the order names, kept as
`CMission.OrderTarget` and read by nothing here; `provinces` is the fleet its search has found, `CMission.Target`,
written only once there is one - an intercept still waiting in port writes none, as in the saves of July and October
1937 and the first hour of May 1940's. The loader took both as the fleet it is after, which sent a loaded waiting
interceptor straight out to sea. The AI's intercept names the enemy fleet it is given on (`moving.md`, section 14), as
all six of the original's saved intercepts do; until 2026-11-05 it named none, and so wrote no `target`.

**A deployment's number** is its unit's and one [exe]: a line's unit takes a number and **two are passed over**, the
first for the finished unit's deployment - the original's own lines do so, from a file and in play. A line ordered or
going on to the next of a series here passes them over too now, and a finished unit's deployment takes the first
(`buildqueue.md`, section 4); a deployment with no number could be found by nothing in the original, and never placed.

**Not held, so not written**: a brigade's `home` and `builder`, a unit's `country_intel` and a lent unit's
`expeditionary_owner` and `expeditionary_date`; a paradrop's `message` and a rebase to a carrier's `carrier_target`, in
no save.

**Checked** with `OpenHOI3.SaveCheck write`, its `keys=` listing every key of the military part, and each country's
military part set beside the save's line for line with the keys above taken out. On 2026-11-05, every country's in
`Germany1936_01_07_22`, `Ireland1936_02_02_04`, `Germany1936_09_02_21`, `Ireland1938_08_25_03` (carriers, passengers,
deployments), `Ireland1940_05_23_18` and `_19` and `Soviet_Union1941_07_16_19` - 191,822 lines in the first; and in
`Ireland1937_07_05_02` and `Ireland1937_10_03_21` every country's but what a load changes as the original's does: a sea
battle taken up again, its ships at their distances again and its fleet's count towards breaking off from nought
(section 7), and an interceptor's being told to return. `write <install> new <start save>` writes a new game beside the
start save: the counter is the save's and the layout the save's; the figures and the trees are meant to differ, the
original's AI having moved by then. **The check mended the loader five times**: an invasion's landing sea was reported
and not read; a deployment's own number was thrown away; the player's numbers left a gap at the counter, so a new
game's first unit would have been serial 0, which the original never gives; a waiting intercept was sent after the
fleet its order names; and a naval order kept its being told to return across a load, which the original's does not.

## 10. The writer, its third increment: pacts, wars, convoys and battles

What the loader's third increment reads (sections 6 and 7), written as the original writes it [exe]
(`FINDINGS-savegame.md`, B.2; `FINDINGS-savedbattle.md`, A.2, A.3, B.2 to B.4): a `Write` beside each reader -
`CSaveDiplomacy`, `CSaveWars`, `CSaveConvoys`, `CSaveCombats` - and `sunk_ships` in `CSaveWriter`.

**The top level** gains, after the countries, `sunk_ships` - written always, `sunk_ships={ }` with none - and after the
factions `diplomacy`, `combat`, every `active_war` and every `previous_war`. **A country** gains, after
`national_unity`, `escorts` and `convoys` (its pool) and each of its convoys.

| block | written |
| --- | --- |
| `diplomacy` | **every pact in the order it was made, whatever its kind** - the original keeps them on one list - each `first`, `second`, the `end_date` the history gave a guarantee, an alliance or a puppet (`vassal`) where it gave one, and `start_date`; a trade's `to`, `from`, `trade_from` and `trade_to` in the goods' order (lists at column 0), its `convoy` where it has one, then `first` (its `from`), `second` (its `to`) and `start_date` |
| `active_war`, `previous_war` | `name` - a war made in play's written out, "2nd War of German Aggression", as the war list shows it - `limited`, every `attacker` and `defender` in order, `action` (`1.1.1.0` none), `original_attacker` and `original_defender`, `---` in every save, and each `war_goal`'s `casus_belli`, `actor` and `receiver` - and its `country` and `region` where it names them, in no save, so where they go is *inferred* |
| a country's `convoy` | `id` (type 42), `start`, `end`, `escorts`, `convoys`, `path` (an indented list) where it has one, `last_attack` (`1.1.1.0` never), `start_date`, `daily` - always, each good not nought in a pool's order - `trade`, `ship` (a 1 or 0 for each good, at column 0), `lend_lease`, and `lendlease_targets`, the giver, for a lend-lease convoy |
| `sunk_ships` | each ship under its owner's tag, as a ship in a fleet is written |
| a battle | under its kind's key, in the list's order: `location`, `day`, `duration`, each side, `terrain` (the province's) |
| a side | `dice`, every `unit` in the order it joined, every `sunk_ship`, `losses`, `size` - a figure for each type of the database, the empty type first, at column 0 - `tactic` where it holds one; a land side's `front` and `reserves`, a naval side's `positioning`, a bomber's `strength`, `building` (the empty building first), `pool` and `current_producing` |
| `history` | the finished battles' own `history` key, left out where there are none; each entry an empty key and its block - `location`, `attacker`, `defender` (`---` none), `end_date`, `convoy` where set, `type` - and a one-space token after each, so the first stands after a line of indentation and the closing brace after the last token |

**Each pact holds its number among the pacts** (`CGameState.NextPact`) - an alliance, a guarantee, a NAP, an embargo,
an influence, an alignment and a trade route its `Made`, a puppet its country's `PuppetMade` - given as it is made in
play, in the save's order as a save is read, and for a new game **by its start date and within a date in the order the
history's files are read** - the alliances, the guarantees, the NAPs and then the puppets, each file in its order - as
the original applies its history a day at a time: the start save's guarantee of 1935 stands before its alliances of
1936 (`CGameStateLoader.NumberHistoryPacts`; *measured* on the start save). An alliance and an embargo now hold their
start, and a guarantee, an alliance and a puppet the end the history or a save gave them, which only the writer reads.

**A war's name**: a war made in play is written under its name written out, so the writer is handed the localisation;
and **a save's name a declaration made is read back into its parts** - the key whose text it fits, its order and its
adjectives, each a country's - so that a war declared after the load is named against it, as the original's
`CGameState_IsWarNameTaken` compares names (`war.md`, section 7). Until 2026-11-05 a loaded war kept only its text,
and a German declaration after loading the 16 July 1941 save would have been "War of German Aggression" a second time.

**A history war's `action`** is the hour the history is folded to, not its first entry's: the start save's war of 3
October 1935 has `1936.1.1.0`, which a new game now gives it - measured, and then read: entering a new game sets every
active war's to the tick (`FINDINGS-savegame.md`, A.5).

**A new game's finished wars** [exe] (`reversing/findings/FINDINGS-war.md`, C.13.1): every file of `history/wars` is a
war, whatever it holds [mod: history/wars]; one with somebody on both sides on the start date is fought, one that is
not and began on or before it - its first dated entry, or none at all - is finished, a `previous_war`, and one that
begins later is neither. So the base game's two files with no war in them - `ContinuationWar.txt`, a comment alone,
and `SovietJapaneseWar.txt`, all commented out - are **the two nameless `previous_war`s every save of the original
holds**, and a new game here holds them too since 2026-11-05 (`CWarReader.Read`); they had been missing.

**Not held, so not written**: a `casus_belli` pact (`type`,
`first`, `second`, `start_date`; two in the 16 July 1941 saves); an `undeclared_war`; a sunk ship's `home` and
`builder`, as in the unit tree.

**Checked** with `OpenHOI3.SaveCheck write` and each block set beside the save's as text. On 2026-11-05, in
`Germany1936_01_07_22`, `_03_31_19` (lend-lease convoys) and `_09_02_21`, `Ireland1936_02_02_04`, `1937_07_05_02`,
`1937_10_03_21`, `1937_11_02_21`, `1938_08_03_14`, `1938_08_25_03`, `1940_05_23_15`, `_18`, `_19`, `1941_07_16_18` and
`Soviet_Union1941_07_16_19`: **every war and finished war, and every country's pool and convoys, the save's byte for
byte** - 14,666 lines of convoys in `Ireland1938_08_25_03`; **the pacts byte for byte** but for what is not held, in the
save's order - 12,715 lines in the same save; **every battle and every list of finished battles byte for byte** in the
five saves with battles - land, naval and air battles, a ground and land bombings, 454 lines of finished battles in
`Ireland1938_08_03_14` - the bombings coming after a load in the opposite order, as the original's load turns them
(section 7); and every sunk ship but for `home` and `builder`. `write <install> new <start save>` gives the start save's
283 lines of pacts byte for byte.

## 11. Saving in play: the save dialog and the autosave

**The save dialog** is the in-game menu's (`ingamemenu.md`, section 6): it lists the player's `save games` folder and
the install's, writes to the player's, offers the country's name and the hour, and asks before writing over a file.
A save is `CSaveWriter`'s text in the original's bytes, the file truncated where one stands (`CSaveFiles`).

**The autosave** [exe] (`reversing/findings/FINDINGS-autosave.md`):

- **Decided as each day begins**, by the interface's daily update after the day's pass: what an earlier day decided
  and has not been written yet is dropped, then the frequency is asked of today. Never in a tutorial - there is none
  here.
- **The frequency** is `settings.txt`'s `autosave` [mod: settings.txt, the game options window]: `NEVER`; `WEEKLY`, a
  day whose number from the calendar's start, less one, is a whole number of weeks; `MONTHLY`, the 1st; `HALFYEAR`, the
  1st of January and of July; `YEARLY`, the 1st of January, the default; `FIVE_YEAR`, the 1st of January of a year
  five divides (`CAutosave.IsDue`).
- **Written a frame later** in the original: the frame that finds the decision only marks that it has waited, and the
  next writes, so what it holds hangs on the speed. **Here it is written as it is decided**, the hour that turned the
  day run whole, so it always holds the turn of the day (`CSession.DecideAutosave`; `docs/compatibility.md`, entry 16).
- **Three files** in the player's `save games` folder: `olderautosave.hoi3` is deleted, `oldautosave.hoi3` becomes
  it, `autosave.hoi3` becomes `oldautosave.hoi3`, and the game is written to `autosave.hoi3`. **They are the
  original's own files** - the player's folder is shared, as the settings are - so a game played here shifts the
  original's autosaves along, and the other way round.
- **`debug_saves`** other than nought [mod: settings.txt] makes the original ignore the frequency and save on the 1st of
  every Nth month, one file a save, under a name the reading left part unread. **Not built**: said once, and nothing
  autosaves.
- No message says a save was made; a failure is logged, `Failed to write save file`.

**Checked** by `SaveFilesTests` - the six frequencies' days, the three files shifted along and the dialog's names - and
with `--click`: the dialog listing the maintainer's folder newest first, offering `Germany1936_01_01_00`, asking
`CONFIRMSAVETEXT` before writing over that save and leaving it untouched on Cancel, and as Italy writing
`Italy1936_01_01_00.hoi3`, which `OpenHOI3.SaveCheck load` read back. **The hour an autosave holds hangs on the speed
in the original**: the maintainer's three autosaves - `1941.5.1.1`, `1941.6.1.1` and `1941.7.1.1` - were made at speed
and hold the first hour of the day, and at a slow speed it holds the turn of the day, `.0` (his word, 2026-11-05). Here
it always holds the turn of the day, by his decision.

## 12. The fourth increment: politics and the research queue

Read and written both ways (`CSavePolitics`), each key where the original writes it in a country block [exe]
(`FINDINGS-savegame.md`, B.4; `reversing/findings/FINDINGS-government.md`, A.6, B.4, B.5 and B.7):

| key | what | read | written |
| --- | --- | --- | --- |
| `research` | one a technology in the queue, in its order | a name no technology has is left out and reported | after the categories |
| `last_election` | the day of the last election, at hour 0 | `1.1.1.0` is never | then these three |
| `last_surrender` | the hour it last surrendered | `1.1.1.0` is never | |
| `election` | read by nothing in the original, loaded inverted and saved again | not kept | always `yes` - nothing sets it, and every save of the maintainer's has it |
| `duration` | the country's own term in months, 0 its government's | | |
| `spiescaught` | the spies caught at home this month | | after `diplo_influence` |
| `organization`, `popularity` | `{ data = { a figure a party } }`, one by each ideology's number, number 0 included | made at the ideologies' count, a number past the save's nought | after the shares, **each key at column 0** |
| `government` | the form of government | none written is none | after `officers`; left out where there is none - the rebels' in every save |
| `ministers` | the pool of ministers it may appoint, in its order, each by its number in the country's file | a number the file does not give is left out and reported | after the tags' blocks, on one line, `ministers={1 2 }`, `ministers={}` with none |
| `cabinet` | each party's seats, by its ideology's number | **allotted again from popularity**, and the save's list kept behind | after the spies' keys, a list at column 0 |

**A load does not run a new game's setting-up**, so until this increment a loaded game had no party figures, no seats
and an empty pool, and the month's drift, the elections and the filling of posts worked on nothing; they are the save's
now.

**The cabinet after a load** [exe, *inferred* in part]: the original's `AfterLoad` allots the seats again from
popularity, as a new game's setting-up and every drift do (`FINDINGS-government.md`, B.4) - so a load here does too
(`CCabinet.Allot`). And reading `cabinet` **appends** the save's list to a cabinet already made, so the seats the game
uses are the first, and the save's list stands behind them, read by nothing and written again after them
(`FINDINGS-government.md`, B.7, *inferred* from the reading and the saves). `Germany1936_04_20_03`, a game the
maintainer had loaded, bears it out: every country's `cabinet` has 22 figures, the first eleven the seats its
popularity allots and the last eleven another set. So a country keeps the list a save gave (`CCountry.CabinetLoaded`)
and **a game loaded here writes the seats and then the save's list**, 22 figures for a save of 11, and 33 for that one -
as the original's would.

**Not held, so not written**: `nocategory` and `last_mission_cancel` - in every save of the maintainer's `0.000` and
`1.1.1.0`. `nukes` is held, read and written since 2026-11-09 (`nukes.md`). `war_exhaustion` is held, read and written since 2026-11-07 (`warexhaustion.md`, section
6).

**Checked** with `OpenHOI3.SaveCheck write` and each key's text set beside the save's. On 2026-11-06, in
`Germany1936_01_01_00`, `_01_07_22`, `_04_20_03` and `_09_02_21`, `Ireland1936_03_11_04`, `1937_07_05_02`,
`1938_08_03_14`, `1939_09_06_18`, `1940_05_23_15` and `1941_07_16_18`, and `Soviet_Union1941_07_16_21`: **every
country's research queue, dates, spies caught, party figures, government and pool the save's byte for byte**, and its
cabinet the save's seats followed by the save's list, **but for Italy in `Ireland1936_03_11_04`**, whose saved seats
are not what its saved popularity allots - two to one party where the popularity gives one, the only such of 1,111
country-saves, likely an event that moved popularity without an allotment after it, so a load here allots it otherwise
as the original's `AfterLoad` would. `write <install> new <start save>` gives every key the start save's but
popularity and the cabinet it allots, which a new game's draw on our generator moves (`politics.md`, section 10).

## 13. The fourth increment: spies

Both ways (`CSaveEspionage`), after a country's convoys and before its `cabinet` [exe]
(`reversing/findings/FINDINGS-espionage.md`, the record's table; `FINDINGS-savegame.md`, B.4): `spies`, the free
spies, in thousandths; then five lists, each at column 0 with **an entry for the original's null country `---`
first** - nought, its date the null date (`FINDINGS-savegame.md`, B.2, item 14) - and then one a country in `common/countries.txt`'s order
[mod: common/countries.txt], so a country's place is its number here plus one (`espionage.md`, section 1):

| key | each entry | read into |
| --- | --- | --- |
| `spy_priority` | a whole number | the presence's priority |
| `spy_allocation` | thousandths | its level |
| `spy_date` | a date with its hour, unquoted, `1.1.1.0` for never | the hour its missions last changed |
| `spy_mission` | nine whole numbers a country, the missions in their order | the weights, and the shares worked out from them, which are not saved |
| `covert_ops_points` | thousandths | its covert-ops points |

A list that ends early leaves the rest nought. **Not held, so not written**: `last_mission_cancel`, which belongs to
the active missions (`active_mission`, `last_mission`), not built - `1.1.1.0` in every save of the maintainer's.

**Checked** with `OpenHOI3.SaveCheck write` in the eleven saves of section 12, on 2026-11-06: **every country's spies
the save's byte for byte**. `write <install> new <start save>` gives every key the start save's but `spy_allocation`,
72 of 101 countries, which is the spies the two days catch by another roll (`espionage.md`, section 14).

## 14. The fourth increment: the top level's counters, flags, victory conditions, fired events and fronts

Both ways (`CSaveTop`, and the factions' and the player's in `CSaveLoader` and `CSaveWriter`), each where the original
writes it [exe] (`FINDINGS-savegame.md`, B.2):

| key | what | read | written |
| --- | --- | --- | --- |
| `automate_trade` | the local player's switch | given to the country played, whoever that is | after `player`, where one is played |
| `rebel`, `theatre` | the next rebel faction's and theatre's numbers | the game's counters | `rebel` before `convoy`, `theatre` after it |
| `flags` | the game's own flags, each `yes` | each set | after `unit` |
| `victory_conditions` | a `faction` list a faction - each chosen condition's name and date, `0.1.0.0` none, then the faction's name - and `game_finished` | as the original's loader sets them (`reversing/findings/FINDINGS-factionplay.md`, C.5): each named chosen, with its date, and **met where it has one**; the faction's others not chosen; a name the faction has not is left out and reported | after `flags` |
| `fired_events` | an `id = { id = N type = 40 }` an event that fires once and has fired | found by its number | after `id`, **by number** - the original's is its table's bucket order, *inferred* the same: no save has more than two |
| `weather` | each front, its `direction` and `rotation` in thousandths, `speed` - the hours to its next move - `home` and `provinces` | the fronts, in order | after `sunk_ships` |
| a faction's `progress` | the victory points its members hold | the game's figure, made again each hour | the first key of its block |

**Three things a load had lacked**: a new game's choice of victory conditions is not run on a load, so a loaded game
had none chosen and the faction box's tooltip read "0/0"; the theatre counter was the history's, so a theatre made
after a load could take a number a loaded one had; and a loaded game had no fronts, its first hour bearing new ones.

**A province is not marked as under a front** by a load: the original does not save it, and the first hour's weather
marks every front's provinces again before anything asks (`weather.md`, section 3). A zone's cooldown and heading are
not saved either, and start again at nought, as in the original.

**Not held, so not written**: the player's `ai` list and `automate_sliders` and `automate_tech_sliders`;
`gameplaysettings`; `count`, the draws made on the generator; `undeclared_war`; the three statistics and
`strategic_warfare`. And a faction's members are written in `common/countries.txt`'s order (*Not established*).

**Checked** on 2026-11-06 with each top-level block's text set beside the save's: in the eleven saves of section 12 and
`olderautosave.hoi3` of July 1941, **every one of these the save's byte for byte**, and each faction's `progress`
in three of them. `write <install> new <start save>` gives all but the fronts the start save's - a new game's are drawn on
our generator - and the seed.

## 15. The fourth increment: exile, goods, battles fought, strategic warfare, alignment and lend-lease

Both ways, each where the original writes it in a country block [exe] (`FINDINGS-savegame.md`, B.4):

| key | what | read into | written |
| --- | --- | --- | --- |
| `government_in_exile` | `yes` while in exile | whether it is (`war.md`, section 14) | after `acting_capital`, only while in exile |
| `strategic_warfare` | `alliance`, `convoy` and `ground_bombing`, each entry `"hour"="value"` at column 0, an empty line after each list | the three lists; each impact their sum, **nothing pruned** - an entry over a week old stays until the day's prune, as the saves hold them | then |
| `pool` | an exile's own stockpile | it | then these ten, **each written whatever it holds** (`CSaveLedgers`) |
| `home`, `usage`, `to`, `back`, `convoyed_out` | the day's ledgers so far: made at home, used by industry, supplies and fuel sent out and come back, carried out by convoy | each | |
| `traded_for`, `traded_for_sans_allied`, `traded_away`, `traded_away_sans_allied` | paid and paid for, with and without allied supply | each | |
| `land_battles_fought`, `air_battles_fought`, `naval_battles_fought` | the battles it has fought | each | then, each only above nought |
| `alignment` | `position = { x y }`, an empty line after it | where it stands on the triangle | after `mobilize` |
| `lendlease_targets`, `lendlease_distributions`, `lendlease_effectiveness` | whom it gives to, each one's weight and effectiveness | the giving, in order | after `cabinet`, only while it gives (`CSaveLendLease`) |
| `lend_lease_from`, `lend_lease_from_values`, `lend_lease_income` | whom it receives from, what each has sent not yet made IC, and the whole of that | the receipts, in order, and the income | then, only while it receives |

**A country in the lend-lease lists is its number among the countries plus one** - the null country `---` being
0, as in the spies' lists (section 13): Britain is 3 and the United States 5 in every save.

**Until this increment a loaded government in exile was no exile**: eight in the 16 July 1941 saves, each using its
host's stockpile where its own is the save's.

**Not held, so not written**: `prov_vp`, the victory points the country controls, an hourly sum the game keeps only by
faction (`progress`, section 14); `offmap_vp`; and lend-lease's `lend_lease_from_yesterday`,
`lend_lease_from_yesterday_values` and `lend_lease_income_yesterday`.

**Checked** on 2026-11-06 with each key's text set beside the save's, in `Germany1936_01_01_00`, `_03_31_19` (the
lend-lease of March 1936), `_04_20_03` and `_09_02_21`, `Ireland1936_03_11_04`, `1937_07_05_02`, `1937_10_03_21`,
`1938_08_25_03`, `1939_09_06_18`, `1940_05_23_15` and `1941_07_16_18`, `Soviet_Union1941_07_16_21` and
`olderautosave.hoi3`: **every country's the save's byte for byte**.

## 16. The fourth increment: the AI's strategy and theatres

Both ways (`CSaveAiStrategy`), after the templates [exe] (`FINDINGS-savegame.md`, B.4;
`reversing/findings/FINDINGS-aistrategy.md`, A and B; `reversing/findings/FINDINGS-theatres.md`):

- **`ai_hard_strategy` and `ai_event_strategy`**, the strategies scripts give, are not built (`aistrategy.md`, section
  12): written empty - `initialized=no`, `personality=balanced` - as every save of the maintainer's has them, and one
  that says anything is reported.
- **`ai`**: `initialized`, the `personality` (`military`, `capital`, `diplomacy`, `balanced` - a strategy never made
  `balanced`), then the eight lists in the original's order - `conquer_prov`, `defend_prov`, `threat`, `antagonize`,
  `befriend`, `protect`, `rival`, `vassal` - each entry an `id` (a province's number or a quoted tag) and a `value`, in
  the list's order; **read back as they stand**, an entry the scaling left at nought among them, which the lists' own
  add would drop (`CStrategyList.Append`).
- **Each `area_theatre`**: its `id` (type 45); each `front` - its `provinces`, `local_enemy` where it faces anyone,
  `front` (hot) and `sea=no`, every front here being a land one; its own `provinces` and its `key` provinces, each left
  out where empty; its headquarters as `unit`, an id pair; `country`; `hot`. Read under its own number, its provinces
  stamped with it, its areas and ports worked out again from its provinces as they are not saved.

**What a load does with them** [exe] (FINDINGS-aistrategy.md, A; FINDINGS-liveread.md, section 22): a saved strategy is
kept until the game is **entered**, and entering makes every country's again, at the save's own hour, so its ten days
start there - measured on the running game on 2026-11-10, where an earlier reading had the first midnight do it. A load
here keeps the save's until the entering pass (`CTick.RunEntryPass`), so a save read and written back at once is unchanged. Every country's theatres start marked, as the original's constructor leaves them, so the first hour refreshes the
loaded ones (`theatres.md`, section 8) - their fronts made again, their headquarters kept. **Until this increment a load
made every strategy and every theatre again at once**, the theatres from nothing under new numbers - Japan gained a
headquarters at P'yongyang the save does not have, four hours on from `Germany1936_01_07_22`; it does not now.

**Not held, so not written**: `consolidate` and `static`, which the strategies scripts give would set; `max_subunits`,
`land_perc`, `air_perc`, `naval_perc` and `armor_bias`, which the original works out with the strategy and nothing here
reads (`FINDINGS-aistrategy.md`, C, read and not built); and `military_access`, read in part (section I). `war_with` and
`building_prov` are in no save of the maintainer's.

**Checked** on 2026-11-06 with each country's three blocks set beside the save's, the keys not held taken out of the
save's: in the eleven saves of section 12 and `olderautosave.hoi3`, **every country's strategies, lists and theatres
the save's byte for byte**.

## 17. The player's automation and the game's difficulty

Both ways (`CSaveTop`), read 2026-11-06 (`reversing/findings/FINDINGS-frontend.md`, C.3 and D.3; `frontend.md`, section 4):

- **After `player`**: `ai = { ... }`, the seven flags - diplomacy, production, technology, politics, intelligence,
  Armies, deployment - each 0 or 1 but the sixth, which holds the Armies level, 7 Player Control down to 0 Theatres; then
  `automate_trade`, the played country's, `automate_sliders` and `automate_tech_sliders`, the Technology setting [exe].
  A flag other than 0 or 1 is refused on a load, as the original's setter refuses it [exe].
- **After the top level's `flags`**: `gameplaysettings = { setgameplayoptions = { difficulty mode } }`, the difficulty 0
  to 4 and the game mode 0 or 1 [exe]. A block without them leaves Normal and the normal mode.

**What a load does with them**: the played country's ministers are made by them - an area handed over makes that area's
minister, one not handed over has none - and its modifiers take the difficulty's block. The lobby may change them before
Play (`frontend.md`, section 4).

**Checked** on 2026-11-06 with `write` and `keys=automate,gameplaysettings`: `Germany1936_01_07_22.hoi3`'s the save's.

## Ours

- A key nothing reads yet is reported, by its block and how often it was met, where the original drops an unknown key
  without a word (*Stricter* in `docs/compatibility.md`).
- ~~The AI's strategy is made again on a load, where the original keeps the one a save holds; the save's `ai` blocks are
  not read yet.~~ Kept, as the original keeps it, since 2026-11-06 (section 16).
- With no `--country=`, the save's own country is played; the original's lobby chooses.
- A unit's number here is not the save's pair: what the player minted is numbered from the history's counter on, and
  the writer turns it back (sections 5 and 9). Another peer's minting is not told apart and is written as the player's.
- The top level's `id` is the game's own count of what it minted, where the original gathers the highest of what it
  holds (section 9).
- The save's empty `PW_TEMPLATE_` templates are left out, as a new game here has none.
- **What a province held when the supply pass last came to it is not saved**; a loaded province's is set to what it
  holds now. With none, nothing passes on the first midnight; with this, organisation four hours on gains 50 brigades
  (section 6).
- **A side's `retreat` list is not held.** The original has one and saves it, but nothing in it puts a unit there but
  the loader (`reversing/findings/FINDINGS-savedbattle.md`, A.11): a unit falling back, by hand or beaten, leaves its
  side at once and marches with its own `retreat`, as here, so the list is empty in every game and no save holds one.
  One a save did hold would be reported. A `tactic` of no number known is
  reported and leaves the side with none, so it picks one at its next hour, where the original holds an empty tactic
  that counts as one until the next change of tactics. Neither is in any save.
- The original keeps a battle on its province as well as its units; here a province's battles are found on the list.
- **The `save games` folder is made where it is missing** as a save is written; the original's write would fail
  (*inferred*, `FINDINGS-ingamemenu.md`, C.2).
- **Saves of one second are listed by name** in the save dialog, where the original's sort leaves them in no fixed order.

## Not established

- Whether a loaded game starts paused in the original.
- Lua on the original's load path.
- **Which hour first sees the officers the day adds.** Here the day's officers come after the hour's ratio, so a brigade
  sees them an hour later than in Britain's saves; making the ratio again after the day's leadership puts Britain right
  and two dozen countries wrong, so it is neither, and needs reading (section 5).
- The country a unit's `country_intel` triple names: its first number is a country's, by a numbering not matched to the
  save's order of countries, so the grants are reported and not read.
- The exception the original makes where a carrying fleet is in its passenger's own subtree, the fleet written in the
  passenger's place: in none of the maintainer's saves, and not handled.
- A naval order's `capital`, `sub_unit` and `other`, the halt figure `stop` and an air order's figure for each air type:
  nothing here holds them, so they are reported.
- **The first midnight after a load drops two supply convoys the original keeps** - Britain's to Dar es Salaam and
  Japan's 103: nothing is asked of the far depot, where the original asks 1.49. The same network agrees province for
  province on a new game (`OpenHOI3.SaveCheck supplypass ... network=10130`), so it is what the pass has on its first
  day after a load; not followed further.
- An alliance's and a
  puppet's `end_date` are kept for the writer (section 10); whether the original ends an alliance or a puppet on its date
  in play was not read.
- A side emptied in play is decided once, by a flag the save does not keep, so the original decides it again at the
  fourth hour after a load; whether its message and its last-battle figures come again was not traced
  (`FINDINGS-savedbattle.md`, A.8); a side with no units loads here as it is written, and was not followed further.
- **Expeditionary forces** - a unit one country has lent another, saved in the receiver's tree with
  `expeditionary_owner` (the lender) and `expeditionary_date` - are not built, and the two keys are reported. In the
  Ireland game's saves of 6 September 1939, Japan has lent 20 divisions to Manchukuo and 6 to Mengjiang, and the United
  States one to the Philippines; one hour on from `_18`, 540 of Japan's brigades' organisation differs from `_19`. The
  saves' brigades are full at Japan's officer ratio of 1.024 where ours is 1.237, Manchukuo's and Mengjiang's lent ones at
  that same 1.024, and the receivers' own at ratios of about 1.39 where ours are 0.33 and 0.56. Counting a lent
  division's officers to its lender gives Japan 1.022, near but not the save's, and does not fit the United States, so how
  the original counts a lent unit, and whose ratio holds its organisation, needs reading. The six saves of 23 May 1940,
  an hour apart, add Britain's 2 divisions lent to France and Germany's 1 to Slovakia: in every hour from one to the
  next, Germany's 531, Britain's 372 and France's about 300 brigades are held a little higher than the save's. `reversing/findings/FINDINGS-organisation.md`, section 1, has the
  count's two words on it.
  The twelve saves of 16 July 1941, an hour apart, have Germany's 7 divisions lent to Slovakia, and there Germany's
  brigades stand a little *above* our ceiling (62.895 to 62.000 for its army's headquarters), where Japan's stand below
  it - so neither "the lender counts them" nor "the receiver does" fits every case.
- ~~**Italy's ceiling of organisation in July 1941 is the officer ratio's times 1.05.**~~ It is the triggered modifier
  `veteran_army`, 250 land battles and more being +5% land organisation, the save giving Italy 251 (read in the running game,
  2026-11-10, `reversing/findings/FINDINGS-liveread.md`, section 5). Ours gives Italy that modifier on a load.
- A faction's members are written in the order they joined; here they are written in `common/countries.txt`'s, the
  order of joining not being held.
- What the sunk ships answer: `total_of_ours_sunk` and `total_sunk_by_us` are counts over `sunk_ships`, but their
  bodies were not read, so they stay unanswered.
- A ship's, a brigade's and a wing's `home` and `builder` are reported throughout the unit tree and among the sunk
  ships.
- **How the original numbers the brigades of a line ordered in play**: three numbers apart in the saves (4944, 4947,
  4950), where a line here gives them one each after the two passed over. Not read; the numbers are this game's
  (*Ours*), and only their order matters to the rules.
- **The order of `fired_events`**: the original walks its table of events that fire once bucket by bucket; written
  here by number, which the two saves holding any agree with (section 14).
- **Why Italy's seats in `Ireland1936_03_11_04` are not what its popularity allots** (section 12): an event's change
  of popularity, which no allotment follows, is the likely reason; not traced.
- **Whether anything reads an order's `stop`**: three searches found nothing but the writer; an unrecorded function
  reaching it another way is not ruled out (`FINDINGS-savegame.md`, C.11).

## 18. The strategic warfare manager's records

The last block of the file, `strategic_warfare`, is read and written whole (`CSaveWarfare`; `strategicwarfare.md`, section 6): byte for byte on `Germany1936_03_31_19`, whose 8 enemies carry whole rings. A game saved before its first day writes the played country as its `player`, as the original, which sets it as a game is entered.
