# Industrial capacity: buildings, the province sum, and the country's total

How the original works out a country's IC, written as rules this project can implement. No
addresses and no offsets. Each rule says how sure it is - `confirmed`, `likely`, `inferred`
in the fact base's vocabulary - or **measured**, meaning counted here against the files of a
Their Finest Hour install on 2026-10-07. Where this project chooses its own behaviour the
paragraph is marked **Ours**.

Sources: `reversing/findings/FINDINGS-ic.md`, `reversing/findings/FINDINGS-distribute.md`,
`reversing/findings/FINDINGS-leadership.md` (section 7),
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

and it is added to the running sum **only if it is positive**. The provinces are the ones
the country **controls**, not the ones it owns. `confirmed`

`LOCAL_IC` is where a province held without a claim loses half its IC: the `non_core`
static modifier has `local_ic = -0.5` (`modifiers.md`, section 3).

**The sum does not start at zero.** One game-wide modifier's IC is added to every country's
as a flat amount, and **it is `base_values` in `static_modifiers.txt`, which has
`ic = 5`**. `confirmed` - first (measured) on 2026-10-07, as the only static modifier with
an `ic` in it, and then read. So every country has five IC before any province is counted,
including one that holds nothing.

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

**A country that holds no province keeps the IC it was given when the game was made, and its
ministers have no part in that figure.** The daily calculation is run only for a country that
holds something or is in exile (`production.md`, section 2) - `confirmed` - so such a
country's IC is never worked out again; that the figure it keeps is its laws' without its
ministers' is **measured** and the reason `inferred`: Guyana, with the law that halves IC and
an armament minister who adds a tenth, uses resources as for an IC of 2 and not 3, and the
Italian Social Republic, with a minister who takes a twentieth off, as for 5 and not 4. With
it, what every one of the 22 countries that do not exist uses in a day agrees with the save.
**It is the IC alone**: leaving such a country's ministers out of everything was tried, and
its money, officers, influence and spies stopped agreeing.

**Nothing else computes IC.** Everything that spends or shows it reads what this wrote.

## 4. What follows the total, each day

**What the industry needs** is worked out in both: per point of total IC, per day, **1 metal, 2
energy, 0.5 rare materials**, for every country without exception, into a pool emptied first -
the total as it stands here, after its floor of one and before the shortage cap and lend-lease
below. A need and not a use: nothing is taken for it, and **the economy ledger's "Used by
Industry" row shows it** beside "Usage", which is what left the stockpile (`statistics.md`,
section 2). Not saved, and made again on entering a game. `confirmed` (corrected 2026-11-15: this
section had it among the steps entering leaves out, but the calculation's test for the daily
steps jumps straight to it; `reversing/findings/FINDINGS-supplypass.md`, section 11). *Measured*
on our first screen, nothing keeping it: Germany 201, 402 and 100.5 at 1936 and 317, 634 and 158.5
at 1941.12.11; Ireland 7, 14 and 3.5 at 1936. Built 2026-11-15 (`CCountry.ResourcesNeeded`,
`CIndustrialCapacity.FillResourcesNeeded`).

These run in the daily calculation but not in the one made on entering a game - which is
why, in the original, a freshly loaded game shows IC without its lend-lease share or its
shortage cap until the first midnight. `confirmed`

- **Energy into crude oil.** A unit of energy makes `0.1 + the technology bonus` units of
  crude oil - the technology's `energy_to_oil_conversion`; the tenth is built into the
  original. Three things limit how much energy is converted in a day, and the smallest
  wins:
  - **what industry can make**: `max(0.05 * total IC, 1)` crude oil, so that many times
    `1 / rate` energy - and never less than one energy a unit;
  - **the balance**: only as much as leaves the country holding exactly twice as much
    energy as crude oil, counting the day's net flow of each. A country with less than
    twice as much energy as crude oil converts nothing;
  - **what industry needs today**: never more than the stockpile's energy less
    `2 * total IC`.

  In thousandths, with every division truncating:

      rate   = 100 + the technology bonus
      wanted = (energy + energyNet - 2 * (crude + crudeNet)) * 1000 / (1000 + 2 * rate)
      energy converted = min( max(50 * IC, 1000) * max(1000000 / rate, 1000) / 1000,
                              wanted,  energy - 2000 * IC )
      crude oil made   = energy converted * rate / 1000

  `confirmed`, from `reversing/findings/FINDINGS-distribute.md`, section 6, which reads it
  through and corrects the earlier summary. (measured: of the 78 countries in the savegame
  that exist, **71 are down by exactly this much energy** over the two days it has behind
  it - Bhutan 10 a day, the United States 38.25, Germany 33.496.)
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
  section 4). (measured: 70 of the 78 have exactly the crude oil the two conversions leave
  them after the savegame's two days.)

Both conversions are for a country that exists. One that holds nothing runs its shares of
IC (`production.md`, section 1) and converts nothing. `confirmed`

*Ours:* both conversions and the cap are applied each day, in that order around each
other - energy into crude oil, the cap, crude oil into fuel - before the shares are run.

- **The day's net flows are the original's fifteen pools as they stand when the conversion
  reads them** (`CDailyLedger`; read 2026-11-15): income is home production - and convoyed in,
  but not for supplies or fuel - what came back, what exiles handed over, debt repaid to it, what
  was made by turning, traded for and came as tribute; expense is what was sent from the capital,
  used, turned into another good, convoyed out and traded away, less debt it repaid and the
  tribute it sent, held below nought. At that moment **the usage holds nothing of either good**:
  `CCountry::ClearDailyPools` zeroes it, with `sent_to`, `sent_back`, the tribute received and the
  exiles' hand-over, at the head of the day before the supply pass, and industry uses nothing
  until the shares run after the conversion; the IC calculation zeroes the conversions' own pools
  as it begins; and today's home production is in, `CollectDailyProduction` running just before.
  So yesterday's industry holds nothing back - until 2026-11-15 it did, this project taking
  yesterday's usage of energy off the balance. The refining's "crude oil coming in" is the same
  income of crude oil. **Measured:** with it, the 1936 start save's crude oil and energy are 77 of
  77 countries the save's, where 69 were (the United States had converted on one day of the two
  where the original converts on both); at the three later starts crude oil is 72, 69 and 69 of
  the countries the save's where it was 67, 66 and 65, and energy 71, 64 and 62 where it was 65,
  61 and 58 (`bookmarks.md`, section 3.1). *Ours:* each other pool
  holds what this project's own day has written by then - convoys, trade, tribute and debts all
  come earlier in it - which the original's clearing of those pools was not read to confirm.
- All the fuel goes to the stockpile, as all the supplies do.
- The original rounds the energy through single-precision floats. Whole thousandths go
  through that unchanged up to about 16,000 units, which the smallest of the three limits
  always is; this project uses integers.
- Industry's need is not kept as a figure of its own, and there is no lend-lease.

A country's total before the cap is its base
times `1 + GLOBAL_IC + the technology bonus`. `GLOBAL_IC` is what its laws and ministers
give it (`modifiers.md`, section 7), and the technology bonus is `ic_modifier` summed over
its technology levels (`resources.md`, section 4). **The original does not save IC**, so
the total cannot be checked directly; what it saves is what industry used, which is a
lower bound, and every country clears it.

## What is not established, in one place

- ~~which of a country's pools hold **yesterday's figures and which today's** at the moment
  the energy conversion reads them~~ - the usage, the conversions' pools and home production
  settled 2026-11-15 (section 4); when convoyed, traded, repaid and tribute-sent goods are
  cleared, not read;
- whether a history line **sets or adds to** a building level.
