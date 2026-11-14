# Units: what they are made of, and what an order of battle starts them with

What the original makes of the files under `units/` and `history/units`, written as rules
this project can implement. No addresses and no offsets. Each rule says how sure it is -
`confirmed`, `likely`, `inferred` in the fact base's vocabulary - or **measured**, meaning
checked on 2026-10-08 against a savegame the original wrote two days into an unmodded game
started on 1 January 1936. Where this project chooses its own behaviour the paragraph is
marked **Ours**.

Sources: `reversing/findings/FINDINGS-unitstart.md` (what a brigade starts with),
`reversing/findings/FINDINGS-oob.md` (the keys an order of battle accepts, and where
numbers come from), `reversing/findings/FINDINGS-unitdef.md` (what a unit's figures are
made of).

This is the first of the specs on units and covers the first hour only: what there is, and
what it holds before anything has happened. What units draw each day is `upkeep.md`; how
they are built, moved and fought with is not written yet.

All arithmetic is in thousandths and every division truncates; see `numbers.md`.

## 1. Unit types

The original has two words. A **unit** is a thing on the map with a place in the chain of
command: a division, a corps, a fleet, an air group, a headquarters. A **sub unit** is what
a unit is made of: a brigade, a ship or a wing. Only sub units have strength, organisation
and experience, and only sub units have a type.

A type is one block of one file directly under `units/` - `infantry_brigade = { ... }`.
The folder below, `units/models`, is not types (section 5). `confirmed`

- `type` says which service it belongs to: `land`, `naval` or `air`.
- A line whose key is one of the loader's 47 statistics is a number: `max_strength`,
  `default_organisation`, `officers`, `supply_consumption`, `soft_attack` and the rest.
- A line whose key is one of twelve flags is a yes or a no: `active`, `capital`,
  `is_armor`, `is_mobile`, `can_paradrop` and the rest.
- `sprite`, `unit_group` and `on_completion` are names.
- A block named for a terrain, or `river`, `fort`, `night` or `amphibious`, is what that
  circumstance does to the type: any of `attack`, `defence`, `movement`, `attrition`.
- `usable_by` lists the countries that may build it.

What `active`, `is_buildable`, `usable_by`, `unit_group`, `minimum_of_type` and
`max_percentage_of_type` decide - which types a country may order, and how many - is
`builders.md`, section 2; `unit_group` is the combined arms group, section 4 there.

**Ours**: a key that is none of those is reported, where the original drops it.
`available_trigger` is script, and is read with the rest of script onto each type
(`CUnitAvailableTriggerReader`, since 2026-11-14; `builders.md`, section 2).
`extra_amphibious_defence` is read and not kept, because nothing uses it yet.

A type with no `type` line of the three is left out and reported. **Ours** - what the
original does with one was not read.

## 2. A brigade's figures

A technology's file may hold a block named after a unit type:

    smallarms_technology = {
        infantry_brigade = { soft_attack = 0.5 }
        ...
    }

That block is what **one level** of the technology adds to that type, written the way a
type's own figures are, terrain blocks included. `confirmed`

**A brigade keeps a level of its own in every technology that has a block for its type**,
apart from its country's level. Its figures are worked out from them and never adjusted:
`confirmed`

    figures = the type's
    for each technology of the type, in the order they were declared:
        add (that technology's block for the type) x (the brigade's level of it)

Three things about the adding, each of which looks like a slip in the original and is
reproduced: `confirmed`

- **`toughness` is added twice.** A level of a technology worth 0.4 toughness is worth 0.8.
- **`repair_cost_multiplier` is never added.** A technology cannot change it.
- After each technology is added, `build_cost_ic` and `build_cost_manpower` are raised to
  0.1 if they are under it, and `build_time` to 1. That is what stops technologies
  discounting a unit to nothing. It is done once per technology, at whatever level -
  nought included - so a type no technology touches is never held to those floors.

Two flags on a technology matter here:

- **`change = no`** marks a technology that needs no new equipment - the doctrines. Its
  level reaches a brigade without the brigade being upgraded (section 4, and measured in
  section 8).
- A technology with no `additional_offset` has one level (`research.md`, section 1).

## 3. The order of battle

A country's history names one file under `history/units` with `oob = "GER_1936.txt"`.
Where the history has several, the one in force on the start date is the one read.
**It is not read while the history is.** It is read as a game starts, country by country
in the order `countries.txt` declares them, by a country that already has its laws, its
technology levels and its modifiers and already knows whether it is at war. `confirmed`

The history's `officers_ratio` is kept for section 7.

### The shape

Seven keywords declare a unit, and a unit may hold units: `theatre`, `armygroup`, `army`,
`corps`, `division`, `navy`, `air`. Three declare what a unit is made of: `regiment`,
`ship`, `wing`. `confirmed`

    corps = {
        name = "I. Armeekorps"
        location = 1527
        regiment = { type = hq_brigade name = "I. Armeekorps HQ" historical_model = 0 }
        division = {
            name = "1. Infanterie-Division"
            location = 1527
            is_reserve = yes
            regiment = { type = infantry_brigade experience = 45 name = "Infanterie-Regiment 1" historical_model = 1 }
        }
    }

- A unit takes `name`, `location`, `base` (where a fleet or an air group is based),
  `leader` and `is_reserve`. Some thirty more keys are legal - they are what a savegame
  keeps for a unit in the field - and are read without comment and not kept.
- A sub unit takes `type`, `name`, `historical_model`, `experience`, `strength`,
  `organisation`, `highest`, `is_reserve` and `pride`; and a block named after a technology
  of its type, `{ level progress }`, which is how a savegame writes a brigade's levels.
- A `ship` may hold `air` blocks: the air groups aboard a carrier. They are units of the
  country like any other, commanded by no unit.
- A unit at the top of the file with no `location` is read in full and thrown away.
  `confirmed` A `location` that is no province puts the unit in province 1. `confirmed`
- A sub unit whose `type` nobody declares is logged by the original and kept with no
  type. **Ours**: it is left out, and reported.

**Ours**: a key outside those lists is reported. The original does worse than drop it:
its reader takes one word for the value, so an unknown key with a block is followed by the
block's contents being read as keys of the unit around it.

`military_construction` at the top of the file is a unit being built when the game starts:
a line of its country's build queue. It takes `country`, `cost`, `progress`, `duration`,
`size`, `status`, `is_reserve`, `manpower`, `factor`, `name`, and a block named after a
unit type for each brigade, with `name`, `historical_model`, `experience` and - as a
savegame writes it - `model`. What a new game makes of one is `buildqueue.md`, section 5:
its `duration` is the days it still has to go, and its `cost` and `progress` are not used.

### Reserves

`is_reserve = yes` on a unit is copied onto each `regiment` **read after it**. Nothing goes
back over the ones before, and a `wing` does not take it. A regiment's own `is_reserve`
counts too, and the unit's yes outweighs the regiment's no. `confirmed` Every such line in
the base game's starting files comes before its regiments.

### Names

A regiment with no name is named for its place in its unit and the unit's name, as that
name stands when the regiment is read: `3. 1st Division`. `confirmed`, and measured: 790
of the 2912 regiments the base game starts with are named that way, and all agree.

**A ship or a wing with no name is named as its block closes**, while the file is read: its type's
list in the country's file first - for a ship or a wing only, once round from where the list left
off - then `SUBUNIT_NAME` with the first number whose name the country does not use, `$TYPE$` the
type's `_short` text: `1st CAG`. Every name is the country's as it is read, so only a name earlier
in the file can take a number from it. **A unit with no name is named once every file has been
read**, as the new game is entered, in the order it joined the country's list - a carrier's air
group as its block closes, a unit at the top of the file as it is added: its level's pattern with
the first free number, `$TYPE$` its first brigade's - `AIR_NAME`, `1st Air wing` - and never a
list. So a name anywhere in the file can take a group's number, and the two numbers are two
searches over two texts, agreeing while each group holds one wing. Only a new game names; a save
and `load_oob` leave a unit as it is, so an unnamed group `load_oob` brings stays unnamed.
`confirmed` (`reversing/findings/FINDINGS-unitstart.md`, section 6a), and measured: the 24 air
groups aboard carriers and their wings are the save's, 24 of 24 - BlackICE's `AIR_NAME` gives
`1st CAG Air wing`. The ordinal is the one of `builders.md`, section 8, `111st` and all.

### Numbers

Every unit and every sub unit has a number, unique across both. One count runs over every
country in turn, and **each block takes the next number as it closes** - so a unit's
brigades are numbered before it, and a division before the corps it is in. A file need
not give numbers, and none of the base game's do. `confirmed`

**A unit under construction takes five**: one for the line of the queue, one for the unit
it will make, two that are passed over, and one for each of its brigades - which is one,
in all thirty of the base game's. `confirmed`, and (measured) every line, unit and brigade
has the save's number. Of the two passed over, the first is what the finished unit's place
among those waiting to be put on the map is numbered with (`buildqueue.md`, section 6).

A sub unit left out for its type is numbered all the same, and so is a unit thrown away
for having no location. `inferred` from where the number is given.

## 4. What a brigade becomes once its line is read

A brigade is made with **100 strength, 100 organisation** - more than any type has, so
"all there is" - **an experience of -1**, which stands for "not given", and no historical
model. Its line sets what it sets. Then: `confirmed`

    if the line gave a historical_model, and the country has a model of that number:
        for each technology the model lists:
            the brigade's level = the model's                        (section 5)
        for each technology of the type whose change is no:
            if the brigade's level is 0, it becomes the country's level
    work out the brigade's figures                                   (section 2)
    if the line gave a historical_model:
        strength     = the lesser of its strength and the most it may have
        organisation = the lesser of its organisation and its default_organisation
        highest      = strength

- **The model's level is taken whatever the country has reached.** A brigade can start
  with equipment its country has not researched.
- **The most strength it may have** is its `max_strength`, and for a reserve

      max_strength x (1 + reserves_penalty_size)

  unless the country has mobilised. `reserves_penalty_size` is the conscription law's
  modifier and is below nothing: -0.75 for a volunteer army, which leaves a quarter.
- **A country at war as the game starts has mobilised**, and its reserves start whole.
  `inferred` for the rule, and **measured**: it is what makes every reserve in the save
  come out right, sixty of them at full strength.
- **Organisation is held to the type's figure and nothing else**, because no game is
  running yet. Once one is, the ceiling has four more parts: what the army headquarters
  above adds for its leader's skill, the same reserve share, the country's officer ratio,
  and its land, naval or air organisation modifier. `confirmed` They are
  `organisation.md`, section 2, and all but the leader's are built.
- **A brigade whose line names no historical model gets none of this**: every level stays
  at nothing and it keeps its 100 and 100. `confirmed` Every line in the base game names one.

## 5. Historical models

A model is a list of technology levels for one unit type. Every country has a numbered
set of them for every type.

**The defaults.** `HISTORICAL_MODEL_MAX` of them - a define, 10 in Their Finest Hour -
numbered from 0. Model `n` lists every technology of the type: `confirmed`

    at 0   a technology with one level whose allow block holds any condition
    at n   every other

So a default model 2 is "two levels of everything that has levels". `measured`: on the
brigades whose model is a default above 0, 1,650 levels of 1,650 are `n` and 1,322 of
1,322 one-level technologies are 0.

**The files.** Each file under `units/models` belongs to the country whose tag its name
begins with, in either case; a name that begins with no country's tag is skipped. Its
blocks are named `<unit type>.<number>`: `confirmed`

    armor_brigade.3 = {
        tank_gun = 3
        tank_engine = 4
    }

- The block **replaces** that model's list. A technology it does not mention is not at
  the default level; it is not in the model, and the brigade keeps nought of it.
- A number past the models the country has must be exactly the next one, which adds a
  model. Any other is refused.
- A technology with nothing for that type is refused.
- **Ours**: each refusal is reported, as the original logs it.

`measured`: where a country's file has the model, all 3,215 levels on its brigades are
the file's.

**What a brigade is shown as** (read 2026-11-14, `reversing/findings/FINDINGS-unitpaneltooltips.md`, section 13).
`historical_model` gives a brigade its levels and is never asked again: the original keeps it, never writes it to a
save, and no screen reads it. Everywhere a brigade's model shows, it is **the model nearest the levels the brigade
itself carries**: `confirmed`

    score of a model = sum, over the technologies the model lists that change a unit (`change`),
                       of |the brigade's level - the model's| x 1000
    the least wins, from 999,999,000, the first of equals; none for a type with no model

and it is marked `+*` where **the first model the brigade's levels match exactly** is not that one - none matching, or
an earlier one alike: a model matches where it lists every technology the brigade carries, each that changes a unit at
the brigade's level and the rest at any. So a model out of a file that leaves a technology out is never matched
exactly, and is marked wherever it is shown. Whose models: `confirmed`

| where | whose models | the brigade's |
| --- | --- | --- |
| the unit panel's numeral, the reorganise window's | its owner's | Roman numeral of the number plus one, I for none |
| its summary, its card's label, a sunk ship's row | its builder's, else its owner's | name and `+*`; the type's name for none (the summary leaves the line out) |
| its card's picture | its builder's, else its owner's | `<TAG>_<type>_<n>.tga`, `<type>_<n>.tga`, `<type>_0.tga` |

**A builder** is the country a save's `builder` names, or a line's builder where it is not the line's country - a
licence's seller (`buildqueue.md`, section 4): `confirmed`. The original writes `builder` on most brigades, naming the
owner itself on nearly all (3,195 of 3,201 in a save of July 1941, Manchukuo's from Japan the rest); here one is held
where a save names one or a line gives one, and written back where held. Who else sets it was not read.

**A model's name**: the country's text `GER_infantry_brigade_1` - the set's own country, the builder's or the owner's;
for a member of a faction that does not lead it, the faction leader's - Hungary in the Axis taking
`GER_infantry_brigade_1`; the game's
`infantry_brigade_1`; else the type's name and ` (` the number plus one in Roman numerals `)`. `confirmed` for the
order, read 2026-11-14; the leader is the faction's first member.

So **a save loaded is shown as it was**, its levels being in it: in `Germany1936_04_20_03.hoi3` 2,413 brigades show as
I, 1,415 as II, 247 as III and 8 as IV, 321 of them marked, where all had shown as I. And a new game's brigades show,
by their levels, as the model their order of battle names: 3,685 of 3,706 on 1 January 1936 (`OpenHOI3.SaveCheck` on the
start save); the 21 others are models whose levels an earlier model has too - Britain's file has no `battleship.0` and
its `battleship.1` lists every technology at nought, as the default model 0 does - and show as the earlier, as the
original's first of equals does.

## 6. Experience

Still as the game starts, once the units exist. For each brigade whose experience is
below nothing - the "not given" it was made with: `confirmed`

    experience = unit_start_experience                                a regular
    experience = unit_start_experience x (1 + reserves_penalty_size)   a reserve
    never more than 100

`unit_start_experience` is the training law's modifier: nothing for `minimal_training`,
10, 15 and 25 for the three above it.

- **Mobilising does not spare a reserve's experience**: this rule has no test for it.
- **An experience the line gives is kept as it is**, reserve or not.

## 7. The officers a country starts with

Straight after its order of battle is read: `confirmed`

    for each of the country's units, at every level:
        n = the officers its own brigades call for
        if the history gave an officers_ratio:  n = n x officers_ratio
        officers += n

The ratio is applied unit by unit, each rounded down. "The officers its own brigades call
for" is the unit's own figure, which is a sum over its brigades - except that on land a
brigade that holds no frontage (`combat_width` of nothing: a headquarters, engineers)
stops counting once it has under 0.1 strength or under 1 organisation. Ships and wings
always count. (`reversing/findings/FINDINGS-unitdef.md`.)

**That `officers_ratio` is the history key is measured, not read**: the key's place in
the loader was not found. What the save shows is in the next section.

## 8. Against the savegame

`tools/OpenHOI3.SaveCheck` builds the units of all 78 orders of battle and sets them
beside the save's, matched by number:

| | agree |
| --- | --- |
| a brigade of that number, in the unit of that number, of that type | 3706 of 3706 |
| its name, where the file or section 3 gives one | 3706 of 3706 |
| whether it is a reserve | 3706 of 3706 |
| the strength it started with (the save's `highest`) | 3706 of 3706 |
| its experience | 3706 of 3706 |
| the level of every technology that takes new equipment | 26648 of 26648 |
| the level of every doctrine, or its country's where that is higher | 10235 of 10235 |
| its strength is no more than it started with | 3706 of 3706 |
| a unit of that number, name and kind | 1579 of 1579 (1555 until the carriers' air groups were named, 2026-11-13) |
| where it is | 1578 of 1579 |
| where a fleet or an air group is based | 244 of 244 |
| which unit commands it, where the file nests it | 994 of 1028 |
| the officers each country started with | 95 of 100 |

What does not agree, and why:

- **The one unit in a different province** is an air group aboard a carrier: the file puts it
  where its fleet is not.
- **The 34 commanders** are units the file nests and the save has under a theatre of the
  original's own making, below.
- **The five countries' officers** were Britain, the United States, France, the Netherlands
  and Poland: right in the whole number and out by a fraction. They are the five whose
  leadership a triggered modifier changes (`leadership.md`), so it was their two days of
  training that was out, not what they started with - and since triggered modifiers were
  built on 2026-10-14 they agree.

What the save has and the files do not:

- **86 theatres, each with one headquarters brigade** at a fifth of its strength, numbered
  after everything the files made. The original's AI makes them as a game starts, and
  moves every unit the files leave at the top under one. That is stage 5.

Three things the comparison had to allow for, each of which says something:

- **A doctrine reaches a brigade within two days.** Section 4 leaves a doctrine at the
  model's level where the model gives one; the save has those brigades at their country's
  higher level. So the doctrine comparison takes the higher of the two.
- **The laws.** A reserve's strength and experience depend on its country's conscription
  and training laws, and by the save the original's AI has changed laws. Each country is
  tried with the save's law and with its history's, for each of the two, and judged with
  the pair its brigades fit best. **Every country fits the conscription law the save
  shows, and 43 fit their history's training law instead of the save's.** So something
  changes conscription before the units are made - for the player's country too: Germany's
  history says `volunteer_army` and its reserves are at the half a `two_year_draft` leaves -
  and training afterwards.

  **The maintainer checked this in the running game on 2026-10-08**: a fresh 1936 game as
  Germany shows `two_year_draft` on the first screen with nothing touched. His reading is
  that it is a bug - the AI should not have touched the country the player plays. It has
  the shape of the AI script's own rule, "the next law up, if it is allowed", applied
  twice to conscription and twice to the economy; it is not Germany's history file,
  though that file does hold the same law in a block dated 1944. How the script comes to
  run before the first hour, and why training did not move with conscription, is not
  established. **Ours, decided by the maintainer the same day: the player's country keeps
  its history's laws**, and its units are made under them - `docs/compatibility.md`, entry
  6. So Germany's reserves starting at a quarter here is meant.
- **Organisation is not where section 4 put it two days in**: 958 brigades are, 2745 are
  below and 3 above. All three groups are accounted for in `organisation.md`, section 7 -
  a change of law held the 2745 to the running game's ceiling, and the 3 had an hour to
  gain in.

## What is not built

- **Most of what a unit does**: it does not move and is not on the map. What it draws
  each day - supplies and fuel, rotation, reinforcement and upgrades - is built, and is
  `upkeep.md`.
- ~~**Leaders.**~~ Built 2026-10-11: `leaders.md`. What a leader does to a unit is not.
- ~~**Units under construction.**~~ Built 2026-10-08: `buildqueue.md`.
- **The AI's theatres**, and its choice of laws before the units are made - for the
  countries it runs. For the player's country the history's laws are the decision
  (section 8), so Germany's reserves start at a quarter where the original's start at a
  half, and that stays.
- **Names for wings and air groups.**

The ceiling on organisation once a game is running, the officer ratio it needs and the
hourly return of organisation were on this list until 2026-10-08. They are built, and are
`organisation.md`.

## What is not established, in one place

- where the history's `officers_ratio` is read. (Where the ratio the game shows is capped
  was on this line and is answered: the define `MAX_OFFICERS`, 1.4, which is why Germany's
  reserves behave as though its history's 1.5 were that. `organisation.md`, section 1.)
- what gives a wing, a ship or an air group its name;
- ~~what the three numbers a construction takes and no savegame shows are spent on, and what
  a construction of several brigades takes;~~ read 2026-10-08, section 3;
- whether a ship takes its fleet's `is_reserve`: the regiment's rule was read and the
  wing's, not the ship's;
- the least strength a brigade may be held to: the original floors the most it may have
  at a figure that is not in the file;
- what a type with no `type` line becomes;
- what changes conscription laws before the units are made.

## A file of units as a game runs

`load_oob` reads a file under `history/units` into the scope's country [exe]: nothing where the name is empty, or where the country owns no land and is not the rebels. Its units are on the map at once at the file's `location`, and one with no location is left out. Numbers follow the game's last. Each brigade takes what the file gives, held to its ceiling as it stands now; **experience the file does not give is not filled in**, and a unit with no `leader` line has no leader, where a new game fills both [exe]. `reversing/findings/FINDINGS-scriptgaps.md`, section 6.

**Ours**: a `military_construction` in such a file is reported and not taken. Neither of the base game's two files has one.

