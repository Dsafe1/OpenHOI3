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
- **Bringing units back to strength takes manpower** (`upkeep.md`, section 4). Building
  them takes most of it and waits for the build queue; the rest of what does is in
  `reversing/findings/FINDINGS-manpower.md`.

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
  **At a later start the war's months are counted from its first dated history entry, not
  from the day the country joined it** (`reversing/findings/FINDINGS-laterstart.md`, section
  5): (measured) at 1941.6.22 the Soviet Union, Romania and Finland, which join World War II
  that day, are each 21 months into it and keep 0.648 of their most - all three the save's,
  under the save's laws.
- The year here is the most divided by `MAX_MANPOWER` again.
- **Whatever manpower a country's history file gives is thrown away**: the pool is set, not
  added to. No history file in Their Finest Hour gives any.
- **The laws that count are the ones the new game brought forward**, not the history's: the
  engine moves every country's laws to the latest each group allows before the country is set
  up (`politics.md`, section 13), and the set-up works the most out again under them. Germany's
  history says volunteer army, which would start it with 469; it starts with 882, on the
  two-year draft. The Lua's changes come after the two days and do not touch the pool
  (`politics.md`, section 12). And **the effects of the decisions its history has taken** are
  in it, which the set-up runs first (`events.md`, section 13): the United States' New Deal.
- **Its triggered modifiers are in it**, tested as the game is entered before the set-up
  (`modifiers.md`, section 5): France's population crisis.
- **A strategic resource does not count yet.** `confirmed`: who has the use of which is
  not worked out when a new game is entered, only when its first day is run
  (`modifiers.md`, section 6). (measured) The Soviet Union and France hold the two
  provinces with black soil, worth 10% more manpower, and the United Kingdom is given
  France's. All three start with a most worked out without it and gain manpower with it
  from the first day.
- **The countries are set up one after another, each working out its most in its turn**, in
  the order `countries.txt` declares them (`likely`: the new game's per-country set-up is one
  loop, `reversing/findings/FINDINGS-savegame.md` A.5; measured). A province is weighed by its
  owner's law (section 1), so **land a country holds of a country set up after it is weighed
  by that country's law as its history left it**, not as its own set-up later brings it
  forward. Nothing at 1936 shows it; at a later start it decides every occupier's most.
  (measured) Japan, set up before China, holds 130 Chinese provinces at 1941.6.22: weighed by
  China's history law, the volunteer army, they give it nothing, and Japan starts with
  558.603 - by the three-year draft China's set-up then gives itself, under Japan's total
  exploitation, they would give it 39.9 a year before technology, and it would start with 773. Under the save's laws and occupation policies, every country's manpower at
  both 1941 starts is the save's this way, 72 of 72 - Germany's, Italy's, Hungary's and
  Bulgaria's among them.
- **A province is weighed by its modifier as the entry first made it, before any country's modifier was made** - so the
  revolt risk its `revolt_risk` static is scaled by (`revolt.md`, section 3) has no country's `global_revolt_risk` in it.
  The most is read off the province's modifier (`reversing/findings/FINDINGS-manpowergain.md`), and the entry makes every
  province's before its countries' turns (`reversing/findings/FINDINGS-laterstart.md`, section 2, steps 3 to 5); that a
  country's modifier holds no revolt risk then is *measured*, not read. Their Finest Hour cannot show it - none of its
  laws gives a global revolt risk, and its statics give less than nothing. BlackICE's `free_press` gives 4, the
  Comintern's 4 and the Axis's 2, and its `revolt_risk` static is -0.07 manpower a point: (measured, 2026-11-19, on a
  BlackICE GitHub start save made without BiceLib's patches but the infrastructure fix) Czechoslovakia, under
  `free_press`, starts with the save's 749.136 this way and with 508.376 with the 4 counted, and so do 36 more countries,
  Italy - at war under the Axis's 2 and `emergency_conscription`'s 5 - among them: 77 of 79 and the player's, where 42 of
  79 were. **Ethiopia does not fit**: its 268.944 is what about 6 points would give, ours 332.638 with none (*not
  established*, below).

## 5. Ours

- **The income is worked out and added once a day**, before leadership, and the most once a
  month and on entering a game - the original's own times.
- ~~**There is no AI**, so a country starts under its history's laws: Germany with 469 here
  where the original gives it 882.~~ Since 2026-11-14 a new game starts the pool at its most
  once the laws are brought forward, the decisions of the history run and the triggered
  modifiers tested, as the original does: 882 for Germany, and every country of the start
  save's 78 of 78 (`OpenHOI3.SaveCheck firstdays`).
- **The set-up's turns** (section 4) are reproduced without running the countries one by
  one: each country's `global_manpower_modifier` is taken before any laws are brought
  forward (`CManpower.BeforeSetUp`), and the most a new game starts with weighs a province
  owned by a country later in the order by that figure (since 2026-11-15).
- **Only reinforcement spends manpower** (`upkeep.md`, section 4). Nothing is built, so
  the pool mostly grows.
- ~~**A government in exile is treated as any other country.** There are none yet.~~ An exile
  is cut to a tenth as it goes (`war.md`, section 14), and a new game's set-up then starts it
  at its most like anyone else, losing for its war where it still owns a province: (measured)
  Norway's 36.318 and France's 57.072 at 1941.6.22, France's population crisis off in exile.

## 6. What the savegame does and does not confirm

`tools/OpenHOI3.SaveCheck` gives each country the laws, the government and the technology
levels the start save has, works out what it started with and two days of income, and
compares that with the save's manpower:

- **all 23 countries that do not exist yet**, to the thousandth;
- **78 of the 78 that do** since 2026-11-14 (76 until then, 75 until 2026-10-14). Every one of
  them has units, so units cost no manpower in a game's first two days. The United Kingdom is
  among them only because it is given France's black soil.

The three that did not fit, each for something this project did not have:

- **The United States** had 254.936 where 1050.564 was worked out. Its history takes the New
  Deal decision, whose effect the new game's set-up runs, and the modifier that gives takes 40%
  off; with it the figure is exact - and is since 2026-11-14 (`events.md`, section 13).
- **France** fits exactly with the triggered modifier `population_crisis`, 30% off - and
  does since triggered modifiers were built on 2026-10-14 (`modifiers.md`, section 5).
- **Brazil** fitted with one minister's 5% left out. It fits since 2026-11-14 as declared: the
  minister is the first type his block gives for the post, not the second (`politics.md`,
  section 7).

The later saves cannot check the income of a country that has units, because by then its
manpower has been spent on them as well. They do check a country without any: East Germany,
above.

## What is not established, in one place

- the original's **rule for a government in exile**: when working out the month's figure,
  and only then, it replaces the whole sum with a tenth of the manpower of a province the
  country is listed as controlling and does not control. Whether that can happen, and
  whether replacing rather than adding is a slip, were not settled;
- two more places the original works out the most, which were not read;
- what a country's modifier holds when the entry first makes the provinces' (section 4): that it holds no revolt risk is
  measured on a BlackICE start save, and Ethiopia's start there, about 6 points' worth below ours, is not explained -
  at war from the start and invaded - nor China Yunnan's 653.332 to our 655.752.
