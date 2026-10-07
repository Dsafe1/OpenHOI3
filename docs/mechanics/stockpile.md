# The stockpile: where it is, what it starts with, and what a day does to it

How the original keeps a country's goods, written as rules this project can implement. No
addresses and no offsets.

**The starting stockpile was measured, not read.** Nothing in an install's files says what
a country holds on the first day, and the fact base does not have it either. It was worked
out on 2026-10-07 from a savegame the original wrote on 1 January 1936, with no mods, and
is marked **measured** below. Rules from the fact base are marked `confirmed`, `likely` or
`inferred` in its vocabulary, and this project's own choices are marked **Ours**.

Sources: `reversing/CLASSES.md` (*Provinces, goods and supply* and *The country's goods
pools*), `reversing/findings/FINDINGS-ic.md`, `reversing/findings/FINDINGS-production.md`
(*Two budgets, and the resource price of IC* and *Consumer goods*).

All arithmetic is in thousandths and every division truncates; see `numbers.md`. The goods
and their order are in `resources.md`, section 1.

## 1. Where a stockpile is

- **Every province has a pool of its own**: what is lying in that province. `confirmed`
- **A country's stockpile is the pool of its capital province.** It is not a field of the
  country. Whoever's capital a province is, that province's pool is their stockpile.
  `confirmed`
- It follows the **acting** capital, the province the government sits in now, which is not
  the same as the capital once a government has moved. A government in exile is the
  exception and keeps a pool of its own. `confirmed`
- **Two countries can share one stockpile.** East Germany's capital is Berlin and
  Nationalist Spain's is Madrid, so on the start date what those two are given lands in
  Germany's stockpile and Republican Spain's. (measured)

*Ours:* a country has one capital for now, so there is no acting capital and no exile.

## 2. What a stockpile starts with

(measured) Before the first day, every country is given a number of days of what its
industry needs:

    metal          = base IC * days
    energy         = base IC * days * 2
    rare materials = base IC * days * 0.5
    crude oil      = base IC * days
    money          = base IC * days * 0.0025

- **Base IC, not total IC.** It is the province sum in whole IC before any law, minister or
  technology (`ic.md`, section 3), so it includes the five that `base_values` gives every
  country. The United States get 256 x 90 = 23040 metal although their laws leave them 153
  IC to spend.
- **`days` is 90 for a country at peace and 75 for one at war.** Only Italy and Ethiopia
  are at war on the start date, and they are also the only two that are mobilised, so
  which of the two decides it is **not established**. Italy has 69 x 75 = 5175 metal and
  Ethiopia 7 x 75 = 525, both exact.
- **The four resources are added to the pool of the country's capital, whoever holds that
  province.** A country that does not exist yet still has a base IC of five and a capital,
  so 450 metal, 900 energy, 225 rare materials and 450 crude oil are put in a province
  that belongs to someone else. Berlin starts with 149 x 90 metal: Germany's 144 and East
  Germany's five.
- **Money is not added: it is given only where the pool has none**, and the countries are
  taken in the order `countries.txt` declares them. Three cases show it, and no other
  rule tried fits all three:
  - Berlin has Germany's 32.4 and nothing for East Germany, which is declared later.
  - Madrid has 1.125 - what Nationalist Spain's five IC give - and nothing for Republican
    Spain, which holds the city, has 44 IC, and is declared on the next line.
  - The United States, whose history file gives them 1 money, have exactly that 1.
- **What the history file gives comes first.** A country's history file can name a good
  with an amount, and it is added to the stockpile before any of the above. Their Finest
  Hour does it twice on the start date: the United States' `money = 1`, which is why they
  get no other, and Germany's `rare_materials = 40000`, which Berlin has on top of its
  149 x 45.
- **A puppet seems to start with no more than 1000 IC-days.** Manchukuo has a base IC of
  21 and starts with exactly 1000 metal, 500 rare materials and 2.5 money, not 1890, 945
  and 4.725. It is the only puppet whose IC is high enough to show it, so one country is
  all the evidence there is, and whether it is a limit or something else is **not
  established**.
- **Supplies and fuel start with something too, and it does not come from IC.** Countries
  with the same base IC start with very different amounts. It probably follows what their
  units need; **not established**, and nothing is given here until there are units.

Of the 78 stockpiles belonging to a country that exists, metal and rare materials come
out exact - to the thousandth - for 76 and money for 77 (section 6 has the rest). Crude
oil and energy cannot be compared exactly, because the original has already converted
some of one into the other by the time anything can be saved; they agree to within that
conversion.

That a history file's amount is added rather than set is `inferred` from Germany alone.

The money rule has a consequence for mods. BlackICE gives nearly every country
`money = N` in its history file, so under it none of them gets any other money, and each
starts with exactly what the file says. The mod's maintainer, asked on 2026-10-07, thinks
that is what happens in play; it has not been measured.

## 3. Two days have already passed on the first screen

(measured) A savegame made on the start date, at hour 0, with nothing touched, is not the
start. Every stockpile is what section 2 gives plus **exactly two days** of what the
country produced at home less what its industry used, and each province's record of
yesterday's supply throughput is already filled in. So the original runs its daily work
twice before the player sees anything. That is also when its AI changes the laws
(`resources.md`, section 5).

*Ours:* not done. A game here starts from section 2, and the first daily pass is the first
midnight.

## 4. What a day does to it

The original keeps 23 pools on each country that record a day's movements, and its figures
for a day's income, expense and balance are sums over them. `confirmed` The ones whose
part is known:

| goes in | | goes out | |
| --- | --- | --- | --- |
| home production | `resources.md`, section 3 | what industry used | 1 metal, 2 energy, 0.5 rare materials for each IC **spent** |
| brought in by convoy | from the country's own cut-off provinces | sent to the units | supplies and fuel |
| traded for | | traded away | |
| made by conversion | fuel from crude oil, crude oil from energy | used by conversion | crude oil, energy |
| returned from the units | supplies | tribute to an overlord | what a puppet holds over a threshold |

Three things about it that matter now:

- **Home production reaches the stockpile every day.** (measured: for metal and rare
  materials, two days of home production less usage is exactly the difference between the
  saved stockpile and section 2.)
- **Industry is charged for the IC it spends, not for the IC it has**, at the moment each
  thing is paid for. `confirmed` IC left unspent costs nothing. So what a country uses
  cannot be worked out before there is production to spend IC on.
- **Money** comes in once a day:

      money = base IC * IC_TO_MONEY * (1 + GLOBAL_MONEY)

  `IC_TO_MONEY` is in `defines.lua`, 0.05 in Their Finest Hour, and `GLOBAL_MONEY` is the
  country's modifier of that name. It is base IC again, and **the consumer goods slider
  has no part in it**: the fact base read that twice and reports it as unexpected.
  `confirmed` for the formula; (measured) it reproduces the savegame's figure to the
  thousandth for 97 of the 100 countries that have an economy. It is counted as home
  production and added to the stockpile. It is paid as part of the consumer goods share's
  work, and only to a country that has any IC left that day (`production.md`, section 3).

## 5. What this project does now

*Ours*, all of it:

- A province has a pool. A country's stockpile is its capital's.
- A new game fills the stockpiles as section 2 says. Wars are read from `history/wars` for
  the one purpose of knowing who is at war on the start date. **Puppets are not read**, so
  Manchukuo starts with the full 1890 metal.
- Each day a country that holds its capital has what its provinces produced added to its
  stockpile. Then every country's shares of IC are run (`production.md`): money and
  supplies come in, and what consumer goods and supplies cost in metal, energy and rare
  materials goes out.
- **Most of what will be taken out is not taken yet.** The build queue, reinforcement and
  upgrades spend IC on units; the conversions are the rest of IC's daily work (`ic.md`,
  section 4); supplies and fuel are drawn by units. Until those exist a stockpile mostly
  grows.
- **A country that does not exist has an economy all the same**, as in the original: East
  Germany, which does not exist in 1936, earns money, makes supplies and uses IC every
  day, all of it in Berlin's pool. (measured, and `confirmed` since: the daily loop over
  a country's shares does not ask whether it holds anything.)

## 6. What the savegame does and does not confirm

`tools/OpenHOI3.SaveCheck` compares what a new game here starts with against the saved
stockpile with two days of the save's own home production and usage taken off again. It
compares by capital, since two countries can share one, and for a country that does not
exist it takes off one day of home production and two of usage: the original resets such a
country's usage every day and its home production never, so that is what the save holds.
(measured: Berlin and Madrid come out exact only that way.)

**Does not agree, with a reason:**

- *Manchukuo*, in metal, rare materials and money: the puppet's 1000 IC-days of section 2.
- *The United Kingdom* has 454 more metal and 225 more rare materials than its own 161 IC
  gives it, which is one five-IC country's worth and a little over. What a country that
  does not exist was given, in a capital Britain holds overseas, has reached London by
  the second day; which one, and how, was not followed.

**Does not agree, and is not explained:**

- *Money per day* for the Netherlands, the Philippines and South Africa, each of which
  earns more than the formula gives.

## What is not established, in one place

- whether **war or mobilisation** is what shortens the 90 days to 75, and where either
  number comes from: neither is in `defines.lua`;
- whether a history file's goods are **added or set**;
- what holds **a puppet** to 1000 IC-days, if that is what it is;
- what **supplies and fuel** a country starts with;
- how the original runs **two days before the first screen**, and what else those two days
  do;
- what moves the goods given to a country that does not exist **out of a capital somebody
  else holds**;
- everything in section 4's table beyond home production, usage and money.
