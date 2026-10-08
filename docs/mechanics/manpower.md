# Manpower: what a country gets, and what it starts with

How the original adds to a country's manpower each day and what it gives a country when a
game begins, written as rules this project can implement. No addresses and no offsets. Each
rule says how sure it is - `confirmed`, `likely`, `inferred` in the fact base's vocabulary -
or **measured**, meaning checked on 2026-10-08 against savegames the original wrote with no
mods: one from 1 January 1936 and four later ones of the same game. Where this project
chooses its own behaviour the paragraph is marked **Ours**.

Sources: `reversing/findings/FINDINGS-manpowergain.md`,
`reversing/findings/FINDINGS-countrymodifier.md` (section 4, for strategic resources), and
for what takes manpower away `reversing/findings/FINDINGS-manpower.md`.

All arithmetic is in thousandths and every division truncates; see `numbers.md`.

## 1. What a year brings

`confirmed` throughout.

**The province sum.** For each province the country **controls**:

    part = (province manpower + province LOCAL_MANPOWER)
           * (1 + province LOCAL_MANPOWER_MODIFIER + the owner's GLOBAL_MANPOWER_MODIFIER)

- The bracket on the right is never taken below nothing, and a part that comes out negative
  is not added.
- **The province's percentage and the country's are added together, not multiplied.**
  `overseas` takes three quarters off and a volunteer army half: such a province gives
  nothing, where multiplying would have left an eighth of it. This is not how leadership
  does it (`leadership.md`, section 1), where the country's percentage is applied to the
  whole afterwards.
- **The country's percentage is the province owner's**, though the manpower goes to whoever
  controls it. An occupier gets a province's manpower under the conscription law of the
  country it took it from.
- The province's own manpower is what its history file gives it. The two province values
  are its modifier's (`modifiers.md`, section 3): `overseas` and `non_core` each take 75%
  off, and an occupation policy 65% to 95%.
- `GLOBAL_MANPOWER_MODIFIER` is on the five conscription laws, from -50% for a volunteer
  army to +100% for service by requirement, on one minister type and on one strategic
  resource.

**The year.**

    year = (the province sum + country GLOBAL_MANPOWER) * (1 + the technology bonus)

- **`GLOBAL_MANPOWER` is a flat amount that no law scales.** `base_values` gives every
  country 4, so a country that holds nothing gets four a year whatever its laws.
- **The technology bonus multiplies the whole**, the flat amount included. It is what a
  technology's `manpower_gain` adds up to over the country's levels - one technology in
  Their Finest Hour, a tenth a level. Here leadership differs again: its technology bonus
  is added to the laws'.

(measured) Germany: 59 from its provinces under a two-year draft that changes nothing, four
levels of the technology: (59 + 4) x 1.4 = 88.2 a year.

## 2. What a day adds

    month = year / 12
    today = month / the days this month has
    manpower += today, when that is more than nothing

`confirmed`, and (measured): East Germany, which does not exist until 1945 and holds
nothing, gains 0.010 a day in January and March and 0.011 in February and April - 0.333
over 31, 28 and 30 days.

- Each step drops what does not fit: 4 a year is 0.333 a month and 0.010 a day in January.
- **A day in February is worth more than a day in January**, because the month's figure is
  shared between fewer of them. February always has 28.
- Every country gets it, each day, before its leadership is worked out (`leadership.md`).
- **Nothing takes manpower away here.** What does - building units, bringing them back to
  strength, and so on - is in `reversing/findings/FINDINGS-manpower.md` and waits for units.

A government in exile has a rule of its own in the original that was read and not
understood; see the last section.

## 3. The figure called its most

    most = year * MAX_MANPOWER, and never less than 1

`confirmed`. `MAX_MANPOWER` is a define, 10 in Their Finest Hour: **a country's most is ten
years of what it gets.**

- **It is not a limit.** Nothing holds the pool to it: East Germany starts with exactly its
  most, 40, and has 41.158 by 20 April. (measured, and `confirmed` - the daily addition does
  not look at it.)
- It is what a new game starts a country with (section 4), what the `max_manpower` and
  `manpower_percentage` conditions of the script read, and what the interface shows.
- It is worked out once a month, after the country's modifier has been rebuilt, and on
  entering a game. So a law changed in the middle of a month changes the income the next
  day and this figure when the month turns.

## 4. What a new game starts with

`confirmed`, and (measured) for 98 of the 101 countries in the savegame.

    manpower = most

    and for a country that owns a province and is at war:
    manpower -= year * (1 + 0.12 * months at war), never leaving less than nothing

- **A country at peace starts with ten years of manpower.**
- **A country at war starts a year short, and an eighth of a year more for each month of
  the war.** The months are the ones its starting stockpile counts (`stockpile.md`, section
  2): calendar months since the oldest war it is still in. Italy and Ethiopia, three months
  into theirs, each start with 8.64 years' worth. The 0.12 is built into the original.
- The year here is the most divided by `MAX_MANPOWER` again.
- **Whatever manpower a country's history file gives is thrown away**: the pool is set, not
  added to. No history file in Their Finest Hour gives any.
- **The laws that count are the ones the original's AI has chosen**, not the history's: it
  picks its laws before the country is set up (`resources.md`, section 5). Germany's
  history says volunteer army, which would start it with 469; it starts with 882, on the
  two-year draft its AI took.
- **A strategic resource does not count yet.** `confirmed`: who has the use of which is
  not worked out when a new game is entered, only when its first day is run
  (`modifiers.md`, section 6). (measured) The Soviet Union and France hold the two
  provinces with black soil, worth 10% more manpower, and the United Kingdom is given
  France's. All three start with a most worked out without it and gain manpower with it
  from the first day.

## 5. Ours

- **The income is worked out and added once a day**, before leadership, and the most once a
  month and on entering a game - the original's own times.
- **There is no AI**, so a country starts under its history's laws: Germany with 469 here
  where the original gives it 882.
- **Nothing spends manpower.** The pool only grows until there are units.
- **A government in exile is treated as any other country.** There are none yet.

## 6. What the savegame does and does not confirm

`tools/OpenHOI3.SaveCheck` gives each country the laws, the government and the technology
levels the start save has, works out what it started with and two days of income, and
compares that with the save's manpower:

- **all 23 countries that do not exist yet**, to the thousandth;
- **75 of the 78 that do**. Every one of them has units, so units cost no manpower in a
  game's first two days. The United Kingdom is among them only because it is given
  France's black soil.

The three that do not fit, each for something this project does not have:

- **The United States** has 254.936 where 1050.564 is worked out. Its AI takes the New Deal
  decision before the country is set up, and the modifier that gives takes 40% off; with it
  the figure is exact. Decisions are stage 4.
- **France** fits exactly with the triggered modifier `population_crisis`, 30% off, which
  is also stage 4.
- **Brazil** fits with one minister's 5% left out: its AI appointed him after the last day
  was run, as it does with laws (`production.md`, section 7).

The later saves cannot check the income of a country that has units, because by then its
manpower has been spent on them as well. They do check a country without any: East Germany,
above.

## What is not established, in one place

- the original's **rule for a government in exile**: when working out the month's figure,
  and only then, it replaces the whole sum with a tenth of the manpower of a province the
  country is listed as controlling and does not control. Whether that can happen, and
  whether replacing rather than adding is a slip, were not settled;
- two more places the original works out the most, which were not read.
