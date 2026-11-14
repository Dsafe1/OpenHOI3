# Production: the six shares, and what spending IC costs

How the original divides a country's IC each day and what each part of it does, written as
rules this project can implement. No addresses and no offsets. Each rule says how sure it
is - `confirmed`, `likely`, `inferred` in the fact base's vocabulary - or **measured**,
meaning checked on 2026-10-07 against a savegame the original wrote on 1 January 1936 with
no mods. Where this project chooses its own behaviour the paragraph is marked **Ours**.

Sources: `reversing/findings/FINDINGS-distribute.md`,
`reversing/findings/FINDINGS-production.md`, `reversing/findings/FINDINGS-manpower.md`,
`reversing/CLASSES.md` (*Industrial capacity, and the six sliders*).

Reinforcement and upgrades spend IC on units and have a spec of their own, `upkeep.md`;
so has the build queue, `buildqueue.md`.

## 1. The six shares

A country divides its IC between six things, always in this order: `confirmed`

| | share | what the IC does |
| --- | --- | --- |
| 0 | lend-lease | given to another country |
| 1 | consumer goods | keeps dissent down; spent whole |
| 2 | production | the build queue; spent as far as the queue needs |
| 3 | supplies | makes supplies; spent whole |
| 4 | reinforcement | refills units; spent as far as they need |
| 5 | upgrades | re-equips units; spent as far as they need |

- **A share is a fraction of the total IC, and the six add up to one.** `confirmed` for
  leadership's four, read out of a running game; (measured) for these six, in every country
  of the savegame.
- **A share is not in thousandths.** It is the original's second kind of number, in which
  32768 is one (`numbers.md`, section 2), and the day's arithmetic is done in that kind
  and only turned into thousandths at the end. `confirmed` It matters: Germany's supply
  share is 5085/32768, and its supplies come out right to the thousandth only if that, and
  not 0.155, is what is multiplied.
- **What a share is given to divide is the total IC** - the whole number `ic.md` ends
  with, after the cap a shortage imposes and with lend-lease received added. `confirmed`
- **The six are worked through in order, once a day**, straight after the country's IC is
  recalculated. Each pays for its resources as it goes, so an earlier share can leave a
  later one short. `confirmed`

**Every country with a tag does this, except the rebels - including a country that holds
no province.** `confirmed`, and (measured): the 23 countries that do not exist on the start
date each use resources every day, and for every one of them the metal used is its consumer
goods share plus its supply share, times its IC. East Germany, which does not exist until
1945, has two IC, spends 0.557 of them a day, and takes the metal out of Berlin's pool -
Germany's stockpile - because Berlin is its capital.

### What the shares start as

`confirmed` - read 2026-11-14 (`reversing/findings/FINDINGS-startshares.md`) - and (measured) 24 of 24. As a new game
is entered, each country in turn has its laws brought forward and its technology bought (`politics.md`, section 13),
its order of battle read and its set-up for a new game run; then its land brigades in service are counted
(`organisation.md`, section 1) and its shares set:

    cg   = the demand (section 3) in thousandths, turned into a share: cg * 32768 / 1000, the remainder dropped
    rest = (32768 - cg) / 5, the remainder dropped
    each of the six shares = rest;  then consumer goods = cg

So the demand is the one of that moment: **under the laws the new game brought forward** - the Lua's changes come after
the two days (`politics.md`, section 12) - **with the standing army's term**, the units being read, and **without the
ministers the filling of empty posts gives**, who are put in after. *Exe-hardcoded*: when, and the five-way split;
*mod-able*: the demand's figures, through `base_values`, the laws and the history's ministers.

(measured) Every country nobody has touched in the start save - the 23 that do not exist, and Germany, which the player
had not played - has these shares: East Germany 0.09998 and five of 0.17999, from mixed industry and a full civilian
economy; Germany 0.22400 and five of 0.15518 - 0.200 under the war economy and mixed industry the new game brought it
to, and 0.024 for its 33 land brigades in service on 201 IC at an effective neutrality of 60. Until 2026-11-14 the shares
were set here before the units were read and the laws brought forward, and 11 of the 24 agreed. Counting the ministers
the filling gives would leave seven short - Vichy France, East Germany, the Social Republic, Lebanon, Syria, Mengkukuo
and Guyana, each by a minister's 0.02 or 0.025. The countries the AI runs have shares of its own choosing by the save.

## 2. What a point of IC costs

- **Spending one IC costs 1 metal, 2 energy and half a rare material**, taken from the
  stockpile at the moment it is spent and recorded as the day's usage. `confirmed`
- **IC that is not spent costs nothing.** `confirmed` A share bigger than its job needs is
  wasted, not charged.
- **The IC a stockpile can pay for** is the smallest of its metal, half its energy and
  twice its rare materials, never less than nothing. `confirmed`
- **Once a day the total IC is cut to that**, before the shares are worked through.
  `confirmed` So a country short of energy loses IC that day, across all six shares alike.
  This is in the daily calculation and not in the one made on entering a game (`ic.md`,
  section 4). **It is done only for a country that holds something**: one that does not
  exist keeps its IC whatever its capital's pool holds. `confirmed`
- **Consumer goods, supplies, reinforcement and upgrades are charged without asking what the
  stockpile can pay**; production and lend-lease ask first. `confirmed` - corrected
  2026-10-13 by the reading of lend-lease's IC (`reversing/findings/FINDINGS-tradeui.md`, E.1,
  which found the stockpile's question has four callers, none of them those two); this said
  that only consumer goods and supplies did not ask. What that leaves below nothing is made
  nothing again at the day's end (`stockpile.md`, section 4).
- **IC received as lend-lease costs no resources.** Each share gets the same fraction of it
  that it has of the total, and only what a share spends beyond that is charged - how each
  of the five does it is `aid.md`, section 3. `confirmed`

Whether a charge can take a stockpile below zero is **not established**. Consumer goods and
supplies both charge without asking first.

## 3. Consumer goods

Each day, in this order: `confirmed`

1. **Money.** The country earns `base IC * IC_TO_MONEY * (1 + GLOBAL_MONEY)`, whatever its
   share is (`stockpile.md`, section 4).
2. **The bill.** The whole allocation - share times total IC - is charged for.
3. **Dissent** moves by `demand - share + DISSENT`, the last being the country's modifier
   of that name, and is never below zero. It is one for one: a share a point under the
   demand adds a point of dissent a day, a point over takes one off. There is no other
   decay.

**The demand** is the fraction of its IC the country wants on consumer goods: `confirmed`

    at war:    WAR_CONSUMER_GOODS_DEMAND
    at peace:  PEACE_CONSUMER_GOODS_DEMAND
               + 0.25 * (land brigades in service / total IC) * (effective neutrality / 100)

kept between 0.010 and 0.990. The two demand figures are the country's modifiers of those
names, which is where `base_values` puts its 0.25 for peace and 0.15 for war and where the
laws take theirs off.

The second line is the standing army. **A neutral country with a large army for its
industry pays for it in consumer goods, and stops paying the day it is at war.** A brigade
is "in service" if it is not a reserve, or if the country has mobilised - so mobilising
raises the demand twice over. Effective neutrality is neutrality less the threat the
country feels, from 0 to 100. The 0.25 is built into the original and no file changes it.

`base_values` is added whole to every country's modifier, before anything else.
`confirmed` (`modifiers.md`, section 4).

The arithmetic is whole thousandths, each step dropping its remainder (`FINDINGS-distribute.md`, section 2):

    t = land brigades in service * 1000 / total IC
    t = t * 250 / 1000
    n = effective neutrality (0 to 100000) * 1000 / 100000, held between 0 and 1000
    the term = t * n / 1000

**The brigades in service are counted every hour, with the officers** (`organisation.md`, section 1): every brigade of
a land unit, those finished and waiting to be placed among them, but a reserve's while the country is not mobilised.
*Exe-hardcoded*: the 250 and the count; *mod-able*: the peace and war figures, through `base_values` and the laws.

**The term was built only on 2026-11-02.** Until then the code took the peace figure alone, waiting for units that had
come a month before, and every country at peace asked for too few consumer goods. The AI's script hands what consumer
goods do not need to production (`ministers.md`, section 5), so every country the AI runs built more than the original's:
Japan, whose laws take its peace figure near the floor, gave consumer goods 2% of its IC on 1 July 1937 and production
72%, where the Ireland game's save of that day has 20.8% and 55.6%, and had 82 divisions to its 74. With the term: 21.6%,
51.0% and 74 divisions, and its officers' and research's shares of leadership the save's to a point.

## 4. Supplies

    supplies = share * total IC * IC_TO_SUPPLIES * (1 + GLOBAL_SUPPLIES + the technology bonus)

`confirmed`, and (measured) against Germany: a share of 5085/32768, 201 IC, 7 supplies an
IC, a law worth 10% and four levels of `supply_production` at 5% each give 283.840, which
is what the savegame says. (The law is the industrial policy Germany's history gives it;
see section 7 for why it is not the one the save shows.)

- `IC_TO_SUPPLIES` is in `defines.lua`. `GLOBAL_SUPPLIES` is the country's modifier. **The
  technology bonus is added beside it, not multiplied** - what a technology's
  `ic_to_supplies` key adds up to over the country's levels.
- **The whole allocation is charged for, and there is no check first**: the supplies are
  made whatever the stockpile holds. `confirmed`
- They count as home production.
- **They are made where the factories are.** Each core province the country owns in its
  capital's own region gets the share of the day's supplies that its IC is of the region's,
  into that province's pool; the rest goes to the stockpile. `confirmed`

## 5. The rest

- **Lend-lease** is the first of the six and is built: `aid.md`, section 3.
- **Production** hands each item in the queue, in queue order, up to one day of its cost,
  from the free part first and then against the resource check. `confirmed` It is built:
  `buildqueue.md`, section 4.
- **Reinforcement** and **upgrades** walk the country's units, and are built:
  `upkeep.md`, sections 4 and 5. Each charges for the IC it spends as section 2 says -
  production line by line, reinforcement brigade by brigade, upgrades once for the day.

**What each slider shows as its need** is `upkeep.md`, section 8, for supplies,
reinforcement and upgrades, and section 3 here for consumer goods.

## 6. Ours

- **A second number type**, `CFixed15`, for the shares and the day's arithmetic.
- **Every country but the rebels runs its shares**, as in the original, so a country that
  does not exist earns money, makes supplies and uses resources in whoever holds its
  capital. This replaces the earlier choice not to (`stockpile.md`, section 5).
- ~~**Lend-lease spends nothing yet.**~~ Built 2026-10-13 (`aid.md`, section 3): its share
  sends IC to the country's receivers, and the rest of it is wasted where it has none.
  Production spends what its queue takes (`buildqueue.md`), and reinforcement and upgrades
  what their units take (`upkeep.md`); the rest of what the three are given is wasted.
- ~~**All the supplies go to the stockpile.**~~ Since the supply pass was built they are made
  where the factories are, as section 4 says and as fuel is, and travel to the stockpile a
  province a day (`supply.md`, section 15).
- **A charge takes what there is and no more.** The original cuts total IC to what the
  stockpile can pay for before the shares run, and so does this; the stockpile is checked
  again at each charge only so that a rounding cannot leave it below zero.
- **`base_values` is added to every country's modifier.**
- The shares change only through a command, like everything else.

## 7. What the savegame does and does not confirm

`tools/OpenHOI3.SaveCheck` gives each country the laws, the government, the technology
levels and the shares the save has, runs one day's shares, and compares. Only countries
whose shares nothing has moved can be compared: the 23 that do not exist, and Germany,
which the player had not touched. The AI has moved every other country's since the last
day was run.

- **What a country that does not exist uses in a day** agrees to the thousandth, in metal,
  energy and rare materials, for 22 of 22. Until 2026-10-10 it was 20: Guyana and the Italian
  Social Republic had an IC here that counted their ministers (`ic.md`, section 3).
- **The supplies made in a day** agree for 23 of 23, Germany among them (16 until the triggered modifiers of
  2026-10-14; 14 before Guyana and the Social Republic had the right IC).
- **The laws in the save are not the laws the two days were run under.** For 16 of the 23, Germany included, the
  figures fit only with the industrial policy the new game brought forward in place of the consumer goods the save
  shows - mixed industry gives 10% more supplies. **Answered on 2026-11-14**: the install's Lua looks at every
  country's laws as the new game is set up, before the days, but its changes are commands, which the original's queue
  runs once the new game's entry is over - after the days (`politics.md`, section 12;
  `reversing/findings/FINDINGS-startshares.md`). A new game here does the same since that day, and the tool still tries
  the history's laws by itself and says which countries needed them.

One rounding rule came out of this and is not in the fact base: the consumer goods bill is
the share turned into thousandths *first* and then multiplied by the IC, where the supply
bill is multiplied out first and turned into thousandths after. (measured: East Germany's
0.557 is 0.099 x 2 + 0.359, and every one of the twenty fits that and nothing else tried.)

**The whole of Germany's last day agrees** now that the build queue is in: its industry
used 121.187 IC by the save, and here 44.823 on consumer goods, 13.982 on its three ships,
31.191 on supplies and 31.191 on upgrades, which is 121.187. (`buildqueue.md`, section 4.
The readme carried this for a time as a check that did not come out, 13.781 for the ships
against a cost of 16.160: the 16.160 was the cost under the laws the save shows, which a
change of law made after the last day, and the consumer goods bill is 44.823 and not the
45.024 the sum assumed.)

**The shares a new game gives** are the save's for all 24 countries nothing has moved (section 1), and with them the
standing-army term is seen working: Germany's 0.224 is 0.200 and 0.024 of it. Dissent two days in is the save's for 101
of 101 since the same day (`OpenHOI3.SaveCheck politics`).

## What is not established, in one place

- ~~what **sets the shares** at the start of a game~~ - read 2026-11-14, section 1;
- whether a charge can take a **stockpile below zero**;
- ~~what makes **nine untouched countries' supplies** 10% more than their laws and technology give~~ - the Lua's
  changes of law come after the two days, section 7;
- ~~**lend-lease**, in full~~ - read 2026-10-13, `aid.md`, section 3;
- ~~the **standing-army term has not been seen working**~~ - it has: Germany's 0.224 at the start (section 1). The
  0.252 this said the formula gives came from the save's laws, which the shares were never set under.
