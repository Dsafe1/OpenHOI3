# What an effect does

How the original runs the effects of its events and decisions: in what order, in which scope, how a
block picks its members, and what each keyword and name changes. `script.md` is how effects are
read and `triggers.md` how a condition is answered, the scope with it; **this is what an effect
does**. When an event fires and a decision is taken is the next piece.

Sources: `reversing/findings/FINDINGS-effectrun.md` (every effect's run, read 2026-10-12),
`reversing/findings/FINDINGS-effects.md` (the slot map, the context, the flags, the scales, the
first few effects) and `reversing/findings/FINDINGS-effectgrammar.md` (the value forms). Nothing here
was tried in a running game.

**The rule for what the simulation does not hold** is the one conditions follow (`triggers.md`, the
top): an effect that changes something nothing here keeps does nothing and is **reported as
unbuilt**, and nothing stands in for the system that will own it. Section 8 lists them.

## 1. A block of effects

An option's, an `immediate` block's or a decision's effects run **in the order written**, each in
the scope the block was given. An effect's second argument in the original - nought or an option's
number - is read by nothing, so it is not kept.

- **A scope** (`GER = { }`, `owner = { }`, a province's number and the rest of `triggers.md`'s
  section 2) runs its effects in a copy of the scope it names. **One that cannot be made does
  nothing, and the effects after it run** - where the same block as a condition would be false.
- **A key that is no scope**, `limit` the plainest, is a block whose effects never run; **the
  effects beside it run unconditionally** (`script.md`, section 6).
- **Values**: a whole number is cut at reading and kept in thousandths - `money = 2.5` is 2; a number
  is kept to three places. **`this` and `from` are read by almost nothing** (section 7): written as
  the value of any other effect, they are nought.

## 2. Blocks that pick their members

Each takes a `limit`, every one written joined into one `and`; no `limit` is every member. It is
asked in the member's scope - the province or country put in, the rest kept.

| keyword | members, in order | puts in | `limit` | empty |
| --- | --- | --- | --- | --- |
| `any_owned` | the provinces the country owns | the province; the country kept | each, before its effects | nothing |
| `any_controlled` | the provinces it controls | the province | each | nothing |
| `any_country` | **every country declared, those that own nothing included**, but itself | the country; **the province kept** | each - its own country's asked too, then skipped | - |
| `any_neighbor_country` | its neighbours (`triggers.md`, section 6) | the country; the province kept | each | nothing |
| `any_neighbor_province` | the province's neighbours **that have an owner**, in the order of its edges | the province | each | nothing |
| a region's name | the region's provinces in its file's order, **anyone's** | the province | each | nothing |
| `any_nearby_province` | the provinces **controlled by the province's controller** within `distance` - the province itself among them | the province | each, after the distance | nothing |
| `random_country` | every **other country that owns a province** | the country; the province kept | all, before the draw | **no draw** |
| `random_owned` | the provinces the country owns | the province | all, before the draw | no draw |
| `random_neighbor_province` | the neighbours with an owner | the province | all, before the draw | no draw |
| `random_empty_neighbor_province` | the neighbours that are land and have no owner | the province | all, before the draw | no draw |

**`distance`** is in the game's kilometres, four to a map pixel: a province is near where
`16 x (dx^2 + dy^2) <= distance^2`, `dx` and `dy` between the two provinces' centres in pixels, `dx`
taken the short way round the world.

**The draw** of the `random_*` forms is `triggers.md`'s, section 2: two steps of the scope's state,
the product of their outputs, the remainder over the count of those the `limit` let through.

**`random = { chance = N }`** draws the same way, `% 100`, and runs its effects where the roll is
under the whole number N - **advancing the state whether it runs or not**. **`random_list = { 10 = {
} 30 = { } }`** draws over the sum of the weights and runs the first entry whose running sum the
roll does not exceed: the first entry has one chance more than its weight and the last one fewer, and
a first entry weighing nothing still runs on a roll of nought. A sum of nought or less runs nothing.

**Which state a block advances**, which decides what every later roll is:

- `any_owned` and `any_controlled` give **each member a fresh copy of the scope**: a `random`
  inside rolls the same number in every province, and nothing comes back.
- `any_country`, `any_neighbor_country`, `any_neighbor_province`, a region and `any_nearby_province`
  make one copy for all their members: a draw inside one member carries on into the next, and the
  scope outside is not moved.
- the `random_*` forms move the scope outside by their one draw, when there is something to draw
  from; what their effects draw stays in the member's copy.
- a scope block runs its effects in a copy - but naming `ally` or `local_enemy` draws on the scope
  outside (`triggers.md`, section 2).
- `random` and `random_list` draw on the scope they are in.

**A member taken away by its own effects** - `secede_province` inside `any_owned` - does not stop
the walk: the members are fixed before the first runs. *Ours*: the original reads each next member
before running the current one, and a member added at the end during the walk would be visited; here
the set is taken whole at the start.

## 3. Flags and variables

**A flag** is a name a country, a province or the game holds, set or cleared. `set_country_flag`,
`set_province_flag` and `set_global_flag` set it, making it where it is missing; the `clr_` three
clear it and **do nothing for a flag that is not there**. A cleared flag is kept, unset, so
`has_country_flag` answers only for one that is set. **Names are matched without case.** A province
flag in a country's scope is province 0's.

**A variable** is a number a country holds by name, in thousandths, names matched without case.
`set_variable = { which = X value = N }` sets it, `change_variable` adds to it - each making it at
nought where missing - and **neither does anything in a scope with no country**.
`check_variable = { which = X value = N }` holds where X is **at least** N; asking for a variable that
is not there **makes it, at nought**, and answers whether nought is at least N.

## 4. A country's figures and goods

| effect | changes | how |
| --- | --- | --- |
| `dissent` | its dissent | += v, never below nought, no upper bound |
| `national_unity` | its national unity | += v x (1 + its `national_unity_effect`), held between 1 and 100. A value of nought does nothing |
| `manpower` | **the country's** manpower | += v, never below nought |
| `officer_pool` | its officers | += v where v is not nought, never below nought |
| `capital` | **its declared and its acting capital**, both, to province v | no test of who owns it; a province past the map's last is nought. Nothing moves the stockpile: it is the new capital's pool from now |
| `money`, `supplies`, `fuel` | **in a province's scope, that province's pool**; otherwise the country's stockpile | += v, no bound |
| `crude_oil`, `metal`, `energy`, `rare_materials` | **in a province's scope, what the province can yield** (`max_producing`) - not goods; otherwise the stockpile | += v, no bound |
| `<technology> = N` | **one step of research** on it, as though N leadership had been given - at most one level, its effects applied when one is reached | the progress carried as research's own is |
| `practical = { technology_group = G value = N }` | its ability in G, practical or theory | += N, held between nought and `MAX_TECH_ABILITY`; **its effects inside are never run** |
| `<position> = id` | puts the minister of that id in the post, where he was declared for it and the post's holder has another id | no test of whether the post may be changed or whether he holds another |
| `kill_leader = id` | takes the leader of that id out of service where he is the country's, and off the unit he commands, if any | no cost in organisation; he stays among the country's leaders |
| `leave_alliance = X` | ends the alliance between it and X | a faction is not touched |
| `guarantee = X` | it guarantees X, from now; nothing where it already does, and one X gives it is ended - the original leaves it with nothing pointing at it | **none of these six costs influence, moves relation or threat, or raises a message** (`reversing/findings/FINDINGS-effectrun.md`, section 2.2; `agreements.md` for the offers that do) |
| `end_guarantee = X` | ends its guarantee of X | only where it is the guarantor |
| `non_aggression_pact = X` | a pact between it and X, from now, where there is none | |
| `end_non_aggression_pact = X` | ends the pact between them | either side may |
| `military_access = X` | **it may pass through X's land** - one way | the direction *inferred* in the fact base; it is the byte the access offer sets for the asker |
| `end_military_access = X` | it may no longer | |
| `end_war = X` | **X - not the country - and every country X is overlord of leave the war the two are in** | the country stays in; nothing is settled |

**The stockpile** is the pool of the capital (`stockpile.md`, section 1). Every value is whole but
`dissent`, `manpower`, `officer_pool` and `practical`'s.

*Ours*: a war whose one side is left empty by `end_war` stays in the list with nobody on that side;
what the original's removal does then was not read.

## 5. A province's things

| effect | changes | how |
| --- | --- | --- |
| `change_manpower` | **the province's** manpower | += v, never below nought |
| `leadership` | **the province's** leadership | += v, never below nought |
| `<building> = N` | the level built | += N, and **all of it working** - a bombed building is repaired. No cap at the building's most, no floor. A naval base where the province has no port is refused |
| `strategic_resource = R` | the province's resource | `none` takes it away |
| `change_province_name = "X"` | the province's name, written exactly | - |
| `add_core = X` | a core | **a number: that province becomes a core of the scope's country**; a tag or `this` (by owner): the scope's province becomes a core of that country; `from`: **FROM's province** becomes a core of the scope's country. Nothing for a country that is none or a core already there; added at the end of the province's list |
| `remove_core = X` | the same, taken away | nothing where the core is not there |
| `change_controller = X` | X controls the province | nothing where X already does, or **where the province has no owner** |
| `secede_province = X` | X owns and controls the province | where the scope's country controls it and X owns it, it goes back to X; then, where the scope's country owns it, it goes to X. Anything else: nothing |

None but the building asks whether there is a province: in a country's scope the others change
province 0, the map's empty entry, as the original does - which has no owner, so a change of owner or
controller there does nothing.

**When a province changes owner** the new owner controls it too; **every modifier an event gave it
is taken away**; and the old owner, owning nothing now, leaves its faction. **When it changes
controller** that is all that happens here: what the original's controller setter does besides -
damage to buildings, plunder of a capital, aircraft and ships sent elsewhere, the occupation policy
- waits as it does when a battle takes a province (`landcombat.md`, section 7). *Ours*, of the owner:
the years of nationalism the original sets wait for nationalism.

## 6. Modifiers an event gives

`add_country_modifier = { name = M duration = D }` gives the country `M` for D days - **unless it
already has M, when nothing happens: no second copy, no longer time**. `add_province_modifier` is the
same on the province. `remove_country_modifier = M` and `remove_province_modifier = M` take every
copy away. M is a block of `common/event_modifiers.txt` or `common/static_modifiers.txt`; a name in
neither does nothing.

**A modifier lasts to now plus 24 x D hours. A negative D lasts for ever**: the original keeps the
day of the year and makes it year 1, which the rebuild never takes away. A country's are added at
step 13 of its modifier (`modifiers.md`, section 4), a province's at step 5, and **neither shows until
that modifier is rebuilt**: the country's daily, a province's when it is given one. One whose time
has passed is dropped at the rebuild. `has_country_modifier` and `has_province_modifier` answer
whether it is held, its time not looked at.

*Ours*: where the original drops a province's modifier whose time has passed was not found; here it
is the province's rebuild, as the country's.

## 7. `this` and `from`

Of the effects built, **only `add_core` and `remove_core`** read `this` and `from`, and
`leave_alliance`, `end_war`, `change_controller` and `secede_province` name a country with them -
THIS the root's country or, where it has none, the owner of its province; FROM the root's FROM
country or the owner of its FROM province (`secede_province`'s FROM: the scope's FROM country,
nothing else). **Everywhere else `this` and `from` are nought**: `capital = this` makes province 0 the
capital, `manpower = from` adds nothing.

## 8. What does nothing and is reported

**Unbuilt - what the simulation does not hold yet:**

| waits for | effects |
| --- | --- |
| the event pass | `country_event`, `province_event` |
| diplomacy | `casus_belli`, `add_casus_belli`, `war`, `undeclared_war`, `add_wargoal`, `create_vassal`, `release`, `release_vassal`, `inherit`, `surrender_inherit`, `form_government_in_exile`, `split_troops` |
| factions | `join_faction`, `leave_faction` |
| neutrality and war exhaustion | `neutrality`, `war_exhaustion` |
| the government and the parties | `government`, `coup_by`, `do_election`, `remove_minister`, `organisation`, `popularity`, an ideology's block |
| revolts | `revolt_risk`, `create_revolt`, a rebel type's name |
| espionage and intelligence | `remove_fow`, `local_intel_boost`, `modify_spies` |
| the AI | `add_ai_strategy`, `fixed_ai_strategy` |
| units an event gives or takes | `load_oob`, `add_division`, a unit type's name, `remove_brigade` |

**Unread**: `create_alliance`, which runs a part of the alliance action nobody has read.

*Ours*: where the original faults - `strategic_resource` with a name no resource has, `organisation`
on a country with no ruling party - nothing happens, and the first is reported when read.

## Not established

- Whether a province's new name survives a save.
- What the original's war does when `end_war` leaves a side empty.
- Where a province's modifiers expire.
