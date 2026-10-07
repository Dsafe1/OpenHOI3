# Industrial capacity: buildings, the province sum, and the country's total

How the original works out a country's IC, written as rules this project can implement. No
addresses and no offsets. Each rule says how sure it is - `confirmed`, `likely`, `inferred`
in the fact base's vocabulary - or **measured**, meaning counted here against the files of a
Their Finest Hour install on 2026-10-07. Where this project chooses its own behaviour the
paragraph is marked **Ours**.

Sources: `reversing/findings/FINDINGS-ic.md`, `reversing/findings/FINDINGS-distribute.md`,
`reversing/findings/FINDINGS-mapmode.md` (*Building levels*), and `reversing/CLASSES.md` (*buildings.txt, key by key* and
*Modifiers*).

All arithmetic is in thousandths and every division truncates; see `numbers.md`.

## 1. Buildings

`common/buildings.txt` declares the buildings - eleven in Their Finest Hour: air base, naval
base, coastal fort, land fort, anti-air, radar station, underground, nuclear reactor, rocket
test, industry, infrastructure. (measured)

- **A building's own keys are a fixed list**: `cost`, `time`, `capital`, `port`,
  `max_level`, `on_completion`, `completion_size`, `damage_factor`, `onmap`, `visibility`,
  `show_for_province`, `prerequisites`, `not_if_x_exists`, `confirm`, `orientation`,
  `repair`. `confirmed`
- **Any other key in the block is a modifier, and a building has exactly one.** That is
  where `ic = 1` on industry and `infrastructure = 0.1` on infrastructure go: they are not
  fields of the building, they are what one level of it is worth. `confirmed`, and checked
  value for value against a running game.
- `prerequisites` and `not_if_x_exists` are read and then used by nothing. `confirmed` for
  no reader having been found.
- **A province holds a level for every building, as a maximum and a current value**, both
  in thousandths. They differ when the building is damaged. `confirmed`

A province's history file sets levels with the building's key - `industry = 3`,
`infra = 6`, `naval_base = 4` - undated for the state before any date and dated for
changes. (measured: 6929 `infra` lines and 312 `industry` lines in the expansion's province
files.) That a history line *sets* the level rather than adding to it is `inferred` from the
files: 907 province files state a building's level more than once with the same number, and
adding those up would put many of them past the building's maximum of 10. Another 69 repeat
it with different numbers, where the last one stated is taken to stand. (measured)

*Ours:* one level per building for now. Nothing damages a building yet, so the maximum and
the current level cannot differ.

## 2. What a building does to its province

Every province has a modifier: one value for each kind of modifier the game knows, of which
there are 143. A building contributes its one modifier to its province's.

**A building adds its one modifier, times its current level, to its province's.**
`confirmed` - it is the first step of the province's rebuild. An industry level of 3 with
`ic = 1` gives the province an IC of 3. Eight further kinds of thing feed the same modifier,
and `modifiers.md`, section 3, lists them in order; occupation, revolt risk and damage act
on a province's IC through it rather than in the IC calculation itself, which reads nothing
but the two modifier values below.

## 3. The country's IC

Once a day, for every country, and once on entering a game. `confirmed` throughout unless
marked.

**The province sum.** For each of the country's provinces:

    contribution = province IC * (1 + province LOCAL_IC)

and it is added to the running sum **only if it is positive**. *Which* provinces - the ones
the country owns or the ones it controls - is **not established**; the fact base leans
towards controlled. *Ours:* controlled, until it is read.

**The sum does not start at zero.** One game-wide modifier's IC is added to every country's
as a flat amount; `confirmed`. **It is `base_values` in `static_modifiers.txt`, which has
`ic = 5`** - (measured) on 2026-10-07: it is the only static modifier with an `ic` in it,
and once it is added no country in the original's savegame uses more IC than this project
gives it, where 35 did before. So every country has five IC before any province is
counted, including one that holds nothing.

**The base.**

    base = the province sum, truncated to whole IC

This is the first place the fraction is lost, and it is lost *before* the country's own
bonuses are applied - so a country's fractional province IC never reaches its total.

**The total.**

    multiplier = 1 + country GLOBAL_IC + the technology IC bonus
    total      = base * multiplier, truncated to whole IC
    total is never below 1

So IC is a whole number, and every country has at least one - including a country with no
provinces at all. The country's `GLOBAL_IC` comes from its laws, ministers and so on; the
technology bonus is kept apart from it and added beside it.

**Nothing else computes IC.** Everything that spends or shows it reads what this wrote.

## 4. What follows the total, each day

These run in the daily calculation but not in the one made on entering a game - which is
why, in the original, a freshly loaded game shows IC without its lend-lease share or its
shortage cap until the first midnight. `confirmed`

- **What the industry needs.** Per point of total IC, per day: **1 metal, 2 energy, 0.5 rare
  materials**, for every country without exception.
- **Energy into crude oil.** A country turns spare energy into crude oil at a rate of
  `1 + 2 * the technology conversion bonus`, capped at `max(total IC * 0.05, 1)` a day and
  at the energy left once industry has taken its `total IC * 2`.
- **The resource cap.** The IC the stockpile can pay for is the smallest of its metal, half
  its energy and twice its rare materials; where that is below the total, the total is cut
  to it. `production.md`, section 2.
- **Lend-lease.** IC received is added to the total, and remembered separately so that what
  spends IC knows how much of it was lent.

And straight after the IC calculation, each day: `confirmed`, from
`reversing/findings/FINDINGS-distribute.md`.

- **Crude oil into fuel.** A country refines **half a unit of crude oil per IC**, taking
  the IC as a whole number and halving it, or all the crude oil it has if that is less.
  Each unit makes `(1 + the technology refining bonus) * (1 + FUEL_CONVERSION)` of fuel,
  the two multiplied. It does not refine at all once it holds 99,000 fuel, nor when it
  holds both more fuel than a year of the crude oil coming in and more than twice its
  crude oil. The fuel is made where the factories are, like supplies (`production.md`,
  section 4). (measured: Luxembourg and Honduras, with 4 IC, hold exactly 4 fuel after the
  two days a savegame has behind it.)

*Ours:* the cap is applied each day, before the shares are run. The other three and the
refining are not there yet. A country's total before the cap is its base
times `1 + GLOBAL_IC + the technology bonus`. `GLOBAL_IC` is what its laws and ministers
give it (`modifiers.md`, section 5), and the technology bonus is `ic_modifier` summed over
its technology levels (`resources.md`, section 4). **The original does not save IC**, so
the total cannot be checked directly; what it saves is what industry used, which is a
lower bound, and every country clears it.

## What is not established, in one place

- whether the province sum walks the **owned or the controlled** provinces;
- what the **energy into crude oil** conversion costs: the savegame shows a small country
  losing 10 energy a day for one crude oil, which the rate above does not explain;
- whether a history line **sets or adds to** a building level.
