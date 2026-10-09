# Organisation: the officer ratio, the ceiling, and what brings it back

What holds a brigade's organisation and what returns it when nothing is fighting, written
as rules this project can implement. No addresses and no offsets. Each rule says how sure
it is - `confirmed`, `likely`, `inferred` in the fact base's vocabulary - or **measured**,
meaning checked on 2026-10-08 against savegames of one unmodded game played as Germany: the
save of 1 January 1936, two days in, and the one of 1 February. Where this project chooses
its own behaviour the paragraph is marked **Ours**.

Sources: `reversing/findings/FINDINGS-organisation.md` (all of it, read for this spec),
`reversing/findings/FINDINGS-unitstart.md`, section 8 (the ceiling),
`reversing/findings/FINDINGS-opsarea.md`, section 5 (what a country's units call for),
`reversing/findings/FINDINGS-unitdaily.md`, section 4 (the crowding of a base, which
reinforcement shares).

The units themselves are in `units.md`; what a unit draws each day is in `upkeep.md`; how
officers are trained is in `leadership.md`, section 3.

All arithmetic is in thousandths and every division truncates; see `numbers.md`. A
brigade's organisation is a figure the savegames show to the thousandth, so the order of
the multiplications below is the original's and is kept.

## 1. The officer ratio

    needed = the sum of `officers` over every brigade, ship and wing the country has
    ratio  = 1                                          if needed is nothing
           = the lesser of officers / needed and MAX_OFFICERS

`confirmed`. `MAX_OFFICERS` is a military define, 1.4 in Their Finest Hour: officers beyond
140% of what the units call for do nothing. `officers` is each brigade's own figure - 100
for an infantry brigade - and **every brigade counts, whatever state it is in**, which
is not quite the sum a new game gives a country its officers from (`units.md`, section 7:
that one leaves out a support brigade too weak to count towards its division).

**When.** As a game is entered, and then every hour **after** the units have had their
hour - so an hour's organisation is worked out with the ratio the hour before left.
`confirmed`

**A unit finished and not yet placed is counted too** (`buildqueue.md`, section 6).
`confirmed`, and built. The original also counts the units lent to a country, and counts a
unit it has lent out for the country that lent it; nothing is lent here yet.

**Measured.** Taking each country's officers from the save over what its saved brigades
call for gives the ratio every figure in section 7 is right with: 0.985 for Britain
(11939.444 of 12110), 0.977 for Hungary, and the cap for Germany, whose history gives it
half as many officers again as it needs.

This is the figure the top bar shows (`topbar.md`, section 9).

## 2. The most organisation a brigade may have

Once a game is running: `confirmed`

    most = the brigade's own default_organisation               its type's, with its technologies
         + the skill of the army's commander, times his reach    not built: see below
    if it is a reserve and its country is neither mobilised nor at war:
        most = most x (1 + reserves_penalty_size)
    most = most x the country's officer ratio
    most = most x (1 + land_organisation)        or naval_organisation, or air_organisation,
                                                 by the brigade's branch

- **A brigade that is in no unit gets only the first line and the last.** The commander,
  the reserve's share and the officer ratio are all skipped for it. The one brigade this
  happens to is one just built, which is given its organisation before it is put in its
  unit - so a new ship of Germany's has 30 where its officers would allow 42
  (`buildqueue.md`, section 6; measured there).
- **`reserves_penalty_size`** is the conscription law's modifier, the same one that keeps
  a reserve under strength (`units.md`, section 4), and it is switched off by the same
  things. One caller asks for it differently: a brigade read back from a savegame is kept
  short even when its unmobilised country is at war.
- **The officer ratio is in the ceiling**, which is what makes officers matter: a country
  with nine tenths of its officers has brigades that can hold nine tenths of their
  organisation, and Germany's can hold 140%.
- **The army's commander.** The army is the unit itself when it is one, or the army a
  corps or a division answers to, with at most a corps between; a division straight under
  a theatre has none. Its commander adds his skill times how much of his command reaches
  the unit, which falls with distance. *Ours:* leaders are not built, so nothing is added.
  `COrganisation.ArmyAbove` finds the army for when they are.

**Before a game is running the ceiling is `default_organisation` and nothing else.**
`confirmed` That is why a brigade read from an order of battle starts at its type's figure
(`units.md`, section 4), and it is what section 7 turns on.

Four things hold a brigade to this ceiling: `confirmed`

| what | when |
| --- | --- |
| reading it from a file | as it is made (`units.md`, section 4) |
| gaining a level of equipment or of a doctrine | the day it does (`upkeep.md`, section 5) |
| the hourly return, section 3 | every hour it is not fighting |
| a change of law, section 5 | at once, for every brigade of the country |

## 3. The hourly return

Every hour, for every unit that is not fighting. `confirmed` throughout.

First the unit's factor:

    factor = the share of its supplies it was given the last time it drew        upkeep.md, section 2
    if it stands on land:
        roads  = the province's infrastructure x (1 + local_infrastructure + global_infrastructure)
        factor = factor x roads, with roads held between 0.010 and 1
    factor = factor x the country's officer ratio
    for a fleet or an air group with a base:
        factor = factor x how much of its work the base does                     section 4
    factor = factor x (1 + org_regain)
    if it was given no supplies at all:  factor = -1

Then each brigade:

    most  = section 2's ceiling, a reserve's share taken off only in peace
    share = organisation / most                     in thousandths; -1 if most is nothing
    if share is under 0.250:      factor = factor x (1 + LOW_ORG_REGAIN_BONUS)
    else if it is over 0.750:     factor = factor x (1 - LOW_ORG_REGAIN_BONUS)
    gain = default_morale x factor x 0.2
    organisation = organisation + gain, held between nothing and most

- **An hour at a factor of 1 brings back a fifth of a brigade's morale**: 0.06 for infantry
  with a `default_morale` of 0.30, so 1.44 a day. The fifth is the original's own and no
  file moves it; nor do the quarter and the three quarters.
- **`LOW_ORG_REGAIN_BONUS`** is a military define, 0.3 in Their Finest Hour and 0.25 in the
  base game's file. A brigade nearly out of organisation gains that much faster, and one
  nearly full that much slower.
- **The province's infrastructure** is what its `infra` building gives its modifier, a
  tenth a level, so full infrastructure is 1. A fleet in port stands on land and counts
  the port's; at sea nothing is counted.
- **A unit given no supplies at all loses organisation** at the rate it would have gained
  it: the factor is minus one, and a brigade low on organisation loses faster too. A unit
  given a thousandth of its supplies gains a thousandth as fast; only nothing turns it
  round.
- **The hold to the ceiling does not ask which way the brigade is off.** A brigade above
  its ceiling is at it an hour later, whatever it gained. This is what cuts a country's
  organisation when its officers are lost.
- **`org_regain`** is a country modifier: the minister type `school_of_psychology` gives
  0.1.

**A unit that is fighting gets nothing back, nor one marching into a province an enemy
holds** - with a path, no attack delay running, and the next province held by a country its
own is at war with. Built with the battle (`landcombat.md`). The original also skips an army
aboard a fleet at sea; it multiplies organisation by `PARATROOP_DROP_ORG_MULT` once after a
drop; and it takes `STRAT_REDEP_ORG_LOSS` off the hour's gain of a brigade being strategically
redeployed. None of those three can happen yet.

*Ours:* a new game here starts at hour nought with no hour run, so the first hour is the
first thing to hold a brigade to section 2. Reserves fall to their ceiling in that hour
and regulars begin to climb.

## 4. A crowded base

A fleet or an air group with a base shares it with everything else based there, and a base
has only so much room. `confirmed` for the arithmetic.

    based    = the ships, or the wings, of every unit based in that province
    room     = a fleet:     naval base level x NAVAL_BASE_EFFICIENCY
                              x (1 + naval_base_efficiency, the modifier and the technology's)
               an air unit: air base level
    crowding = room / based, held between 0.250 and 1

- **`NAVAL_BASE_EFFICIENCY`** is a military define, 6 in Their Finest Hour: six ships a
  level. An airfield has room for one wing a level.
- **The modifier and the technology are those of whoever controls the province**, not of
  the fleet's owner. That the technology's part is the key `naval_base_efficiency` is
  `inferred`: the original reads a figure off the controller's technology and the key is
  the one that fits. Nothing here measures it.
- **An air group aboard its carrier is never crowded**, where the carrier is. `confirmed`
- **Ships do not crowd an airfield** nor wings a port: a province keeps the two apart.
  `confirmed` That another country's units based there count is `inferred`.
- **However crowded, a base does a quarter of its work.** A fleet based in a province with
  no naval base at all is at that quarter.

The same figure slows reinforcement (`upkeep.md`, section 4).

## 5. A change of law

After any change of law - not only conscription - the original rebuilds the country's
modifier, prices every line of its build queue again (`buildqueue.md`, section 2), and
then holds every one of its brigades to both ceilings: `confirmed`

    if strength is above the most it may have:
        the country's manpower goes up by  (the excess / max_strength) x build_cost_manpower
        strength becomes the most, and its highest starts again from there
    if organisation is above section 2's ceiling:  it becomes the ceiling

So going down a conscription law sends the reserves' extra men home and gives them back to
the pool, and any law that moves an organisation modifier takes effect at once and not an
hour later.

*Ours:* nothing changes a law yet - there is no politics screen and no AI - so nothing
calls this in a game. `COrganisation.HoldToCeilings` is the routine, built because the
savegames cannot be read without it (section 7) and so that whatever first changes a law
has it to call; `CBuildQueue.RecalculateAll` is the queue's half, and waits the same way.

## 6. What officers are for, in one place

A country trains officers with a share of its leadership (`leadership.md`, section 3) and
starts with those its units bring (`units.md`, section 7). They do two things, both
through the ratio: they set how much organisation every brigade can hold (section 2) and
how fast it comes back (section 3). Building units raises what is called for and so
lowers the ratio; and officers are lost in battle (`OFFICER_COMBAT_LOSS`: `landcombat.md`,
section 6).

## 7. Against the savegames

`tools/OpenHOI3.SaveCheck` runs the two days the original has behind it on the first
screen (`upkeep.md`, section 9) and sets each brigade's organisation beside the save's.
**What it has to assume about those two days is itself a finding**, so it is set out:

- **The two days are run before the original counts a game as running**, with an hour
  before each day. `inferred`, from three brigades. During them a brigade's ceiling is its
  own figure and nothing else, so nobody loses organisation and almost nobody can gain
  any - every brigade starts at its figure. The exception is Germany's three Waffen-SS
  brigades: a doctrine raised their figure from 55 to 60 on the first day, and the hour
  before the second gave them `0.55 x (1.4 x 0.7) x 0.2 = 0.107`. The save has them at
  55.107. Were the officer ratio in the ceiling, they would be under three quarters of it
  and have gained 0.154.
- **Then a change of law holds a country's brigades to the running game's ceiling**
  (section 5). The original's AI changes laws as a game opens, after its last day is run
  (`upkeep.md`, section 9); a save does not say for whom, so the tool takes a country to
  have been held if any of its brigades is below what the two days left it.

With that, on the save of 1 January 1936 (**measured**):

| | agree |
| --- | --- |
| countries with a brigade below what the two days left it | 74 of the 75 that could tell |
| organisation in those, every brigade not under an army with a commander | 2676 of 2676 |
| organisation where holding changes nothing, or was not done (Turkey) | 266 of 266 |
| organisation under an army with a commander, no leaders built | 142 of 764 |

- **2676 of 2676 is to the thousandth**: Germany's reserves at 28.000 (40 x 0.5 x 1.4),
  a Hungarian headquarters at 29.310 (30 x 0.977), Soviet reserves at 8.627.
- **The 622 under a commander are what a commander's skill would make them.** 136 are
  above the ceiling without him and exactly where the two days left them. The other 486
  are at the figure a bonus of a whole number gives - 1 for 339 of them, 2 for 105, 3 for
  30 - and a dozen at 0.82 to 1.30, where his reach is not the whole of it. So the
  ceiling's first line is confirmed by what it leaves over, and waits for leaders.
- **It is the files' chain of command that fits, not the save's.** A Soviet division the
  save has straight under one of the AI's theatres is held as the army its file puts it
  under holds it. So the laws were changed before the AI made its theatres.
- **No hour of a running game had passed**: it would have raised every German regular
  from 40 towards 56, and they are at 40.000.

And a month on, in the save of 1 February (**measured**, read off the save by hand): all
172 German brigades, ships and wings are at exactly their ceiling - 56.000 for those of
40, 49.000 for 35, 70.000 for mountain troops, 42.000 for ships of 30, 84.000 for the
Waffen-SS - and the reserves at half that. So the hourly return reaches the ceiling and
stops, and ships and wings take the country's ratio like the rest.

## What is not built

- **Leaders**, and so the commander's part of the ceiling (section 2).
- **Being carried**, which stops the hourly return (section 3), and the paratroop and
  redeployment rules beside it.
- **Anything that changes a law**, so section 5 is never called in a game.
- **Units lent to another country**, which count for and take the ratio of the country
  that lent them.
- ~~**The supply network.**~~ Built: a unit is given its share of what lies in the province
  it draws on (`supply.md`, section 13), so one beyond its supply lines loses organisation
  for want of supply as in the original.

## What is not established, in one place

- **How the original runs its two days**, and so whether "an hour before each day" is how
  it is written or only what it comes to. Three brigades are all there is to go on.
- **Whether `naval_base_efficiency` is the technology key** behind a port's room.
- **Whose units share a base.** All that are based in the province, by this reading.
- **What a commander's reach is**, for the dozen brigades whose bonus is not whole.
- **Which countries had a law changed as the game opened.** The tool infers it from the
  brigades.
