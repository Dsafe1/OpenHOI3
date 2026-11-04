# Savegames: the original's `.hoi3`, both ways

A game here is saved in the original's own format and the original's saves load here - the maintainer's choice of
2026-11-04, over a format of our own and over a replay. Read out of the original the same day
(`reversing/findings/FINDINGS-savegame.md`; the value writers `reversing/findings/FINDINGS-save.md`), and set against
the maintainer's 45 saves. `confirmed` unless marked.

**[exe]** marks a rule the original's executable holds, **[mod: ...]** what a file may change.

## 1. The stages

1. **The reading** - done: `FINDINGS-savegame.md`.
2. **The loader** - in increments, each set against the maintainer's saves (section 4). **The first is built**: the
   clock, the generator, the played country, every province and country's figures below, the factions.
3. **The writer** - a save loaded and written straight back set beside the file, block by block.
4. **The save dialog** (`ingamemenu.md`, section 6) and the autosave at the settings' interval.

## 2. What a load is

As the original's [exe], but where *Ours* says otherwise:

1. **The world** is built as for a new game - the map, every database, the defines, the leaders and ministers of the
   files, the events - and its history folded in (`CGameStateLoader.BuildWorld`); the original's objects are its
   startup's too, filled again in place.
2. **The save is read over it**, one key at a time. Every figure the save gives is set; **one it leaves out is set to
   what leaving it out means** - nought, none, the define's pressure - and not left as the history had it. **What is not
   loaded yet is cleared and reported**, never answered from the history (`CLAUDE.md`'s rule on what is not built):
   today the units, the queue, the deployments, the leaders in service, the convoys, the wars and the pacts.
3. **The played country** is the one asked for (`--country=`), else the one the save names. The original never takes
   it from the save - its lobby chooses, by default the save's own (*inferred*) - so the two come to the same.
4. **The generator** is seeded with the save's `seed`, where the original throws `seed` and `count` away
   (`docs/compatibility.md`, entry 15). Here the hour seeds the generator again from the game's seed and the hour, so
   the seed is all a loaded game needs to go on with the draws it would have made; `count` is the writer's.
5. **What the original works out again**, before the first hour: the provinces' and countries' modifiers, the strategic
   resources, IC, manpower, leadership, every country's effective neutrality, and **a fresh AI for every country** - the
   ministers' first hours, and the strategy, which *Ours* makes again where the original keeps a saved one.
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
| province | `owner`, `controller`, every `core`, `manpower`, `leadership`, `revolt_risk`, `points`, `nationalism`, `strategic_resource`, `pool`, `current_producing`, `max_producing`, every building's pair, `weather` (each figure, the pressure its define where none), `flags` |
| country | `capital`, `acting_capital` (the capital where nought), every law group, every post (a minister of the country's by id), `ideology` (`noIdeology` none), `technology` (level and progress), every category's ability, `distribution` and `leadership` (the shares), `manpower`, `officers`, `diplo_influence`, `national_unity`, `dissent`, `neutrality`, `effective_neutrality`, `auto_assign_leaders`, `flags`, `variables`, and every other country's block - `value` the relation, `threat`, `occupation_policy` (none written, `no_occupation`) |

A country's stockpile is its capital's `pool`, so it comes with the provinces.

**Checked**: `OpenHOI3.SaveCheck load <install> <save> [hours]` loads a save, lists every key not loaded yet by its block,
sets the owners, the laws and the technology levels beside the save's and runs the hours given. On 2026-11-04,
`Germany1936_01_07_22.hoi3`: loaded in 1.7 seconds, **owners 14,168 of 14,168, laws 707 of 707, technology levels
28,987 of 28,987**, and 24 hours run on. The list of what is left is the next increments' work:

- **the units** - the whole tree, brigades, orders and missions, carried units, plans; the leaders in service; the
  queue's three kinds of line and the deployments; the templates (`FINDINGS-savegame.md`, section C);
- **the wars** (`active_war`, `previous_war`) and **the pacts** (`diplomacy`), the battles (`combat`), `sunk_ships`;
- the four id counters and the `id` blocks, the weather's fronts, the victory conditions and the faction's `progress`,
  the game's flags and its gameplay settings;
- a country's own pools (`home`, `usage`, `to`, `back`, the traded ones), its politics (`organization`, `popularity`,
  `cabinet`, `election`, `last_election`, `duration`, `ministers`), its research queue (`research`), its spies and their
  missions, its convoys, alignment, war exhaustion, AI (`ai`, `ai_hard_strategy`, `ai_event_strategy`), statistics and
  the rest the report lists; a province's supply figures and `out_of_supply_days`, which the day makes again, and its
  `history`.

## Ours

- A key nothing reads yet is reported, by its block and how often it was met, where the original drops an unknown key
  without a word (*Stricter* in `docs/compatibility.md`).
- The AI's strategy is made again on a load, where the original keeps the one a save holds; the save's `ai` blocks are
  not read yet.
- With no `--country=`, the save's own country is played; the original's lobby chooses.

## Not established

- Whether a loaded game starts paused in the original.
- Lua on the original's load path.
