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
   increment reads, and the factions.
4. **The save dialog** (`ingamemenu.md`, section 6) and the autosave at the settings' interval.

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
   (`docs/compatibility.md`, entry 15). Here the hour seeds the generator again from the game's seed and the hour, so
   the seed is all a loaded game needs to go on with the draws it would have made; `count` is the writer's.
5. **What the original works out again**, before the first hour: the provinces' and countries' modifiers, the strategic
   resources, IC, manpower, leadership, every country's effective neutrality, and **a fresh AI for every country** - the
   ministers' first hours, and the strategy, which *Ours* makes again where the original keeps a saved one. With the
   units (section 5): each unit's leader put on, each brigade's figures made from its type and its levels, the queue's
   cost, the officer ratio, and what an intercept watches.
6. **None of a new game's two days**, the filling, the month or the first hour: a loaded game's first hour is the hour
   after its `date` [exe]. Nor are the laws moved, technology bought, the order-of-battle files read, history replayed or
   history revolts carried out.

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
- the four id counters and the `id` blocks, the weather's fronts, the victory conditions and the faction's `progress`,
  the game's flags and its gameplay settings;
- a country's own pools (`home`, `usage`, `to`, `back`, the traded ones), its politics (`organization`, `popularity`,
  `cabinet`, `election`, `last_election`, `duration`, `ministers`), its research queue (`research`), its spies and their
  missions, alignment, war exhaustion, AI (`ai`, `ai_hard_strategy`, `ai_event_strategy`), statistics and the rest the
  report lists; a province's `out_of_supply_days` and `capital`, which the day makes again, and its `history`.

## 5. What the second increment reads: the units

From `FINDINGS-savegame.md`, section C, [exe] but where marked.

**Ids.** A unit's, a brigade's and a line's id is a pair (section 3). What the history made is type 41, and its serials
are the numbers a new game here gives them - one count over units, brigades and lines - so they are kept as they are;
the save's top-level `unit` is the next of them. What the player minted in play is type 4713, numbered here after that
counter in the order it was minted, so a unit's place among its country's units, which reinforcement and the shares go
by, is the original's (*Ours*: the numbers themselves are this game's). Any other type - another peer's - goes after
both. A leader's id is his own (type 37), the number his file gives him.

**The tree.** A country's top-level `theatre`, `armygroup`, `army`, `corps`, `division`, `navy` and `air` blocks, each
with everything under it:

| block | keys read |
| --- | --- |
| unit | `id`, `name`, `base`, `leader`, `previous`, `movement_progress`, `path`, `location`, `retreat`, `dig_in`, `attack_delay`, `combat_hours`, `is_prioritized`, `can_reinforce`, `can_upgrade`, `fuel` and `supplies` (what it was given), `start_date` and `end_date` (the order's two hours, `landorders.md` section 5), `disengage`, `possible_retreat`, `track` (shadowed by a patrol), `was_paradropped`, `is_reserve`, `plan`, `target` and `staging_province` (the AI's sealift and an airborne army's staging), `at_sea` (the hours before a fleet starts another battle), the order, every brigade, every unit under it |
| brigade (`regiment`, `ship`, `wing`) | `id`, `name`, `type`, `organisation`, `strength`, `highest`, `experience`, `is_reserve`, `pride`, `current_distance`, every `<technology>={level progress}`, and a carrier's `air` groups |
| order | `province`, `target`, `stance`, `start_date`, `death_date` (the end hour; `1.1.1` none), `return_to_base`, `time`, `priority`; a patrol's and an intercept's `provinces` and a join's `join_fleet` (the unit aimed at), an air order's `provinces` (its area), a sortie's `start` and `current`, a raid's `attack` (its hours to the next try at the convoys - *inferred* from the field it is written from), an escort's `convoy` and second `province`, a support attack's `combat`, an air supply's `supplies` and `fuel` |
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
| `unit_deployment`, `building_deployment` | a finished unit with no location, and a finished building, waiting to be placed |
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
| a country | `convoys` and `escorts` (its pool), `mobilize` (written only while mobilised) |
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

## Ours

- A key nothing reads yet is reported, by its block and how often it was met, where the original drops an unknown key
  without a word (*Stricter* in `docs/compatibility.md`).
- The AI's strategy is made again on a load, where the original keeps the one a save holds; the save's `ai` blocks are
  not read yet.
- With no `--country=`, the save's own country is played; the original's lobby chooses.
- A unit's number here is not the save's pair: what the player minted is numbered after the history's counter, so the
  writer has to turn it back (section 5).
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
- An alliance's and a puppet's `end_date` and a NAP's `balance`, `our_power` and `their_power`: nothing here holds
  them, so they are reported; whether the original ends an alliance or a puppet on its date in play was not read.
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
- **Italy's ceiling of organisation in July 1941 is the officer ratio's times 1.05** on every kind of brigade (44.100
  for a headquarters of 30 at the ratio's cap of 1.4, where ours gives 42.000), so the hour brings 124 brigades down.
  Its modifier for land organisation here is nought, and the event modifiers a save keeps are read; where the 5% comes
  from was not found.
- A faction's members are written in the order they joined; here they are written in `common/countries.txt`'s, the
  order of joining not being held.
- What the sunk ships answer: `total_of_ours_sunk` and `total_sunk_by_us` are counts over `sunk_ships`, but their
  bodies were not read, so they stay unanswered.
- A ship's, a brigade's and a wing's `home` and `builder` are reported throughout the unit tree and among the sunk
  ships.
