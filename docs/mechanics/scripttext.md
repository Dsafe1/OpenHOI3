# Conditions and effects as words

How the original writes a block of conditions and a block of effects as text: an event option's
tooltip, the effects of an event's `immediate` block under its description, and a decision's two
tooltips - what taking it does, and what it needs. `triggers.md` is what a condition answers and
`effects.md` what an effect does; **this is what each says of itself**. `events.md`, sections 10
and 12, is where the texts are shown.

Sources: `reversing/findings/FINDINGS-scripttext.md` (every leaf and every block, read 2026-10-12 by
a wave of five), `reversing/findings/FINDINGS-techtext.md` (the condition blocks, the asterisks and
the corrected indentation), `reversing/findings/FINDINGS-effects.md` (an effect's three text slots)
and `reversing/findings/FINDINGS-eventfire.md`, 3.4 and 3.7 (the scopes the tooltips are asked in).
Nothing here was set beside a running game.

## 1. Where the words are asked, and in which scope

| text | made of | the scope |
| --- | --- | --- |
| an event option's tooltip | the option's effects (section 4), at depth 0, trimmed | the seed, a state of chance of the seed, FROM the event's FROM country, **the country the player's**, the province the event's where it has one - **not the event's own country** |
| under an event's description | its `immediate` block's effects, where they come to more than one character: a blank line and `§W` and the text | the event's own scope |
| a decision's `select_button` | its `effect` block | the player's country, a seed of nought, its state of chance set back to the seed first |
| a decision's `info_icon` | its `allow` block (section 3) | the same |

An option's tooltip is asked of the player's country even for an event fired for another: what an
effect names as "the country" is the player's. That is the original's (`events.md`, section 10).

## 2. A key's text

Every word comes out of the localisation by a key, its `$NAME$` places filled by the line. Three
things about it everywhere:

- **A key no table has is shown as the key itself** - `HAS_FULL_FRONTAGE` - colour codes and all. The
  original's effects colour a name by asking for `§Y` + name + `§W` as a key, which shows the name in
  yellow because no table has it. About fifty keys the leaves ask for are in no table of the base
  game; each shows as its own name. **A key of no length is nothing.**
- **A variable nothing fills stays as written** - `$NAME$`.
- **Numbers** are written one of four ways: **whole** (cut toward nought); **cut** to n places
  (`CNumberText.Format`); **trimmed** to the places it needs (`CNumberText.Trimmed`); and as
  **men**, a hundred times the value, whole.

## 3. A block of conditions

The account of an `allow` block, for a country: **each condition writes one line, with a green or red
asterisk** - `(§G*§W)` where it holds and `(§R*§W)` where it does not - **and a block of conditions
writes a header and its conditions a level deeper**. A level is three spaces. The rules are the
technology screen's (`technologyscreen.md`, section 9) with **the corrected indentation the mod's DLL
puts into the original** (`docs/compatibility.md`, entry 10), and extended here to every block:

- **At the top** an `allow` - an implicit "all of" - is a plain list: each condition's line at depth
  0 with its asterisk, no header.
- **A condition's words turn round inside a `not`** (`redWhen`): "Is AI" becomes "Is NOT AI". A `not`
  writes its conditions on the line its parent placed for it, in the turned-round words, with its
  own asterisk where it sits below the top; its conditions' asterisks are red where they *hold*.
  **A block inside a `not` below the top has no asterisk of its own** - the `not`'s stands for it, as
  for a condition there. The corrected arithmetic gives it one, and the line showed two: decided
  on seeing it, 2026-10-12 (`docs/compatibility.md`, entry 10).
- **`and`** below the top: AND_TRIGGER_STARTS + `:` and a break, then each condition a level deeper
  with its asterisk; **`or`**: OR_TRIGGER_STARTS, the same; a block inside a block at its real depth.
- **`any_owned_province`**: ANY_PROVINCE_STARTS + ` (` + the scope country's name + `):`;
  **`any_neighbor_province`**: NEIGHBOR_PROVINCE_STARTS + `:`; **`any_core`**: ANY_CORE_STARTS + `:`;
  **`any_neighbor_country`**: NEIGHBOR_COUNTRY_STARTS + `:`; **a region**: the region's name + `:`.
  Each then its conditions a level deeper **with no asterisks**, asked in **the scope unchanged** - the
  original's: they are asked against the country, not each member. **A region's header sets the
  scope's province to none** for everything written after it.
- **`enemy = { }`** and a mean time's `modifier` write as an `and`.
- **A scope, `GER = { }`, `FROM = { }`, `owner = { }`...**: its name - a tag's country, a province's
  name, or TRIGGER_FROM, TRIGGER_THIS, TRIGGER_OWNER, TRIGGER_CONTROLLER, TRIGGER_CAPITAL,
  TRIGGER_SEA_ZONE, TRIGGER_ALLY, TRIGGER_LOCAL_ENEMY, TRIGGER_OVERLORD, TRIGGER_EMPEROR,
  TRIGGER_REVOLUTION_TARGET - with its asterisk at the top; its conditions are written once at depth 0
  in the scope it names, and then
  - **where that is two lines or more**: `:` and a break, and its conditions again a level deeper,
    with asterisks asked in that scope;
  - **where it is one line**: the line's leading asterisk is taken off (where the line is longer than
    eight characters), its first letter is lowered, and it is put after the name with a space -
    "Germany is at war with §YFrance§W";
  - a scope with no condition leaves its name and a space.
- **Seven conditions of a battle's tactics write nothing**: `is_winner`, `is_convoy`, `has_armour_unit`,
  `has_combined_arms`, `has_combat_modifier`, `terrain`, `province_temperature`.

**Each leaf's line** is `reversing/findings/FINDINGS-scripttext.md`, sections 3 to 5, word for word:
the key for each wording, what fills it, the colours, the number's form and the break. Four shapes
cover most of them:

| shape | line |
| --- | --- |
| a word | T(key) + break |
| a figure | T(key) + `§Y` + the figure + `§W` + break |
| a name | T(key) + `§Y` + the name + `§W` + break - or the name first, then the key |
| a text | T(key) with its `$NAMES$` filled, and nothing after: the table's text ends in its own break |

**Which wording**: the second wherever `redWhen` is set - inside a `not` - and for a yes-or-no
condition **wherever `redWhen` is what was written**, so `war = no` at the top reads "At Peace."
`has_removable_minister` has its two wordings the other way round from the rest, and
`can_create_vassals` and `has_empty_adjacent_province` read only `redWhen`. **No leaf reads the depth.**

## 4. A block of effects

**A block** (the original's base, the option's block itself): I is three spaces a level; **its own
line, where it has one longer than a character, with I before it**; then each effect's block **with I
before it too** - and the effect's block indents its own line again, so **a line at depth d stands
6d spaces in**. Effects of one keyword that follow each other and **name a target** - `add_core`,
`remove_core`, a unit type - are folded onto one line: each but the last gives its target's name and
`§W,§Y `, and the last writes the line with all of them before its own.

| block | what it writes |
| --- | --- |
| a scope, `FROM = { }`... | `(` + the country's name + `): ` - **nothing where the scope is the player's country**, a province or none - with I before it; then its effects in that scope **a level deeper**, each with I before it. **Where they say nothing the whole block is nothing**; where they have no break, the head gets one |
| `any_owned`, `any_controlled`, `any_nearby_province`, `any_neighbor_province` | **no header**: the block's effects at the same depth, **once for each member** that passes its `limit`, in that member's scope |
| `any_country`, `any_neighbor_country` | for each member but the scope's own country: `(` + its name + `): ` and a break + its effects; `any_country` **leaves out countries that own nothing**, which its effect does not |
| `random_country`, `random_owned`, `random_neighbor_province`, `random_empty_neighbor_province` | no member, nothing; else the effects of the one member a draw picks, **the draw moving the tooltip scope's state of chance on**; no header |
| `random` | `§Y` + its chance trimmed + `§W% chance of ` + (` (` and a break where it has more than one effect) + I + its effects at depth 0 + (`)` where more than one) + a break |
| `random_list` | I + RANDOM_LIST_EFFECT ("One of §Y$SIZE$§W options will happen.") - then for each entry RANDOM_EFFECT ("§Y$CHANCE$§W% chance of "), $CHANCE$ its weight x 100 over the total, whole, + its effects at depth 0, or NO_EFFECT where they come to a character or less, + a break |
| a region | its name + `: ` (not indented), a break where its effects come to two lines or more, then its effects a level deeper, **in province none** |

**Each leaf's line** is `FINDINGS-scripttext.md`, sections 1 and 2, word for word. **A government
position given a minister - `head_of_state = 1031` - is the original's `replace_minister`** (one class
makes both): "Edward VIII will become Head of State.", nothing where he is not found, the post is not
one of his, or the scope's country has him in it already. **Effects with no words**: the flags, the variables, the AI's strategies (`add_ai_strategy` and the rest),
`add_division`. **Many lines are nothing where the effect would change nothing** - `create_alliance`
where already allied, `add_core` where the core is had, `war` where at war - and asking that is asking
the game, so **what the simulation does not hold answers as `triggers.md`, section 8, has it**: an
alliance not built is no alliance, and `create_alliance` writes its line.

## 5. Kept as the original writes them

None is mended. The plainest:

1. **A key no table has shows as its name** - about fifty conditions' and effects' keys in the base
   game; `max_manpower` writes its key's name without asking the table at all, the wrong way round,
   with `%` after a count of men.
2. **Words the wrong way round**: `has_removable_minister`; REMOVE_SPIES_EFFECT says "Add"; the two
   ALLOCATED_SPIES keys' English swapped; SPIES_INFILTRATED says "Grk".
3. **Lines with no break**, which run into the next: `has_cb`, `has_removable_minister`, PERFORM_COUP,
   ELECTION_EFFECT, LOCAL_INTEL_BOOST, ADD_RULING_PARTY_ORG_EFFECT, the spies and practical keys,
   REMOVE_FOW, WARGOAL_EFFECT, ADD_UNDECLARED_REGION_EFFECT; and lines with two, which leave a blank:
   `government_in_exile`, `air_battles_fought`, `has_strategic_resource`, the planes and ships counts.
4. **A negative figure keeps its minus inside "Lose"**: "Lose §R-5§W money".
5. **`change_manpower`'s figure is a hundred times its value**; `num_of_cities = THIS` writes a
   country's name where its number goes.
6. **`do_election` and `release` say something only for a country that owns no province**; a
   building's Gain or Lose compares the value with the level working, though the effect adds it;
   `remove_core` writes its line exactly where there is no core to lose.
7. **A country's effect that names a province names province none**, and so do `is_capital`,
   `is_blockaded`, `port`, `region` and the rest asked of a country.
8. **The scope blocks of conditions are asked against the country, not their members**; a region's
   header empties the scope's province for what follows.
9. **`active_mission` writes a mission's raw key**, `practical` a technology category's.

## Ours

- The words of a condition about something the simulation does not hold are written as the
  original writes them, and **its asterisk is red**, as `triggers.md`, section 8, answers it.
- **The indentation of conditions is the corrected one everywhere**, not only in a technology's
  requirements (`docs/compatibility.md`, entry 10, extended 2026-10-12). Effects have no correction
  and keep the original's doubling.
- **An enhanced game's `kill_leader` says what the leader commands and where** (after BiceLib's
  `activateKillLeaderVariables`): `KILL_LEADER_EFFECT` is filled with three places more than `$NAME$`, for a text
  that asks for them - `$UNIT$`, the unit he commands now; `$LOCATION$`, the province it stands in; `$WHERE$`, the
  two ready as ` (unit, province)` in red, a bracket of punctuation and colour alone so that every word stays the
  localisation's, cut at 255 characters. A leader who commands nothing has all three empty, and a unit with no
  place the unit alone in its bracket. A faithful game fills the name alone and leaves the rest as written, as
  the original without the DLL does. No switch: only a text that asks shows anything.
- **An enhanced game's `load_oob` says what its file would do** (`CLoadOobText`, after BiceLib's
  `activateLoadOobDetails`), where the original's shows the bare path. On, in an enhanced game, where the
  localisation has `OPENHOI_LOAD_OOB_UNITS`; every word is the mod's, some twenty keys `OPENHOI_LOAD_OOB_...`. The
  path first, then a line each that has something to say:
  - not found - no such file, or nothing in it - and nothing more;
  - how many units, and the provinces they stand in, the one with the most first and the count beside one with
    several; **four provinces** and then how many more;
  - the brigades, ships and wings, those there are;
  - the lines added to the build queue;
  - the leaders it puts on units, in the file's order, **ten** and then how many more: each by name, the kind of
    unit - a key a kind, `OPENHOI_LOAD_OOB_KIND_CORPS` and its like, so that an article is the mod's - and where he
    is now, in red since that unit is about to lose him; a number that is none of the scope country's leaders is
    said so. Under a leader whose rank is not the one the unit's level takes, a line: that the file makes it so,
    where the game has the fix `oob_leader_ranks` and he is a land leader, and otherwise that it does not fit.
  - **With Alt held both lists are whole**, and a line says so where one was cut.

  The file is counted off its parse by the engine's own reader of unit files and not loaded
  (`COrderOfBattleReader.Summarise`): a unit is a block of one of the seven kinds with a place - its own or, under
  another, that one's - and a unit at the top with no place is left out with all under it, as a load leaves it
  out. BiceLib's scans the characters and counts every block with a `location`. Read once a game for each path.

## Not established

- Where a table's `\n` becomes a break in the original's reader, which a scope's and a region's test
  of "more than one line" hangs on; here it is made one where a table is read.
- How the original treats a `$NAME$` nothing filled when the text is drawn.
- The name the original gives country none and province none. *Ours*: nothing - so an effect in a
  region, which the original writes of province none, names no province, and `add_core` there, whose
  label is that name, writes nothing: Anschluss's ten regions read "Pfunds: Innsbruck: ..." and no
  more.
