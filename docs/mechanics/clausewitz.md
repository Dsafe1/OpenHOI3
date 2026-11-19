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
