# What a trigger answers

How the original answers the conditions of its events and decisions: the scope a condition is asked
in, how a block changes it, and what each of the language's keywords and names asks of the game.
`script.md` is how the conditions are read; **this is how they are answered**. When an event is
asked and how a decision is taken are the next piece.

Sources: `reversing/findings/FINDINGS-triggereval.md` (the scope, the composites, `MakeScope`),
`reversing/findings/FINDINGS-scopetriggers.md` (the scope words, the sets, the relations),
`reversing/findings/FINDINGS-numtriggers.md` (the numbers and their scales),
`reversing/findings/FINDINGS-triggerleaves.md` (every other leaf, read 2026-10-12) and
`reversing/findings/FINDINGS-triggergrammar.md` (the names that are not keywords). Nothing here was
tried in a running game.

**The maintainer's word on what the simulation does not hold yet, 2026-10-12**: a condition about
something nothing here keeps - a flag, a government, an ideology, a minister's dates, a spy, a
threat, a battle's history - **answers false and is reported as unbuilt**. It is not answered from
the history files or anything else standing in for the system that will own it: "Taking the
history backed state would just be adding a system that is wrong and would be removed later on
anyways." Section 8 lists them. What the simulation does hold is answered as the original answers
it, its oddities with it.

## 1. The scope

Every condition is asked of a **scope**, which holds:

- **a country**, or none - which the original writes `---` and keeps as country 0;
- **a province**, or none - province 0;
- **a country and a province it came from** - FROM;
- **the scope the question started in** - THIS, the *root*. Every scope made from another keeps
  its root, however deep the blocks go;
- **how many days to look ahead** - read by `date` alone (section 5), nought unless the caller sets
  it;
- **a state of chance**, which `ally` and `local_enemy` draw on (section 2);
- **the side of a battle**, where the question is asked inside one - a tactic's or a trait's.
  Nothing an event or a decision asks has one.

A block that changes the scope makes a **copy** and changes its country, its province or both;
FROM, the root, the look-ahead and the battle are carried unchanged into every block, `FROM = { }`
included. So FROM, written at any depth, names what the root was raised from, and THIS what the
root was.

## 2. A block that names a scope

A key that is a country's tag, a province's number or one of nine words is a block whose
conditions are asked in another scope. Its conditions are an `and` (section 3) - **after the scope
is made, and false if it cannot be**:

| written | country | province | false when |
| --- | --- | --- | --- |
| a tag | that country | none | - |
| a province's number | none | that province | - |
| `owner` | the province's owner | none | the province has no owner |
| `controller` | the province's controller | none | the province has no controller |
| `from` | FROM's country | FROM's province | - |
| `this` | the root's country | the root's province | - |
| `ally` | one of the country's allies, drawn by chance | **kept** | it has none |
| `local_enemy` | the owner, drawn by chance, of a unit in the province that is fighting and is not the province owner's | **kept** | there is none |
| `capital_scope` | none | the country's capital | - |
| `sea_zone` | none | a sea province of the province's | **always, here** (section 8) |
| `overlord` | the country's overlord, or none | none | - |

So `ally`, `local_enemy` and a set of countries (section 3) keep the province they were entered
with, and a province condition inside them is still about it; and `capital_scope`, `sea_zone` and a
province's number throw the country away, so a country condition inside them is about **none**
(section 4) - `123 = { war = yes }` does not ask about the province's owner.

**A country's allies** are every country it has an alliance with that owns a province, in the
order the countries were declared, and then every other member of its faction that is not on the
list already. **The draw** takes two steps of the original's generator on the scope's state of
chance - MSVC's `rand`: the state times 214013 plus 2531011, kept to 32 bits - multiplies the two
steps' fifteen-bit outputs (bits 16 to 30 of each), and takes the entry that product leaves over
the count, counted from the first (`reversing/findings/FINDINGS-effectrun.md`, section 3.2). It
**advances the state of the scope it was asked in**, not a copy: two `ally` blocks in one condition
draw twice. The units of `local_enemy` are taken in the order the province lists them. The state
of chance an event's scope starts with is the caller's.

## 3. Blocks of conditions

| written | answers | with none inside |
| --- | --- | --- |
| `and` | every one holds | **true** |
| `or` | one holds | **false** |
| `not` | **none** holds - a NOR, which is plain negation only because the scripts write one | **true** |
| a scope (section 2) | the scope can be made, and every one holds in it | **true**, if the scope can be made |
| `any_owned_province` | every one holds in the scope of some province the country owns, its country kept | **false** |
| `any_neighbor_province` | every one holds in the scope of some neighbour of the province **that has an owner** - never a sea province, never empty land | **false** |
| `any_neighbor_country` | every one holds in the scope of some neighbouring country (section 6), **the province kept** | **false** |
| `any_core` | every one holds in the scope of some country with a core on **the province**, the province kept - a province's block over countries, false in a country's scope | **false** |
| a region's name | every one holds in the scope of some province of the region, its country kept | **false** |
| `enemy` | every one holds in the scope of the other side of the battle | **true**; **false where there is no battle** |

Each stops at the first answer that settles it. The sets go in the order the game keeps them in: a
country's provinces by number here, a province's neighbours in the order of its edges, its cores in
the order its history gave them, a region's provinces in the file's order. Only `ally` and
`local_enemy` change anything when asked, so the order shows nowhere else.

## 4. What a missing part reads

**Only `faction` asks whether the scope has a country.** Every other country condition asks its
question of country 0, and every province condition not listed as guarded asks of province 0. So
inside a province's number, a country condition reads the placeholder and not the province's owner;
inside a tag, a province condition reads province 0.

Here there is no placeholder country: **a country condition asked of none is answered as for a
country with nothing** - no provinces, no units, no faction, no laws, no wars, no allies, every
figure nought. Province 0 is the map's own empty entry, which has no owner. Where the original
compares a tag with the scope's country by number, none matches none: `owns = 123` in a province's
scope asks whether 123 has no owner.

*Ours:* what the original's placeholder country holds was not read (`FINDINGS-triggerleaves.md`,
section 2.9); a country of defaults is the reading's own guess.

## 5. The leaves that answer

*Us* is the scope's country, *here* its province. **A number** is written to three places and
compared in thousandths; **a whole number** has its decimals cut at reading, so `= 2.9` is 2;
**whole x 1000** is a whole number compared with a figure kept in thousandths. Unless the table says
otherwise a comparison is `>=`, and a yes-or-no leaf compares its answer with what was written -
only `yes` is yes (`script.md`, section 5).

### THIS and FROM

A leaf that takes a country reads `this` and `from` in one of four ways:

- **by owner**: THIS is the root's country, or **the owner of the root's province** where the root
  has no country; FROM is the root's FROM country;
- **direct**: THIS is the root's country, with no owner to fall back on;
- **ignored**: the original sets its flags and never reads them, so `this` and `from` ask about none
  - and where the value is a number, about nought;
- **honoured** where a leaf says so.

### The clock

| leaf | answers |
| --- | --- |
| `date` | **the hour now, plus 24 for each day of the scope's look-ahead**, is at or after the date and hour written. A date that does not read is before time began: always true |
| `year` | the year is at least N |
| `month` | the month **counted from nought** - January is 0 - is at least N. So `month = 5` holds from June to December, and `year = 1940 month = 5` is false again in January 1941 |
| `always` | `= yes` is true, anything else false |

### The country

| leaf | answers | notes |
| --- | --- | --- |
| `ai` | nobody plays us | `ai = no`: a person plays us |
| `exists = TAG` | that country owns a province | the written yes or no is not read |
| `exists = yes/no` | we own a province | the only way to ask whether a country is gone |
| `tag = X`, `X = { }` as a condition | we are X | THIS and FROM read from the root, direct |
| `capital = N` | our **declared** capital is province N | `==`; `this`/`from` ask for province 0 |
| `num_of_cities` | how many provinces we **own**, at least N | **honoured**: with `this`/`from` the bar is that country's count - THIS by owner, FROM the scope's |
| `owns = N` | province N's owner is us | `this`/`from` ask about province 0 |
| `controls = N` | province N's controller is us | the province comes from the value, so a province scope around it does not matter |
| `total_ic` | our IC, in whole IC | the value is cut to whole IC first: `= 99.9` is 99 |
| `dissent`, `national_unity`, `manpower`, `max_manpower` | the figure, a number | |
| `lost_national` | the share of the country's owned cores it does not hold, made at the head of its day - a core somebody else owns counts in neither, and with none owned it is nought (built 2026-10-16, `FINDINGS-effectrun.md` 1.12) | a share, `0.3` |
| `max_manpower_greater_than` | **our manpower**, not our ceiling | one body with `manpower` |
| `manpower_percentage` | manpower over the ceiling, **a share from 0 to 1**: `= 0.5` is half, `= 50` never | *ours*: false for a ceiling of nought, where the original divides by it |
| `money`, `energy`, `metal`, `rare_materials`, `crude_oil`, `supplies` | what lies in the pool of **our capital**, a number | there is no `fuel` |
| `num_of_convoys` | our convoys, at least the whole number | |
| `total_amount_of_divisions` | our land units **of more than one brigade** - a corps of one headquarters is none | |
| `total_amount_of_brigades` | the brigades of our land units **but their headquarters brigades** | |
| `total_amount_of_ships`, `total_amount_of_planes` | our ships, our wings - not fleets, not air units | the value cut to a whole number |
| `total_of_ours_sunk` | the ships on the game's list of those that left it whose owner is ours - **sunk, finished by bombs, disbanded or removed with a fleet alike** (built 2026-11-16, `FINDINGS-triggerleaves.md` section 1's table, `FINDINGS-savedbattle.md` B.4) | the value cut to a whole number; with no country in the scope, the ships of nobody |
| `total_sunk_by_us` | the ships on that list whose sinker can still be found and is **ours now**: a brigade, ship or wing of one of our units, or a ship of ours on the list itself (*inferred*: the list's ships stay registered) - a sinker gone from the game, a wing disbanded, is nobody's, so the figure can fall | as above |
| `has_leader` | a leader **in service** has exactly that name | case and all |
| `brigade_exist` | a brigade of ours has exactly that name | case and all |
| `brigade_in_combat` | the unit holding the **first** brigade of that name is fighting | the others of that name are not asked |
| `<position> = id` | the minister in that post has that id | false for an empty post |
| `<law group> = law` | our law in **that group** is that law | a law of another group is never ours there |
| `<technology> = N` | our level of it | |
| `<unit type> = N` | **only that N is nought or less** | the original compares the type with each brigade's own copy of it, which is never the same object (`FINDINGS-triggerleaves.md`, section 1.3) |

### Countries together

The countries we are at war with are those on the other side of a war we are on; *at war* is
having one.

| leaf | answers | THIS/FROM |
| --- | --- | --- |
| `war` | we are at war | - |
| `war_with = X` | X is not us, and we are at war with X - **or either of us is `REB`**, so `war_with = REB` holds for every country but the rebels | by owner |
| `enemy_ic_ratio` | **at most**, not at least: the IC we would have with resources enough, over the sum of our enemies', is at most the value, a share. False when we are not at war or **any enemy has none** | - |
| `alliance_with = X` | we have an alliance with X - a faction is not asked | by owner |
| `guarantee = X` | we guarantee X | by owner |
| `non_aggression_pact = X` | there is a pact between us and X | by owner |
| `military_access = X` | **we may pass through X's land** - the fact base's "gives X access" read the byte the other way round (`agreements.md`, section 5) | by owner |
| `is_subject` | we have an overlord | - |
| `vassal_of = X` | X is our overlord | by owner |
| `num_of_vassals` | how many countries we are the overlord of, at least N | ignored |
| `num_of_allies` | how many allies we have (section 2), at least N | ignored |
| `is_in_any_faction` | we are in a faction | - |
| `num_in_faction` | how many members our faction has, nought for none, at least N | ignored |
| `faction = name` | **we are a country** and in that faction. A name no faction has, `none` and `from` are never true | `faction = this`: THIS, by owner, shares a faction with us |
| `neighbour = X` | **we are X's neighbour** (section 6) - the question is asked of X's list | direct |
| `can_create_vassals` | we could release a nation (section 6) | the value is not read: `= no` asks the same |
| `is_possible_vassal = X` | we could release X | ignored |

### The province

| leaf | answers | none |
| --- | --- | --- |
| `province_id = N` | here is province N | - |
| `empty` | here has no owner - every sea province | false for `yes` and `no` |
| `port` | here has **a port on the map**, built or not | false for `yes` and `no` |
| `is_capital` | here is **its owner's** declared capital | not guarded: `= no` is true |
| `has_building = B` | the **built** level of B here is above nought - a bombed factory still counts | not guarded |
| `<building> = N` | the built level of it here, in whole levels | not guarded |
| `leadership` | **here's** leadership, not the country's | not guarded |
| `continent = C` | here is on C | `this`/`from`: the continent of that country's **declared capital** - THIS the root's country, direct; FROM the scope's. A continent no file names is the sea's |
| `region = R` | here is in R | false |
| `strategic_resource = R` | here's strategic resource is R; `none` asks for none | not guarded |
| `has_strategic_resource` | here has a strategic resource | the value is not read |
| `owned_by = X` | here's owner is X. **`owned_by = owner` is a tag no country has** and holds only where there is no owner | **false** |
| `controlled_by = X` | here's controller is X; **`controlled_by = owner`: here is not occupied** | **false** |
| `is_core = X` | here is a core of X; **`is_core = N` asks whether province N is a core of us**; `REB` is the country the scope's rebel faction would free (`rebels.md`, section 10) | false; a province with no cores is false |
| `units_in_province = N` | how many units stand here, anyone's | **honoured, and it changes the question**: with `this` (by owner) or `from` it asks whether that country has a unit here, and N is not read |
| `country_units_in_province = X` | a unit of X stands here | by owner |
| `has_empty_adjacent_province` | a neighbour of here has no owner - so any province beside the sea | the value is not read; false |
| `unit_has_leader` | a unit here has a leader | the value is not read: `= no` asks the same |
| `unit_in_battle` | a unit here **of the province's owner** is fighting | the value is not read |

### 5.1 The war record and `lost_IC`

Built 2026-11-16 from `reversing/findings/FINDINGS-triggerleaves.md`, section 4 [exe]; `CWarRecord`.

| leaf | answers | |
| --- | --- | --- |
| `total_offensives` | the land battles our units are fighting **on ground an enemy holds** - its controller at war with us, or either of us the rebels - each battle once however many of our units fight it | whole: the value cut |
| `total_defensives` | the land battles our units are fighting on any other ground - ours, a friend's, a neutral's - each once | whole |
| `total_sea_battles` | **our fleets in a battle** - one a fleet, not one a battle | whole |
| `total_we_bomb` | **our air units under an order that bombs** - the eight bombing missions and carrier protection, not the raid on convoys from the air - flying or at their base: the order alone is asked | whole |
| `lost_IC` | **the share of our owned provinces' local IC modifier on those we hold - held, not lost** - or, where that whole is nought or less, the held part itself (below) | a number |

**When.** All five are counted afresh, never added to, in the original's rebuild of a country's neighbours: **at the
head of the day, before any country's own** (beside `lost_national`), and **as a game is entered**, a new one or a
loaded one, so a load answers as the save's hour stands. Nothing saves them. Between counts they answer as last
counted: a battle begun or ended in the day is not seen until the next midnight.

**What is walked.** Every unit of ours at every level - headquarters, passengers, a carrier's air groups among them
(the original walks the country's list of all its units; that passengers and carried groups are on it is *inferred*,
the list being the save's tree).
An air unit is asked only its order, and then is done; a fleet only whether it is in a battle; any other unit counts
each battle on its list once for the country, by **the controller of the battle's province** as the count finds it.

**`lost_IC`.** Each province we own weighs its `local_ic` modifier times one and our technology IC bonus
(`ic_modifier`), cut to thousandths; the weights of all are the whole, of those we also hold the part. With the whole
above nought the figure is the part over the whole, in thousandths, cut; otherwise the part. **Two faults of the
original, kept** (`docs/enhanced.md`, *Script*): it keeps the provinces we **hold** where `lost_national` beside it
counts those we do not, and it weighs the **modifier**, not the IC. Their Finest Hour's every `local_ic` is a penalty -
`non_core`, `revolt_risk`, the occupation policies - so the whole is never above nought and the figure is the summed
penalties on the owned provinces we hold: `lost_IC = 0` holds only with none of them, and `lost_IC = 0.1` never.
BlackICE gives a positive `local_ic` too, and there the share can come out.

**Who asks.** Their Finest Hour's own `music/songs.txt` asks the four counts 20 times in its songs' chances, and
BlackICE's music 4 more (`music.md`, section 2.1); the chances see them since this was built.

*Ours:* the original rebuilds a country's neighbours, and these with them, also at each change of hands and in some
thirty diplomatic commands and effects and console commands, and on worker threads; here at the day's head and on
entering only, as `lost_national` already is - so within a day in which a province changed hands, or a war or pact
began, a count can lag the original's until midnight.

### 5.2 The last battles

Built 2026-11-16 from `reversing/findings/FINDINGS-triggerleaves.md`, sections 1.1, 3.1 and 4.3 [exe];
`CLastBattleLosses`, `CBattleConditions`.

`last_battle_winner_losses`, `last_battle_loser_losses`, and the `last_air_` and `last_naval_` pairs ask **one figure
of the whole world each**: what the winner or the loser of the last land, air or sea battle decided **anywhere** lost,
whoever fought it - **strictly more** than the whole number written (the value cut). The scope is not read, so the
leaf answers alike in every country's scope.

**Set** as a battle is decided, **before** its relation change and its on_actions, so `on_battle_won` and
`on_battle_lost` events see their own battle's figures: each side's figure is its `losses` cut to whole numbers - **men
on land** (a brigade's strength lost times a hundred), **strength in the air** (a wing's men per point being one),
**ships at sea** (a ship whose notes passed its strength, `navalcombat.md` section 5) - the loser's the side that lost,
the winner's the other. A bombing, a raid on a convoy and a battle with a side that has no country left write nothing,
and the other kinds' figures stand.

**The original's "lost outright" term is dead.** Its decision adds, for a side whose byte says it lost outright, its
standing strength times a hundred, its ships or its wings - and nothing in the executable sets that byte but the side's
constructor, which zeroes it. So the figure is the losses alone (`docs/enhanced.md`, *Script*).

**The event text** gives the same six always, as whole numbers: `$LAND_LOSER_LOSSES$`, `$LAND_WINNER_LOSSES$`,
`$AIR_LOSER_LOSSES$`, `$AIR_WINNER_LOSSES$`, `$NAVAL_LOSER_LOSSES$`, `$NAVAL_WINNER_LOSSES$` (`events.md`, section 11).

**Not saved, and nought at a new game and after a load** - the original saves them nowhere and never sets them but in
a battle's decision; its load leaves whatever the session's last battle left, even a game's before (`docs/enhanced.md`,
*A whole game's replay*).

## 6. Lists the original keeps

The original rebuilds these once a day and answers from the day's lists; here they are worked out
when asked.

- **A country's neighbours** are the owners of the provinces beside the provinces it owns, where
  that province's owner also controls it - so two countries that touch only across an occupied
  province are not neighbours. `neighbour` asks whether *we* are on *X's* list, and the list is not
  symmetric.
- **A nation a country could release** owns no province, is not the country, has a core on a
  province the country owns and controls **other than its capital**, and has its capital owned by
  the country.
- **A country's allies**, section 2.

*Ours:* whether a country is its own neighbour was not read; here it never is. Lists made when asked
differ from the original's only within a day in which a province changed hands.

## 7. Names that are not keywords

A key that is a government position, a building, a technology, a unit type, a law group, a
combined-arms group, an ideology or a region is asked as section 5 and section 3 say. Two of them
answer for nothing an event or decision has: **a combined-arms group** asks of a battle's side
whether it has none of the group (`script.md`, section 5), and is false where there is no battle;
**an ideology's block** is section 8's.

## 8. What answers false and is reported

Every one of these answers **false** - whatever was written, `= no` included, and inside a `not`
that makes the `not` true - and **is reported** with the key that asked. Each is listed with what it
waits for. They are not answered from the history, by the maintainer's decision at the top.

**And a condition that holds because of one holds**: an event whose `not = { government = x }`
is true only because governments are unbuilt fires like any other. The maintainer's word on
2026-10-12, asked whether such an event may fire: "Just fire them." Of the base game's events,
five held everywhere on the start date for that reason when it was asked, flags then unbuilt
(`OpenHOI3.SaveCheck conditions`).

**Flags, variables and the modifiers an event gives answer** since effects were built the same day:
`has_country_flag`, `has_global_flag`, `has_province_flag`, `check_variable`,
`has_country_modifier` and `has_province_modifier` are `effects.md`'s, sections 3 and 6.

The government, the parties and the ministers - `government`, `ideology`, `ideology_group`, `organisation`,
`popularity`, an ideology's block, `minister_alive`, `has_removable_minister` - and the three strategic-warfare
impacts answer since 2026-10-14: `politics.md`, section 8. `spies` and `spy_mission_count` answer since the same day:
`espionage.md`, section 13. `nationalism` answers since 2026-10-14: `occupation.md`, section 5. `revolt_risk`,
`pure_revolt_risk`, `num_of_revolts` and `revolt_percentage` answer since 2026-10-15: `revolt.md`, section 5.
`truce_with`, `casus_belli`, `has_cb` and `has_wargoal` answer with war's first piece: `war.md`, section 9
(`CRelationConditions`; this table listed them as unbuilt until 2026-11-16). `total_of_ours_sunk` and
`total_sunk_by_us` answer since 2026-11-16: section 5.

**Unbuilt - what the simulation does not hold yet:**

| waits for | conditions |
| --- | --- |
| the fog of war's switch | `remove_fow` |
| the AI's missions | `active_mission`, `last_mission`, `is_mission_country`, `is_mission_province` |

**`lost_IC`, `total_sea_battles`, `total_offensives`, `total_defensives`, `total_we_bomb` and the six `last_*_losses`**
answer since 2026-11-16: sections 5.1 and 5.2.

**`is_blockaded` and `blockade`** answer since 2026-11-14: the scope's province's port blockaded as its controller was
last set, compared with the value and false with no province; and the country's share of its home ports cut off, at
least the figure (`modifiers.md`, section 3, step 8).

**`out_of_supply_days`** answers since 2026-11-14: strictly more days out of supply than its figure, of the scope's
province, false with none (`supply.md`, section 18).

**`num_of_ports` and `total_num_of_ports`** count the provinces the country owns with a port and a naval base built, whoever holds them and however damaged; the first only those in the same block of land as its acting capital [exe; `reversing/findings/FINDINGS-scriptgaps.md`, section 1].

**Unread - the original's answer is not known:** `has_hostile_spy_mission`, whose answer is a byte nothing
writes; and the `sea_zone` scope, whose province is a field nobody has named.

**A battle's - asked where there is none:** `is_attacker`, `frontage_full`, `reserves`, `skill`,
`skill_advantage`, `has_armour_unit`, `has_combined_arms`, `has_combat_modifier`, `is_convoy`,
`is_winner`, `trait`, `terrain` and `province_temperature`. The original reads a battle that is not
there; a tactic's and a trait's conditions are still answered by `landcombat.md` section 11 and
`leaders.md` section 11, which hold their battle.

*Ours:* where the original faults, the condition is false - `has_building` with a building nobody
has (reported when read), and `manpower_percentage` with no ceiling. `ideology_group`'s fault, on a
country with no ruling party, is inside the unbuilt.

## Not established

- What the placeholder country holds (section 4).
- What an empty post in a government holds.
- Whether a country is its own neighbour.
- Where a province event's scope and its FROM come from - the next piece.
