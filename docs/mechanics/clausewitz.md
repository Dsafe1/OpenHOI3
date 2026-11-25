# The text format

What every data file of an install and a mod is written in - `common/`, `history/`, `units/`, `technologies/`, `map/`,
the `.gui` and `.gfx` files and a save - and how it is read: `ClausewitzParser` in `lib/OpenHOI3.Content/Clausewitz`.
Events and decisions have a reader of their own (`script.md`). The text is Windows-1252, read byte for byte.

## 1. Tokens and blocks

Words, quoted strings, `=`, `{` and `}`; `#` to the end of the line is a comment. `key = value` is a pair,
`key = { ... }` a block, a block of bare words a list (`color = { 1 2 3 }`), and a block with no key holds its own
entries. A stray `}` at the top of a file is passed over.

## 2. A key with no `=`

The original reads an object's pairs through one routine, `ParseReadKeyValue`, which every object's loader calls before
each key - a save's country, a unit, a province's and a country's history and their dated blocks among them. It takes a
key, then **one token**, and reads a value only when that token is `=`. **Otherwise the token is lost and the value is
whatever the pair before it read** - the parse context's value is not cleared [exe]. Read off the bytes on 2026-11-19
(`reversing/findings/FINDINGS-triggergrammar.md`, section 2, for the routine; `reversing/findings/FINDINGS-history.md`,
section 1, for the history containers that call it per pair).

So, in a block of pairs:

| written | the original reads | here |
| --- | --- | --- |
| `infra = 5` then `manpower 0.66` | `manpower = 5`; 0.66 lost | the same, reported |
| `type = hq_brigade name "15th Corps HQ"` | the brigade named `hq_brigade` | the same, reported |
| `controller = SOV` then `add_core SOV` | `add_core = SOV`, by luck | the same, reported |
| `theatre { ... }` | a theatre: the `{` taken in the `=`'s place, the unit's loader reading to its `}` | the same, reported |
| a word followed by `=` or `}` | the next token lost, then reading out of step | the word passed over, reported |

**An enhanced game's fix `key_without_equals` reads the first three rows as they plainly say** (2026-11-25,
`docs/enhanced.md`): a bare key followed by a word is that key with that word, a quoted one too, reported as before. It
is asked only where a province's history and a unit file are read; a block with no `=` and a word before `=` or `}`
are read as above.

**A block counts as one of pairs when it holds any `key = value` or `key = { }`.** A list of bare words, with no pair in
it, is a list and nothing is said - the original reads lists by another routine. Of the base game's and BlackICE's data
files, the blocks that mix pairs and bare words are all faults (*measured* on the files, 2026-11-19, `tfh` over the
base): Their Finest Hour's Soviet orders of battle of 1938 and 1939 name four HQ brigades `name "..."`, and its Day of
Infamy files open two Japanese theatres with `theatre {` and a Swedish army with `army {` - three units a reader that
took the bare word as nothing would leave out of that bookmark; one `.gfx` file has a stray `;`. Two base province files
write `add_core SOV` and `add_core ENG`, and Their Finest Hour's copies replace both. BlackICE writes `manpower 0.66` in
eight province files - Kiskunhalas and seven in Sardinia - which the original reads as the figure on the line before:
Kiskunhalas manpower 5, Olbia 1.

Every case goes to the validation log (`logs.md`, section 4) at its file and line. A fault of the original, kept:
`docs/enhanced.md` lists reading the value written instead.

*Ours*: the word followed by `=` or `}` - none in either data set - is passed over rather than followed out of step.

## 3. What a reader passes over, said

A key a reader does not know is reported, where the original drops it without a word (`CLAUDE.md`, *Settled
decisions*). Four readers did not, and since 2026-11-19 do - none of their files in Their Finest Hour or BlackICE trips
one today, so each is a net for a mod's mistake:

- **Province history** (`CGameStateLoader.ApplyProvinceHistory`): a key that is none of the province's and no building
  - the original takes it for a building's and finds none - a block other than `revolt`, a `revolt` key other than
  `type`, `size`, `name` and `leader`, and `points` that is no whole number.
- **Country history** (`CCountryHistory`): a key that is no law group, policy, technology, category, position, good or
  key of a country's, a block other than `alignment`, `popularity` and `organization`, and a `capital`, `national_unity`
  or technology level that is no number.
- **War goals** (`CWarReader.ReadGoals`): a key a `war_goal` does not take, an actor or receiver that is no tag, a
  `casus_belli` that names no type and a `region` that names none. A goal whose `is_valid` fails at the date is left
  off silently: that is the original's design, not the file's fault.
- **A file `load_oob` reads** (`units.md`, "A file of units as a game runs"): a brigade of no type or of a type not
  defined, a unit at the top with no location, and one in a province that does not exist - as a new game's reading
  of the same files already said.

And a second pass the same day, over the readers that took only the keys they asked for:

- **`common/countries.txt`**: a line that is no tag and file, and a tag declared again (the first stands).
- **A country's file** under `common/countries`: a key that is none of `color`, `graphical_culture`, `major`,
  `default_templates`, `unit_names`, `ministers`, `last_election` and `duration`; in `ministers`, an entry that is no
  number and block, and a minister's key that is none of his own and no position.
- **Diplomatic history**: an entry that is no block, a key in a pact other than `first`, `second`, `start_date`,
  `end_date` and `value`, and a relation whose value is no number. **Leaders**: a top-level entry that is no number and
  block.
- **`combined_arms.txt`, `unit_upgrades.txt`, `ideologies.txt`, `technology.txt`**: what is outside their one block or
  lists, and what inside it is no group, upgrade, faction key or name.
- **Technologies**: a block that is no unit type, mission, `research_bonus_from`, `decay` or `allow`; a word that is no
  number, flag or key of its own; one of its own figures that is no number; and an entry of `research_bonus_from` or
  `decay` that is no category and amount. *Ours*: a numeric key is still kept as an effect whatever it names - nothing
  asks for one no rule reads, and a list of every effect the simulation asks for is not kept in one place.
- **Buildings**: `max_level`, `damage_factor`, `cost`, `time` and `completion_size` that are no number (read as nought,
  the damage factor as one). **Governments**: a `duration` that is no whole number. **Model files**: an entry that is
  no block, a block inside a model, and a model number or level that is no whole number (each read as nought, as the
  original reads it).
- **Message types**: a category that is none of the list (read as `cat_other`). **On_actions**: one given twice (the
  first stands). **`cb_types.txt`**: a `peace_order` name that is no type - Their Finest Hour's two.
- **`map/terrain.txt`**: a category given twice, a key a category is not read with here - among them the original's
  `attacker`, `defender` and `inland_sea`, none in either data set - a figure that is no number, a colour entry with no
  type or colour, a key a colour entry does not take, and a colour index given twice (the original logs it; the later
  stands). **`map/adjacencies.csv`**: a type other than `river`, `sea`, `impassable` and `canal` (read as plain).
- **Sprites** (`.gfx` `spriteTypes`): an entry that is no kind and block, a kind no sprite is read as, and one with no
  name or no texture.

Measured the same day: the base game's survey gains Their Finest Hour's two `peace_order` names and nothing else, and
BlackICE's not one line - so none of these fires on either's files today. *Not covered*: an `objectTypes` kind no
reader takes - the original's list of kinds was not read, and a kind it knows that is not built here (the unit
figures, `alwaysCounters = no`) is not a file's fault.
