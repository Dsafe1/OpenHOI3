# Event and decision script

How the original reads its events and decisions: which files, the words they are cut into, an
event's, an option's and a decision's keys, and the two languages inside them - triggers, which
ask, and effects, which do. **This is the reading only**: what a trigger answers and what an effect
does are the next two pieces, and when an event fires and how a decision is taken the one after.

Sources: `reversing/findings/FINDINGS-eventfiles.md` (the files and their containers),
`reversing/findings/FINDINGS-triggergrammar.md` (triggers), `reversing/findings/FINDINGS-effectgrammar.md`
(effects), all read 2026-10-12, with `reversing/findings/FINDINGS-script.md` for the keyword lists.
Nothing here was tried in a running game.

**The rule this follows is the project's: be stricter than the original.** The original drops most
of what it does not understand without a word - an unknown key, a misspelt name, a value of the
wrong shape. Here every one of those is a **problem**, reported with its file and line, and the
entry is read as the original reads it wherever that can be said, or left out where the original's
result is a parse gone astray. Section 9 lists the kinds.

## 1. Which files

The events are every `.txt` in `events/`, the decisions every `.txt` in `decisions/`, layered as
any folder is: **a file of the same name in a higher layer replaces the lower one, and every other
file of every layer is read** - the lower layer's first, each layer's in name order. That is the
original's `extend` mode (`FINDINGS-eventfiles.md`, section 1); how the original maps `tfh/` onto
it was not read, and the layering here is the one every other folder uses.

The original reads events again on every game entry and decisions once. Here both are read when a
game is made, after everything they name.

## 2. Words

A file is cut into words (`FINDINGS-effectgrammar.md`, section 1; `FINDINGS-triggergrammar.md`,
section 4), `confirmed`:

- `#`, `!` and `;` each start a comment that runs to the end of the line;
- `=`, `{`, `}`, `(`, `)` and `,` are words of their own, and each of them, `"`, `#`, `!`, `;` and
  white space ends a word; `<`, `>` and `.` are letters;
- `"..."` is a **quoted** word: it is never one of the language's words, so `"yes"` is not `yes`
  and `"THIS"` is not `this`;
- a bare word is one of the language's words **whatever its case** - `OR`, `Not`, `THIS`, `Yes`.

So keywords and the value words (`yes`, `no`, `this`, `from`, `owner`, `none`, `current`, `random`
and the scope words) are matched without case. **Every name looked up - a building, a
technology, a law, a region, a modifier - is matched exactly**, case and all: `Industry = 3` is
nothing. A country's tag is a case of its own (section 7).

`key = value` and `key = { ... }` are the only shapes. *Ours:* the original does not build a tree
first - a block keyword written with a single value reads its parent's following entries as its
own, a single-valued keyword written with a block hands the block's contents to its parent, and a
key with no `=` loses the word after it and takes the previous entry's value
(`FINDINGS-triggergrammar.md`, section 2). Here braces decide the structure, and each of those is a
problem and the entry is left out.

## 3. An events file

    file := { (country_event | province_event) = { <event> } }*

Both make an event; they differ in the list it goes on, and only a country event fires by its
mean time to happen. **Any other key at the top is a problem** (the original logs it and goes on).
An event is kept even when it lacks a `title`, a `desc` or an option - each a problem.

**An event**, `confirmed`:

| key | value | default | notes |
| --- | --- | --- | --- |
| `id` | a whole number | none | **two events of one id are both kept and the later is the one found by its id** - a problem |
| `title`, `desc`, `subtitle`, `picture` | text | empty | `title` and `desc` are localisation keys; `picture` is `gfx/pictures/events/` + it + `.tga` |
| `is_triggered_only`, `major`, `fire_only_once` | `yes` | no | only the word `yes` is true; anything else is false and a problem but `no` |
| `trigger` | a trigger block (section 5) | empty | a second `trigger` is ANDed with the first |
| `mean_time_to_happen` | section 4 | 1 day | |
| `immediate` | an option (below) | empty | so it takes `name` and `ai_chance` too |
| `option` | an option | none | in file order |
| anything else | | | a problem; the original drops it silently |

**An option** takes `name` (a localisation key) and `ai_chance` (section 4), and **every other key
is an effect** (section 6), in file order.

## 4. Mean time to happen, `ai_chance`, `ai_will_do`

**A mean time to happen**, `confirmed`: `days = N` and **`factor = N`** are N days, `months = N`
is N times 30 and `years = N` N times 365 - **each a whole number, its decimals cut**; the last of
the four written wins; `modifier = { ... }` adds a modifier; anything else is a problem (the original
logs it).

**A modifier** takes `factor`, a number to three places, and **every other key is a trigger**, all
of them ANDed (section 5).

**`ai_chance`** (an option's) is a mean time to happen whose own `factor` is a number with decimals
- the original reads it as a `float`, here it is kept to three places. **`ai_will_do`** (a
decision's) is a plain one, so **`ai_will_do = { factor = 0.75 }` is 0 days** - a problem, since
the decimals are lost; BlackICE writes it 18 times. A `modifier`'s factor inside either is still a
number to three places.

## 5. Triggers

A trigger block is a list of `key = value`, all of which must hold. Each key is, in this order
(`FINDINGS-triggergrammar.md`, section 2):

1. **one of the 153 keywords** (the table below);
2. **a government position** (`head_of_state = 23050`: a minister's id), **a building**
   (`industry = 3`: a level), **a technology** (a level), **a unit type** (a count), **a law
   group** (`conscription_law = service_by_requirement`: the law it has) or **a combined-arms
   group** - in that order, each by its exact name;
3. **an ideology**, written with a block `{ organisation = N popularity = N }`, each a whole
   number: **it holds when either is reached**, and one not written is 0, so a block naming one of
   them always holds;
4. **a region**, written with a block of triggers - a scope over the region;
5. **a scope**: a country's tag (section 7), a province's number, or one of `this` `from` `owner`
   `controller` `ally` `capital_scope` `sea_zone` `local_enemy` `overlord`, written with a block of
   triggers;
6. anything else is a problem. *Ours:* the original logs it only directly inside an event's
   `trigger` or another block the engine made, and drops it **silently** inside any `and`, `or`,
   `not`, scope, region or ideology block - and nothing ever reads it again. Here it is a problem
   wherever it stands.

Two kinds in step 2 are not what they look like, `confirmed`: a law group's law **is not checked
against the group** - a law of another group never matches, a problem here; and **a combined-arms
group is always read as `no`**, whatever is written, so `armor_unit_type = yes` asks for no
armour - a problem. A `this` or `from` given to any of step 2's kinds is read as the number 0.

**Value shapes.** *whole* is a whole number, its decimals cut - `money = 1.5` is a problem;
*number* is a number to three places; *yes* is the word `yes`, anything else false; *tag* is a
country's tag, `this` or `from`; *text* is kept as written; *block* is a block of triggers. A name
that is not found is a problem.

| keyword | value | notes |
| --- | --- | --- |
| `and`, `or`, `not` | block | `not` holds when none of its triggers does |
| `any_core`, `any_neighbor_country`, `any_neighbor_province`, `any_owned_province`, `enemy` | block | scopes |
| `active_mission` | a mission's name | missions are not read here: not checked |
| `ai`, `always`, `can_create_vassals`, `empty`, `frontage_full`, `government_in_exile`, `has_armour_unit`, `has_combined_arms`, `has_empty_adjacent_province`, `has_removable_minister`, `has_strategic_resource`, `is_attacker`, `is_blockaded`, `is_capital`, `is_convoy`, `is_in_any_faction`, `is_mission_country`, `is_mission_province`, `is_subject`, `is_winner`, `port`, `remove_fow`, `reserves`, `war` | yes | `always = no` and `always = 1` are both false |
| `air_battles_fought`, `land_battles_fought` | whole, or `this`/`from` | |
| `capital`, `controls`, `owns` | a province's number; `capital` also `this`/`from` | |
| `alliance_with`, `casus_belli`, `country_units_in_province`, `guarantee`, `has_wargoal`, `is_possible_vassal`, `military_access`, `neighbour`, `non_aggression_pact`, `owned_by`, `tag`, `truce_with`, `undeclared_war_with`, `vassal_of`, `war_with` | tag | `owned_by = owner` is the tag `---`, not the word |
| `base_neutrality`, `blockade`, `crude_oil`, `dissent`, `enemy_ic_ratio`, `energy`, `faction_progress`, `is_threatend`, `leadership`, `lost_IC`, `lost_national`, `manpower`, `manpower_percentage`, `max_manpower`, `max_manpower_greater_than`, `metal`, `money`, `national_unity`, `neutrality`, `num_of_convoys`, `organisation`, `popularity`, `rare_materials`, `revolt_percentage`, `spies`, `strat_allies_impact`, `strat_bomb_impact`, `strat_convoy_impact`, `supplies`, `surrender_progress`, `total_amount_of_planes`, `total_amount_of_ships`, `total_defensives`, `total_ic`, `total_of_ours_sunk`, `total_offensives`, `total_sea_battles`, `total_sunk_by_us`, `total_we_bomb`, `war_exhaustion` | number | |
| `last_air_battle_loser_losses`, `last_air_battle_winner_losses`, `last_battle_loser_losses`, `last_battle_winner_losses`, `last_naval_battle_loser_losses`, `last_naval_battle_winner_losses`, `minister_alive`, `month`, `nationalism`, `naval_battles_fought`, `num_in_faction`, `num_of_allies`, `num_of_ports`, `num_of_revolts`, `num_of_vassals`, `out_of_supply_days`, `province_id`, `province_temperature`, `pure_revolt_risk`, `revolt_risk`, `skill`, `skill_advantage`, `total_amount_of_brigades`, `total_amount_of_divisions`, `total_num_of_ports`, `year` | whole | |
| `num_of_cities`, `units_in_province` | whole, or `this`/`from` | |
| `brigade_exist`, `brigade_in_combat`, `has_combat_modifier`, `has_country_flag`, `has_global_flag`, `has_leader`, `has_province_flag`, `last_mission`, `terrain` | text | |
| `check_variable` | `{ which = text value = number }` | |
| `continent` | a continent, `this` or `from` | |
| `controlled_by` | tag, or **`owner`** - "not occupied" | |
| `date` | `year.month.day`, an hour after it optional | a date that does not read is before the start of time - a problem |
| `exists` | `yes`, `no` or a tag | not `this`/`from` |
| `faction` | a faction, `this` or `none` | a name no faction has is the original's null faction, **which is how `none` means none** - the base game writes `faction = none`; any other such name is a problem, and `from` is looked for as a name |
| `government` | a government | |
| `has_building` | a building | |
| `has_cb` | `{ type = a casus belli actor = tag }` | |
| `has_country_modifier`, `has_province_modifier` | a modifier | |
| `has_hostile_spy_mission` | - | **broken in the original** - it reads a block whatever follows and tests a value nothing set; a problem wherever written, and left out |
| `ideology` | an ideology | |
| `ideology_group` | an ideology group | |
| `is_core` | a province's number, a tag, `this`, `from` or `REB` | |
| `region` | a region | membership; `<region> = { }` is the scope |
| `relation` | `{ who = tag value = whole }` | |
| `spy_mission_count` | `{ type = text value = number }` | |
| `strategic_resource` | a strategic resource or `none` | |
| `threat` | `{ which = tag value = whole }` | |
| `trait` | a trait | |
| `unit_has_leader`, `unit_in_battle` | anything - **the value is not read** | `= no` is not a negation: a problem |

A block keyword's own keys take only the keys shown; any other is a problem (the original drops it).

## 6. Effects

An effect block is a list of `key = value`, done in order. Each key is, in this order
(`FINDINGS-effectgrammar.md`, section 3):

1. **one of the 91 keywords** (the table below);
2. **a government position** (a minister's id, `this` or `from`), **a building** (levels to add, a
   whole number), **an ideology** (`{ organisation = number popularity = number }`), **a
   technology** (a whole number), **a rebel type** (a whole number), **a region** (a block, with
   `limit` and effects), **a unit type** (a province's number, `current`, `this` or `from`) - in
   that order, each by its exact name. **A law is not among them**: `training_laws = x` is a problem;
3. **only where the value is a block, a scope** whose effects run if its key turns out to be a
   country's tag (section 7), a province's number, or one of `owner` `controller` `from` `ally`
   `this` `capital_scope` `sea_zone` `local_enemy` `overlord`; **any other key, given a block, is
   a scope that never runs** - a problem;
4. anything else is a problem.

**`limit` does nothing inside a scope.** It is a key of the iterating effects, of a region and of
nothing else; inside a country's or a province's block it is step 3's scope that never runs, so
**its conditions are not asked and the effects beside it are done anyway**. BlackICE writes it 63
times - `1389 = { limit = { has_global_flag = ... } naval_base = 10 }` builds the base whatever the
flag. Kept so, and a problem.

| keyword | value | notes |
| --- | --- | --- |
| `add_casus_belli`, `casus_belli`, `change_controller`, `create_alliance`, `create_vassal`, `do_election`, `end_guarantee`, `end_military_access`, `end_non_aggression_pact`, `end_war`, `guarantee`, `inherit`, `leave_alliance`, `military_access`, `non_aggression_pact`, `release`, `secede_province`, `surrender_inherit` | tag | |
| `add_core`, `remove_core` | a province's number, a tag, `this`, `from` or `REB` | |
| `capital` | a province's number, `this` or `from` | |
| `change_manpower`, `dissent`, `leadership`, `manpower`, `neutrality`, `officer_pool`, `organisation`, `popularity`, `revolt_risk`, `split_troops`, `war_exhaustion` | number, or `this`/`from` | |
| `create_revolt`, `kill_leader`, `local_intel_boost`, `national_unity`, `remove_fow`, `crude_oil`, `energy`, `fuel`, `metal`, `money`, `rare_materials`, `supplies` | whole, or `this`/`from` | `crude_oil = 2.5` is 2: a problem |
| `country_event`, `province_event` | an event's id | one that no event has is a problem |
| `change_province_name`, `set_country_flag`, `clr_country_flag`, `set_province_flag`, `clr_province_flag`, `set_global_flag`, `clr_global_flag`, `remove_brigade` | text | |
| `load_oob` | a file in `history/units` | one that is not there is a problem |
| `coup_by` | an ideology, `this` or `from` | |
| `fixed_ai_strategy`, `form_government_in_exile` | yes | |
| `government` | a government, `REB`, `this` or `from` | |
| `join_faction`, `leave_faction` | a faction | not `this`/`from` |
| `modify_spies` | anything - **not read** | a problem |
| `release_vassal` | tag or `random` | **an unknown tag is `random`** - a problem |
| `remove_country_modifier`, `remove_province_modifier` | a modifier | |
| `remove_minister` | `yes`, a government position, or a minister's id | |
| `strategic_resource` | a strategic resource or `none` | |
| `any_controlled`, `any_country`, `any_neighbor_country`, `any_neighbor_province`, `any_owned`, `random_country`, `random_owned`, `random_neighbor_province`, `random_empty_neighbor_province` | `{ limit = { triggers } effects }` | several `limit`s are ANDed |
| `any_nearby_province` | the same, and `distance = whole` | |
| `random` | `{ chance = whole effects }` | |
| `random_list` | `{ whole = { effects } ... }` | the weights are whole numbers |
| `add_country_modifier`, `add_province_modifier` | `{ name = a modifier duration = whole }` | days |
| `add_division` | `{ name = text where = a province's number <unit type> = text }` | |
| `add_wargoal` | `{ target = tag war_goal = <goal> effects }` | |
| `war` | `{ target = tag call_ally = yes/no limited = yes attacker_goal = <goal> defender_goal = <goal> effects }` | |
| `undeclared_war` | `{ target = tag region = a region effects }` | |
| `practical` | `{ technology_group = a technology category value = number effects }` | |
| `relation` | `{ who = tag value = number }` | |
| `threat` | `{ who = tag value = whole }` | `which` for `who`, and the tag may be `all`; not `this`/`from` |
| `change_variable`, `set_variable` | `{ which = text value = number }` | |
| `add_ai_strategy` | `CAIStrategy`'s 21 keys | |

A war goal, `<goal>`, is `{ casus_belli = a casus belli actor = tag country = tag receiver = tag
region = a region }`, all of them optional. Where an effect above has `effects` in its block, every
key that is not its own is an effect; whether the original then does them for `practical`, `war`,
`undeclared_war` and `add_wargoal` was not read.

## 7. A country's tag

**The first three letters of the text are the tag** (`FINDINGS-triggergrammar.md`, section 4):
`war_with = GERMANY` is GER, and `GERMANY = { }` a GER scope - a problem here, as a likely slip.
The three are matched **without case only where the sum of the three letters' codes leaves the
same remainder by 64 as the tag's own** - the original looks a tag up in a table of 64 buckets made
from the raw letters and then compares without case. Changing a letter's case moves its code by 32,
so only an even number of changes keeps the bucket: `GER`, `Ger` and `gEr` are GER, and `gER`,
`GEr` and `ger` nothing. Kept exactly. A tag that is nothing is the tag `---` and a problem.

**A tag spelled like one of the original's own words never becomes a scope**: `PHI` and `PAN` are
read as the words `phi` and `pan`, and the original asks for a tag only of a word it does not know.
So `PHI = { ... }` as a trigger is unknown and as an effect never runs, and either is a problem;
`create_vassal = PHI` is the Philippines. Of the base game's and BlackICE's tags only these two
collide; the original's own words are otherwise not in this project, so a mod's new tag spelled
like another of them would not be caught.

## 8. Decisions

    file  := { group = { (<name> = { <decision> })* } }*
    group := diplomatic_decisions | political_decisions

**Any other group abandons the rest of the file**, `confirmed` - a problem, and the rest is not
read. **Every key inside a group is a decision's name**; two of one name are both kept and the
later is the one found by its name - a problem. **Only `diplomatic_decisions` are ever listed,
shown or offered** (`FINDINGS-eventfiles.md`, section 6, `inferred`); `political_decisions` load.

**A decision**: `potential` and `allow`, trigger blocks; `effect`, an effect block; `ai_will_do`,
section 4; `major`, `yes`. Anything else is a problem (the original drops it).

## 9. The problems

Each problem names the file and the line. Their kinds:

- **an unknown key** - in an event, an option's `ai_chance`, a decision, a mean time to happen, a
  trigger block or an effect block, or a block keyword's own keys;
- **a name not found** - a building, a technology, a law, a region, a modifier, an event's id and
  the rest; and **a name found only in another case**, which the original does not find either;
- **a value of the wrong shape** - a block where one value goes, one value where a block goes, a
  number that is not one, decimals a whole number cuts, a word that is not `yes`;
- **the structure** - a key with no `=`, a brace too many or too few;
- **what the original does that the file did not mean** - `limit` inside a scope, a law of another
  group, a combined-arms group, `has_hostile_spy_mission`, `unit_has_leader = no`, a tag read from
  the first three letters of a longer word, `PHI`/`PAN` as a scope, two events of one id.

The game reports their count when a game is made; `OpenHOI3.SaveCheck script <install>` lists them.

## 10. What the base game has

*Measured* on 2026-10-12, with `OpenHOI3.SaveCheck script`: 218 country events, 14 province events
and 74 diplomatic decisions, **no unknown key**, and four problems, each the base game's own:

- `events/NaturalDisasters.txt` line 21, `controls 5383` with no `=` - so the original's `CHI`
  scope asks for the value before it;
- `tfh/events/Belgium.txt` line 15, `units_in_province = GER` - a count, so the original reads 0;
- `tfh/events/surrender2.txt` lines 143 and 144, `country_event = 517` and `= 530` - no file has
  either event.

And since the on_actions are read (2026-10-12, `events.md`, section 8), **seven more, one for each
on_action whose ids are no event**: every id of `on_battle_won`, `_lost`, the naval and air pairs and
`on_encirclement` - 545 in all - because Their Finest Hour replaces the base game's `BattleWon.txt`
and the rest with empty files, so those on_actions find nothing in it, as in the original. Eleven in
all.

What the reading made of them, as a control on the zero: of the conditions, 21 ideologies, 16
regions, 6 government positions, 2 buildings, 2 law groups and 1 technology, each the count a
survey of the files' keys gives; 242 scopes; and of the effects, 508 scopes, 228 regions, 106
government positions, 97 buildings, 86 unit types, 16 ideologies and 5 technologies. They are read
in about 10 ms.

## Not checked here

Missions (`active_mission`), the spy missions' names (`spy_mission_count`'s `type`), a minister's
id, a leader's id (`kill_leader`) and `add_ai_strategy`'s values are taken as written: nothing here
reads the lists they name yet. A casus belli inside `has_cb` is checked against
`common/cb_types.txt`, though the original does not look it up and only compares it when asked - a
name there that no casus belli has can never match.

*Ours:* a modifier with no `factor` is given one; what the original's is was not read.

## Not established

- How the original maps `tfh/` and a mod's `replace_path` onto the folders' layers.
- Whether a decision's `potential` and `allow` and a modifier report an unknown key directly inside
  them as an event's `trigger` does.
- Whether the original's reports of unknown keys and names reach a file at all.
