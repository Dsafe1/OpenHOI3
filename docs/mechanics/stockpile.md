# The stockpile: where it is, what it starts with, and what a day does to it

How the original keeps a country's goods, written as rules this project can implement. No
addresses and no offsets.

**The starting stockpile was measured first and read afterwards.** It was worked out on
2026-10-07 from a savegame the original wrote on 1 January 1936, with no mods, and the
routine that fills it was found and read the same day. Rules from the fact base are marked
`confirmed`, `likely` or `inferred` in its vocabulary; what rests on the savegame alone is
marked **measured**; this project's own choices are marked **Ours**.

Sources: `reversing/CLASSES.md` (*Provinces, goods and supply* and *The country's goods
pools*), `reversing/findings/FINDINGS-leadership.md` (section 10),
`reversing/findings/FINDINGS-ic.md`, `reversing/findings/FINDINGS-production.md`
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
  exception and keeps a pool of its own. `confirmed` Both are built since 2026-10-13
  (`war.md`, section 14).
- **Two countries can share one stockpile.** East Germany's capital is Berlin and
  Nationalist Spain's is Madrid, so on the start date what those two are given lands in
  Germany's stockpile and Republican Spain's. (measured)

*Ours:* a country has one capital for now, so there is no acting capital and no exile.

## 2. What a stockpile starts with

When a game is started from the history files - and never when one is loaded - every
country is set up once, in turn. `confirmed` throughout unless marked.

**How many days.**

    days = 30 + 5 * (12 - the months it has been at war), with the bracket never under 2

- **A country at peace starts with 90 days of what its industry needs.**
- **A country at war starts with five days fewer for every month of it**, and never fewer
  than 40. It is being at war that counts, not being mobilised.
- The months are calendar months, the year times twelve plus the month, taken from the day
  the **oldest war it is still fighting** began. Italy and Ethiopia have been at war since
  3 October 1935, which is three months before 1 January 1936: 75 days. (measured: Italy's
  69 x 75 = 5175 metal and Ethiopia's 7 x 75 = 525, both exact.)

**How much.**

    amount         = base IC * days
    crude oil      = amount
    metal          = amount
    energy         = amount * 2
    rare materials = amount / 2
    money          = amount * IC_TO_MONEY * 0.05

- **Base IC, not total IC.** It is the province sum in whole IC before any law, minister or
  technology (`ic.md`, section 3), so it includes the five that `base_values` gives every
  country. The United States get 256 x 90 = 23040 metal although their laws leave them 153
  IC to spend.
- **A subject country's `amount` is held to 1000**, whatever its IC. Manchukuo has a base
  IC of 21 and starts with exactly 1000 metal, 500 rare materials and 2.5 money, not 1890,
  945 and 4.725. (measured, and it is the only subject whose IC is high enough to show it.)
- **Everything is added to the pool of the country's capital, whoever holds that
  province.** A country that does not exist yet still has a base IC of five and a capital,
  so 450 metal, 900 energy, 225 rare materials and 450 crude oil are put in a province
  that belongs to someone else. Berlin starts with 149 x 90 metal: Germany's 144 and East
  Germany's five. (measured)
- **Money is given only where the pool has none**, and the countries are set up in the
  order `countries.txt` declares them. (measured) three ways:
  - Berlin has Germany's 32.4 and nothing for East Germany, which is declared later.
  - Madrid has 1.125 - what Nationalist Spain's five IC give - and nothing for Republican
    Spain, which holds the city, has 44 IC, and is declared on the next line.
  - The United States, whose history file gives them 1 money, have exactly that 1.
- **`IC_TO_MONEY` is the define that sets a day's money** (section 4), 0.05 in Their Finest
  Hour. The other 0.05 is built into the original. So a stockpile starts with a twentieth
  of what its IC-days would earn, and a mod that changes the define changes it.
- **What the history file gives is there first.** A country's history file can name a good
  with an amount, and the history is applied before any of this. Their Finest Hour does it
  twice on the start date: the United States' `money = 1`, which is why they get no other,
  and Germany's `rare_materials = 40000`, which Berlin has on top of its 149 x 45. That a
  history file's own amount is added rather than set is `inferred` from Germany alone.

**Supplies and fuel** are the same number of days of what the country's **units** use:

    supplies = days * the supplies its units use in a day
    fuel     = days * the fuel its units use in a day

Which is why countries with the same base IC start with very different amounts.

**Two things that are not goods are set in the same pass:**

- **Diplomatic influence**: a point for every five base IC, in whole points
  (`leadership.md`, section 4).
- **War exhaustion**: 0.05 for every month at war, up to 10; for a country at peace, 3 less
  0.05 for every month since its last war ended, and not below nothing - which is nothing
  at all for a country that has never fought one. (measured: the savegame has every
  country at 0.000 two days later, Italy included, so something takes it off again.)

Of the 78 stockpiles belonging to a country that exists, metal and rare materials come
out exact - to the thousandth - for 76 and money for 77 (section 6 has the rest). Energy
and crude oil have the two conversions between the start and the save as well
(`ic.md`, section 4); with two days of those taken off too, energy is exact for 71 and
crude oil for 70.

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

**How it runs them was read on 2026-10-10** (`reversing/findings/FINDINGS-supplypass.md`,
section 7): a day, then the filling of the provinces along the supply network, then another
day - all as a game is entered. `supply.md`, section 16, has it and what follows from the
order.

*Ours:* **done since 2026-10-10.** A new game starts from section 2 and runs the two days
as it is made, the filling between them, so the stockpile of the first screen is two days
of home production less usage on from the start - and the first daily pass played is the
first midnight, the third. `tick.md`, section 6, has the order, what is different while
those days run, and the check.

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
- **Consumer goods and supplies are charged whatever the stockpile holds**; the other shares
  ask first what it can pay for. `confirmed` So a stockpile can stand below nothing in the
  middle of a day.
- **The last thing a day does to a land province is to hold each good in it between nothing
  and 99999.** `confirmed` So the debt is gone by the day's end, and no stockpile passes that
  figure. (measured: Israel, which does not exist in 1936 and whose capital's goods Britain
  has shipped away - `convoys.md`, section 8 - records a day's use of 2.395 metal with no
  metal in Jerusalem, and eleven more such countries likewise.)
- **Money** comes in once a day:

      money = base IC * IC_TO_MONEY * (1 + GLOBAL_MONEY)

  `IC_TO_MONEY` is in `defines.lua`, 0.05 in Their Finest Hour, and `GLOBAL_MONEY` is the
  country's modifier of that name. It is base IC again, and **the consumer goods slider
  has no part in it**: the fact base read that twice and reports it as unexpected.
  `confirmed` for the formula; (measured) it reproduces the savegame's figure to the
  thousandth for all 100 countries that have an economy, once the gold three of them hold
  is counted (`modifiers.md`, section 4). It is counted as home production and added to
  the stockpile. It is paid as part of the consumer goods share's work, and only to a
  country that has any IC left that day (`production.md`, section 3).

## 5. What this project does now

*Ours*, all of it:

- A province has a pool. A country's stockpile is its capital's.
- A new game fills the stockpiles as section 2 says. Wars are read from `history/wars`,
  for who is in each and when it began: a war began on the earliest date its file has an
  entry for. **Puppets are not read**, so Manchukuo starts with the full 1890 metal.
- **Supplies and fuel start as section 2 says**, once the units have been made: so many
  days of what they use (`upkeep.md`, sections 1 and 7). War exhaustion starts at nothing:
  nothing here reads it.
- Each day a country that holds its capital has what its provinces produced added to its
  stockpile. Then every country's shares of IC are run (`production.md`): money and
  supplies come in, and what consumer goods and supplies cost in metal, energy and rare
  materials goes out.
- A country that exists also turns energy into crude oil and crude oil into fuel each
  day (`ic.md`, section 4).
- **Units draw their supplies from it every day**, and their fuel when they burn any, and
  reinforcement and upgrades spend IC that is charged to it (`upkeep.md`). Supplies and fuel
  leave it for the provinces through the supply network, and a unit draws on the province it
  stands in; what is made away from the capital comes into it a province a day
  (`supply.md`, sections 12 to 15).
- **The build queue takes nothing yet**, nor does trade. **Convoys do**, since 2026-10-10:
  supplies and fuel leave the stockpile for harbours across the sea, and the resources of
  cut-off land come into it (`convoys.md`, section 6).
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

- *Manchukuo*, in metal, rare materials and money: it is a subject, and held to 1000
  IC-days (section 2).
- *Energy and crude oil* for Japan and the Philippines, which have convoys or an overlord
  moving those goods, and for Albania, Iraq and Mexico, which convert a little less than
  the day's limit: the balance term is what binds for them, and it depends on flows this
  project does not track yet.
- *The United Kingdom* has 454 more metal and 225 more rare materials than its own 161 IC
  gives it, which is one five-IC country's worth and a little over. What a country that
  does not exist was given, in a capital Britain holds overseas, has reached London by
  the second day. **How was read on 2026-10-10**: everything lying in cut-off land is moved
  to its harbour each day and a convoy brings it home (`convoys.md`, section 8). A game made
  here and run as the original runs its start has London's crude oil to the thousandth, the
  450 with it. This check does not run those two days, so the line stays.

## What is not established, in one place

- whether a history file's goods are **added or set**;
- what decides the day **a war began** for this purpose, where its file has more than one
  dated entry;
- what takes the starting **war exhaustion** off again;
- ~~how the original runs **two days before the first screen**~~ (section 3); what else those
  two days do besides what the checks here have shown;
- ~~what moves the goods given to a country that does not exist **out of a capital somebody
  else holds**~~ - read 2026-10-10: the daily move of everything in cut-off land to its
  harbour, and the convoy from there (`convoys.md`, section 8);
- everything in section 4's table beyond home production, usage and money.
