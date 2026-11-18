# Upkeep: what units draw each day

What a day does to a unit that is not fighting, and what it costs its country: supplies and
fuel, the men who go home, reinforcement and upgrades. Written as rules this project can
implement. No addresses and no offsets. Each rule says how sure it is - `confirmed`,
`likely`, `inferred` in the fact base's vocabulary - or **measured**, meaning checked on
2026-10-08 against savegames of one unmodded game played as Germany: the save of 1 January
1936, two days in, and four later ones up to 20 April. Where this project chooses its own
behaviour the paragraph is marked **Ours**.

Sources: `reversing/findings/FINDINGS-unitdaily.md` (all of it, read for this spec),
`reversing/findings/FINDINGS-manpower.md` (troop rotation),
`reversing/findings/FINDINGS-production.md` (the shape every share of IC has),
`reversing/findings/FINDINGS-leadership.md`, section 10 (the starting stockpile),
`reversing/findings/FINDINGS-supplypass.md` (the province a unit draws on and what it is
given there: `supply.md`, section 13).

The units themselves are in `units.md`; the six shares of IC, and what a point of IC costs
in resources, are in `production.md`.

All arithmetic is in thousandths and every division truncates; see `numbers.md`. **The
truncation is not a detail here.** Two of the rules below are what they are only because a
fraction is dropped at a particular step, and both show in a savegame.

## 1. What a unit uses

A unit uses what its own brigades use; the units under it are counted for themselves.

    potency  = 1 + the country's supply_consumption modifier
               - 0.05 x the army group commander's skill x his reach   only for a unit somewhere
               + what the leaders' supply_consumption traits come to    leaders.md, section 8
               never below nothing
    supplies = sum over its brigades of
                   supply_consumption * (potency + 0.01 * the levels the brigade holds)
                   * strength / max_strength                         not for a ship
    fuel     = sum over its brigades of
                   fuel_consumption * (potency + 0.01 * the levels the brigade holds)

- **Every level of anything a brigade holds makes it use a hundredth more**, of supplies and
  of fuel: the levels of all its technologies added together, equipment and doctrine alike.
  `confirmed`
- **A brigade under strength uses that much less in supplies.** It is the type's full
  strength that it is measured against, so a reserve held to half uses half. A ship uses
  the same whatever state it is in. `confirmed`
- **Fuel does not go by strength.** `confirmed`
- `supply_consumption` and `fuel_consumption` are the brigade's own figures, with its
  technology in them (`units.md`, section 2). `supply_consumption` the modifier is a law's
  or a minister's.
- Each product is truncated brigade by brigade, in the order written.

**The leaders' two terms.** The army group is the unit itself or the first above it that is
one; its commander takes a twentieth off for each point of his skill, times how much of the
army group's command reaches the unit (`leaders.md`, section 8) - 0.15 for a skill of 3 in
full reach - and nothing for a unit that is nowhere. Then what the leaders'
`supply_consumption` traits come to for the unit is added: its own leader's, half its
commander's, a quarter his. The floor at nothing is tested once, after both. `confirmed`, and
built (`CUnitSupply.Potency`, 2026-10-11).

**A land unit on a strategic redeployment uses `STRAT_REDEP_SUPPLY_MOD` times its supplies -
twice in Their Finest Hour - and no fuel**, the unit's total and each brigade's figure alike, the
supplies after the strength term. `confirmed` (`reversing/findings/FINDINGS-unitdaily.md`), and
built since 2026-11-18 (`CUnitSupply`). It was left out while there were no strategic
redeployments, and stayed out after they came: on the July 1941 save it made the Soviet Union's
provinces ask 494.75 a day where the original's ask 636.33, and the first midnight after the load
left 339 more supplies in Moscow, over the 50,000 at which the production minister's script shuts
supplies off (`reversing/findings/FINDINGS-liveread.md`, section 131).

(measured) A save keeps one figure of this: a country's `usage` of supplies is what its
units **standing in its capital** were given, plus what the supply network carried out of
the capital. With the second taken off, the formula gives the first to the thousandth for
62 of the 65 capitals whose garrison the save shows fully supplied - France 7.590, Denmark
2.159 - where it gave 38 before the leaders' terms. Of the three it does not, two have a
headquarters the original's AI made, and Moscow was 4.101 against the save's 2.626 - until a
faction's modifier reached its members (2026-10-13, `factions.md`, section 1): with the
Comintern's `supply_consumption = -0.33` it is the save's.

## 2. The draw

**When.** A country's units are seen to before its economy is run: each unit first loses
its rotated men (section 3) and then draws, and only after every unit has drawn does the
country make its supplies and run its shares. A unit that is built and not yet placed draws
too, after the others, and loses no men first. `confirmed`, and built
(`buildqueue.md`, section 6).

**From where, and how much.** A unit asks for what section 1 gives **of the province it
draws on** - the one an army stands in, the one a wing or a fleet is based in - which a
network of provinces keeps filled from the capital, losing a little at every step and limited
by what each province can pass. It is given its share of what lies there, and remembers how
much of what it asked for that was. All of it is `supply.md`, section 13. `confirmed`, and
built.

- **Fuel is burnt only by a unit that is moving, and by a fleet that is not in its own
  port.** An army or an air unit standing still is told what it would have had and the
  province keeps it. `confirmed`
- A unit starts at 1 for both, so one that has never drawn counts as supplied. `confirmed`
- What the units on a country's ground asked for in a day is added up: it is the supply
  share's need (section 8).

The original has a second mode, which it calls arcade, where every unit draws straight on its
country's stockpile and the network does not run. `confirmed` *Ours:* not built. It is what
this project did until the daily pass was.

## 3. Troop rotation

Every day, each brigade of every **army** loses a share of what it may have:

    rate = the country's peacetime_manpower_rotation modifier
    if the unit received less than all its supplies last time:
        rate = rate * (1 + (1 - supplies received) * PEACETIME_MANPOWER_ROTATION_LOW_SUPPLY_FACTOR)
    lost     = most * rate / 365
    strength = strength - lost                   not below nothing, not above most

- `most` is what the brigade may have: its full strength, less the reserve's share
  (`units.md`, section 4).
- **Ships and wings do not rotate.** `confirmed`
- **It is not asked whether the country is at war**, whatever the modifier's name says.
  `confirmed`
- The modifier is the conscription law's: 0.10 a year on a volunteer army, 0.03 on a
  two-year draft. The define is 1.25, so a unit given nothing loses men two and a quarter
  times as fast.
- It takes strength and not manpower. The manpower is paid when reinforcement puts the
  strength back.

(measured) A German brigade of 30 on a two-year draft loses `30 * 0.03 / 365 = 0.00246`,
which is 0.002 once the fraction is dropped, and a reserve held to 15 loses 0.001. Two
days in the save has them at 29.996 and 14.998, and so has every other supplied brigade
in it: 3,638 of 3,638.

## 3a. Attrition

Read whole on 2026-11-14 (`reversing/findings/FINDINGS-manpower.md`, Attrition) and built the same day
(`Sim/Units/CAttrition`). `confirmed` throughout, every figure in thousandths and every step cut to the thousandth.

**A brigade's attrition in a province**, which the original asks per brigade:

    nought for a brigade that is not land
    base     = the province's attrition modifier                             [mod: the province's modifiers -
                                                                               Their Finest Hour's land_province gives 1]
    infra    = infrastructure x (1 + local_infrastructure + global_infrastructure), held between 0.010 and 1
    weather  = the first that holds:
                 temperature >= HIGHTEMPERATUREATTRITIONTHRESHOLD  1, times (1 - the country's jungle_effects)
                                                                      where the terrain's humidity is above 0
                 temperature <= LOWTEMPERATUREATTRITIONTHRESHOLD   1 - the country's winter_effects
                 windspeed >= WINDATTRITIONTHRESHOLD               1
                 else                                              0
               + the unit's commanders' winter_attrition trait where the province is below 0 degrees
               never below 0                                         [mod: defines.lua, the weather block; traits.txt]
    sum      = base + (1 - infra) + weather
             + the terrain's attrition and the brigade's adjuster for that terrain, where the two are above 0
                                                                     [mod: terrain.txt; the unit's file]
    where the province's owner counts the brigade's country a friend, military access counting:
             sum = a tenth of it at the front (a neighbour held by an enemy at war), nought elsewhere
    figure   = sum x (1 + the country's technology maximum_attrition), never below 0     [mod: the technologies]

So **an army at home and away from the front takes nothing**, a tenth at a front of its own or a friend's, and the whole
on an enemy's or a neutral's ground. The infrastructure term grows as infrastructure falls - 0.990 more at none.

**The day's loss** [exe the figures]: each day, after its supplies are drawn, every land unit **in no battle** - moving or
standing - loses from each brigade

    lost     = figure x 0.010 x the brigade's full strength / 30            each step cut to the thousandth
    strength = strength - lost, not below nothing, not above most

a month's rate taken daily. A brigade of 30 at a figure of 1 loses 0.010 a day. **Not while a game is being made**: the
original runs it only with a game on screen, so the two days a new game runs first take nothing. The loss is counted on the
played country's strategic warfare records (`strategicwarfare.md`, section 3).

**The unit panel's `unitattrition`** is the first brigade's figure, whole: the figure is a percentage as it stands, so 1
reads `1%` (`unitpanel.md`, section 4).

(measured) Four hours across the midnight of 7 to 8 January 1936, set beside the original's save of 02:00
(`OpenHOI3.SaveCheck load`): a brigade's strength the save's in 3,977 of 4,072, where without attrition 3,933. Of the rest,
Italy's five in East Africa differ by 0.001 to 0.006, not explained - a division standing at that midnight in another
province than the original's would do it.

The panel's tooltip breaks the figure into its causes since 2026-11-14 (`unitpanel.md`, section 10.2;
`reversing/findings/FINDINGS-unitpaneltooltips.md`, section 11).

## 4. Reinforcement

The fifth share of a country's IC. `confirmed` throughout unless marked.

**Which units, in what order.** Every unit that is allowed reinforcement - a switch on the
unit, on when it is made - in the order the units were made, which is the order of their
numbers; but a unit marked as a priority goes to the front of the list, the last such
first.

**Which brigades.** One that is below what it may have and not being carried anywhere; not
a land unit's while it is in a battle, nor any unit's while it is falling back
(`landcombat.md`); and a ship's only in port.

**What a day does to one brigade:**

    most    = what it may have                          full = its type's max_strength
    missing = most - strength
    share   = missing / most                            in whole thousandths
    if share is nothing: the brigade is left alone

    speed   = (1 + reinforcement_bonus) * (1 + unit_repair) * the unit's supplies received
    gain    = speed * full / 30 * 0.5
    men     = gain / most * (build_cost_manpower * most / full)
    if the country has fewer men than that, gain and men are cut to what it has

    ic      = build_cost_ic * repair_cost_multiplier    build_cost_ic never taken as under 1
    a reserve: ic * 5 * the share its law keeps back    never less than a regular

    if strength + gain would reach most:
        gain = missing / speed
        ic and men are cut by the same proportion

    if the share of IC has anything left:
        the IC is charged for, the brigade gains, the men leave the manpower pool,
        and its experience becomes  old * (1 - g) + the country's unit_start_experience * g
            where g is the gain as a share of most
        the whole ic comes off what the share has left

    the country's need goes up by ic, and its manpower needed by share * the men a whole brigade takes

Six things about it, the first two of which a player sees:

- **A brigade is not touched until it is short by a thousandth of what it may have.**
  `share` is a whole number of thousandths and at nothing the brigade is skipped: 0.030 for
  a brigade of 30, 0.015 for a reserve held to 15. So rotation is not put back day by day.
  A brigade drifts down until it crosses the line, is topped up in a day, and starts again.
- **Topping up divides what is missing by the speed.** At a speed of 1 a brigade is filled.
  A country with ball bearings (`unit_repair = 0.15`) gets 87% of what is missing, and so
  never quite fills one.
- **A day at full speed brings back a sixtieth of a brigade's full strength.** Both
  numbers, the thirty and the half, are the original's own and no file moves them.
- **A day costs a twentieth of what the brigade costs to build**, however much or little
  the day brings back, until the topping-up scales it. `repair_cost_multiplier` is 0.05 on
  every unit type of Their Finest Hour.
- **The share overspends by one brigade**: it is asked whether anything is left before a
  brigade, and the whole of that brigade's cost comes off afterwards.
- **The slider's need is the day's cost of every brigade past the thousandth**, whether or
  not anything was spent on it. So it is nothing most days and jumps when a batch of
  brigades crosses the line together.

`reinforcement_bonus` is what mobilising gives (`initial_mobilization`, +1.0: twice as
fast). The speed has one more factor, for ships and wings: the room their base has over
what is based there, never under a quarter. It is the same figure that slows the return
of their organisation, and is set out there: `organisation.md`, section 4.

*Ours:* a fleet counts as in port when it is where its order
of battle bases it. The original adds `PRIDE_BONUS_EXP` to the experience of the pride of
the fleet each time it is reinforced, before the watering down; that is built as read,
though it reads like a mistake of theirs (`reversing/findings/FINDINGS-unitdaily.md`,
section 4).

(measured) The saves show the sawtooth and nothing else would. Two days in, Germany's
brigades have had two days of rotation and no reinforcement, with 31 IC in the share:
they are 0.004 short, under the line. In the four later saves a regular brigade is
0.013-0.014, 0.015-0.018, 0.025-0.028 and 0.011-0.014 short, and a reserve is half of that
every time.

## 5. Upgrades

The sixth share. `confirmed` throughout unless marked.

**Which units, in what order.** As for reinforcement, with the unit's other switch - but **the units the country has
lent out (`expeditions.md`) come before its own**, where reinforcement takes them after: a unit is upgraded by its
lender, at the lender's levels and out of the lender's share, and never by its host. A unit marked as a priority still
goes to the front, the last such first. (read 2026-11-14, `reversing/findings/FINDINGS-unitdaily.md`, section 5; until
then this took them after, as reinforcement does, which mattered only where the share ran out.)

**Which technologies.** Each technology a brigade lists (`units.md`, section 2) in which the
country's level is above the brigade's own - **unless the technology says
`can_upgrade = no`**, which is never brought up: 19 of Their Finest Hour's do. Every
technology a brigade is behind in moves at once, each one level at a time. **For a government in
exile the level is its faction's first member's where that is higher** than its own
(`reversing/findings/FINDINGS-surrender.md`, C.7; `war.md`, section 14). (measured) Belgium's
brigades at 1941.6.22, brought up towards Britain's levels, spend the 0.989 IC on the first day
that its stockpile in the save shows; at its own levels they spent 0.248.

**What a day does to one technology of one brigade:**

    rate  = 2 * (1 / the days the brigade takes to build)      section 6; in whole thousandths,
                                                               the fraction dropped before the doubling
            never less than 0.001
    rate  = rate * SHIP_, AIR_ or LAND_UPGRADE_SPEED_MOD       by what the brigade is

    daily = what the brigade costs to build (section 6)
            / the number of technologies it lists
            * 0.5 / the levels it is behind in this one

    a technology with change = no:   daily = nothing, and the day's progress is the whole level

    if daily is more than the share has left:
        progress = what is left * 2 * rate      and what is left is paid, all of it
    else
        progress = rate                         and daily is paid

    the level's progress goes up by that; at a whole level it starts again from nothing
    and the brigade has the level - and, for a technology that changes a unit (not a doctrine),
    its builder becomes its owner, unless the unit is one this country lent out (units.md, section 5)

    the country's need goes up by daily

When a brigade gains a level its figures are worked out again (`units.md`, section 2), and
it is held to the strength they allow and to the organisation it may now have
(`organisation.md`, section 2). The resources for the day's IC are charged once, at
the end.

- **A doctrine costs nothing and reaches a brigade a level a day**, whatever the share is
  given. (measured: every one of the 10,235 doctrine levels in the save is where a day a
  level puts it.)
- **And a level more as a game is entered.** The original's `CInGameIdler::Enter` runs every country's six shares of IC
  and four of leadership once, with nothing to share out (`confirmed`, read 2026-11-15: the call passes an allocation of
  0 and a flag of 1, and `CDistributeUpgrade::Distribute` never reads the flag), so no equipment moves on but every
  brigade behind its country in a doctrine gains a level - before a new game's two days. A 1936 start, its default
  models a level or two behind, cannot tell; at a later start a model-1 brigade three levels behind is level with its
  country two days in, which the start saves of all three later bookmarks have (**measured**: without it 48, 775 and 856
  doctrine levels were a level short of the save's, and at a gap of four a level short of the country, as the save is),
  and its organisation's first hour runs off the higher figure - Germany's 44 reserves and headquarters of 1939 that
  were a few hundredths low now agree, 35.077 where they were 35.053 (`CUpgrades.RunAtEntry`). *Ours, not built*: the original's entry
  does the same for a **loaded** game, a doctrine a level on at every load.
- **Being further behind costs no more a day.** The cost of a technology is divided by the
  levels it is behind, so a brigade pays half its build cost a day over its whole list at
  most, for as long as it takes.
- **Ships upgrade four times as fast as their build time says**: the three defines are 4, 1
  and 1.
- **When the share runs out, the technology it runs out on gets a sliver and everything
  after it gets nothing.** The sliver is the money left, doubled, times the day's rate - it
  is not divided by what the technology costs. Who goes without is decided by the order of
  the units' numbers.
- **The slider's need is the day's cost of everything outstanding**, spent or not.

(measured, and exact) Two days of this on what the orders of battle make, with the save's
shares and technology, give the save's own figure on **every one of Germany's 1,469
technology lines**: 0.024 of a level on light armour (160 days: `1000 / 160` is 6, doubled,
two days), 0.032 on motorised, 0.036 on cavalry and interceptors, 0.020 on bombers, 0.080
on destroyers. Germany's share is 31.191 IC and its need 36.088; the share covers its units
in number order as far as the third wing of its fifth air unit, which has 0.008 on its
first technology where its neighbours have 0.020 - two days of `0.206 * 2 * 0.010` - and
nothing on the rest, and the three wings after it have nothing. For the other countries
22,993 of 25,179 lines agree; an AI moves their shares, and what is left is mostly a share
that was nothing on the day and is something in the save, or the other way about.

## 6. What a unit costs, and how long it takes

Upgrades are priced from these, and so is the build queue - with one more term, a
hundredth on both for every level the brigade is to be built with, which an upgrade does
not have (`buildqueue.md`, section 2).

    discount = the practical's part
               + the country's industrial_efficiency modifier
               + what its technology gives as ic_efficiency
               never more than 0.99

    the practical's part, with a = the country's ability in the type's practical, less 5:
        a below nothing:   a * 0.10                 so a point short makes a tenth dearer
        a up to 1:         a * 0.05
        a above 1:         the rough square root of a, * 0.05

    cost = build_cost_ic * (1 - discount)
           a reserve: * (1 + reserves_penalty_size)

    days = build_time * (1 + unit_recruitment_time - discount) * (1 + the speed modifier of its kind)
           the two brackets multiplied out are never taken as under 0.05; whole days

- The practical is the one the unit type names as `on_completion`: what building it
  teaches is what makes it cheaper. `confirmed`
- **The square root is the original's own rough one**, the same routine and the same
  roughness as research uses (`research.md`, section 7): 5 gives 2.25. `confirmed`
- **Industrial efficiency, the technology, takes 2.5% a level off both the cost and the
  time of every unit.** Its key is `ic_efficiency`. (measured: it is what makes section 5
  exact. Without Germany's four levels its share runs out four wings early and every build
  time is a tenth long.) That it is this key and no other is `inferred` from that.
- `unit_recruitment_time` is the training law's: +0.10 for advanced training.
- The speed modifier is `rocket_build_speed` for a type with `is_rocket`, else
  `tank_build_speed` for one with `is_armor`, else `air_`, `naval_` or `land_build_speed`
  by its branch. `confirmed`
- `build_cost_ic` and `build_time` are the brigade's own, with its technology in them.

The original has two more terms, one in each: what technology has done to the cost and to
the time of that one unit type. No technology file of Their Finest Hour sets either, and
which keys would is not read. *Ours:* both are nothing.

## 7. What a new game starts a country with

Supplies and fuel for as many days as it is given of resources (`stockpile.md`, section 2):

    supplies = days * the supplies all its units use in a day
    fuel     = days * the fuel all its units use in a day

where a day's use is section 1 over every unit, the fuel counted whether or not anything
is moving. `confirmed` **It is reckoned before the country's units are given their leaders**,
so with none but those their order of battle names (`leaders.md`, section 5). `confirmed`

Built, and done once the units have been made and before the leaders are chosen, with the
potency of section 1.

(measured, and only partly) All the fuel in the provinces a country holds two days in,
less two days of its refining, is this figure to the thousandth for 18 of the 76 countries
that have a capital to themselves, and within a few units for about twenty more.
Twenty-five hold 2% to 19% more than the rule gives, Germany among them at 9%, and that is
not explained: see the end.

## 8. The needs the sliders show

| share | its need, in IC |
| --- | --- |
| supplies | the IC that would make the supplies the units asked for today: what they asked, over `IC_TO_SUPPLIES * (1 + the law's bonus + the technology's)` |
| reinforcement | section 4 |
| upgrades | section 5 |

Production's need is counted the same way - at the end of its share's every run, entering's with
nothing to spend among them, and when a line is ordered or cancelled - and is in `buildqueue.md`,
section 4; on a new game's first screen it is the second day's too, and on a loaded game's entering's.

`confirmed`. "The units" are the ones drawing on the country's ground, whoever they belong
to (`supply.md`, section 13). The original adds the supplies traded away and takes off those
traded for and tribute; here the only trade is what a country pays a friend for its units on
the friend's depots, and that is added and taken off the same way. There is no tribute.
What the units asked for also carries **the carriage out to the country's provinces** -
each it holds with a unit in it adds its hops to its depot times a load's loss there
(`supply.md`, section 11).

**On the first screen** (`confirmed`, read 2026-11-15; `reversing/findings/FINDINGS-laterstart.md`,
section 18): the three needs are **the second day's**, at every start. What the units asked for is
written only in a day - cleared at its head and added to by the units' draws and the carriage - and
by nothing in entering a game; the reinforcement and upgrade needs are counted by those two shares
each time they run, spending or not, and entering runs every share with nothing to spend - but for a
new game **before** its two days, whose own runs count them again, and nothing after the days does.
A **loaded** game is the other way round: neither figure is in a save, so its supply need is nought
until the first midnight - nothing in entering writes what the units asked for - and its
reinforcement and upgrade needs are what entering's run with nothing to spend counts as the save
stands (`CProduction.RunNeedsAtLoad`; the doctrine level that run moves on is not built for a load,
section 5). Until 2026-11-15 all three were estimated over the second day's on entering, the supply
need from a day's use alone; the reinforcement need of the new headquarters the AI makes in the
first hour, which the second day never saw, was in it (Britain's and Ireland's 0.200 and 0.100 at
every start, nought now). *Measured* (`firstdays country=`, no save keeps them): the supply need at
1936 for Germany 12.288, Britain 20.871 and Ireland 0.148, where the estimate gave 10.817, 17.424
and 0.110; at 1941.12.11 Germany's 129.496 where the estimate gave 55.737, its divisions far into
the Soviet Union adding the hops of the carriage.

## 9. Against the savegames

`tools/OpenHOI3.SaveCheck` makes the units as a new game does, gives each country the
laws, technology and shares its save shows, runs two days of sections 3 to 5 with nothing
charged, and sets every brigade beside the save's.

| | agree |
| --- | --- |
| strength of each brigade, in a unit the save shows supplied | 3,638 of 3,638 |
| strength, in a unit the save shows short of supplies | 9 of 68 |
| level of each doctrine | 10,235 of 10,235 |
| level of each piece of equipment | 26,648 of 26,648 |
| how far each upgrade has got, the player's country | 1,469 of 1,469 |
| how far each upgrade has got, the others | 22,993 of 25,179 |
| supplies a capital's garrison uses, the same units and no leader | 2 of 2 |
| the same, where the save has given a unit there a leader | 27 of 28 |
| the same, where the save has a unit the files do not give | 33 of 35 |
| the same, where the save shows a unit given nothing | 0 of 6 |
| fuel at the start, a country with a capital to itself | 18 of 76 |
| fuel at the start, two countries sharing a capital | 0 of 2 |

**The laws the two days ran under are not always the save's.** The original's AI goes on
changing laws after the last day is run (`production.md`, section 7), and two groups of
laws set what an upgrade costs and how long it takes: the training law and the industrial
policy. So the tool runs each country with the save's law and with its history's, for each
of the two, and judges it with the pair its upgrades fit best. Germany fits its history's
training law - the one its units were made under - and 34 other countries fit a law out of
their history for one group or both.

The 59 brigades whose strength disagrees are all in units the save shows as having
received less than all their supplies - Bhutan's, Iraq's, Mongolia's, Nepal's, Tannu
Tuva's and Yemen's, and eleven of Italy's - where rotation ran faster for it. This check's
two days are run with every unit supplied; the supply network has a check of its own
(`supply.md`, section 17).

## What is not built

- ~~**Leaders.** The three terms they add to what a unit uses (section 1).~~ Built on
  2026-10-11: `leaders.md`, section 8.
- **An air unit in the air.** A land unit on the march burns fuel, and so does a fleet away
  from its port; nothing flies yet.
- **An AI to move the shares.** Only the player's reinforcement and upgrade shares are
  anything but what they started as.

The build queue, which section 6 is for besides, is built: `buildqueue.md`.

## What is not established, in one place

- **Why twenty-five countries start with more fuel than the rule gives**, and in bands:
  thirteen hold 13% to 19% more - France, Italy, the United Kingdom and the United States,
  but also Siam, Lithuania, Turkey and Czechoslovakia - seven hold 6% to 9% more, Germany
  and Japan among them, and five 2% to 4%. It does not follow the size of the country and
  it is not the fleets, since landlocked countries are in the top band. What they have in
  common was not found. Another twenty or so are off by a few units, which is more likely
  the tool's estimate of two days' refining than the rule.
- **What the original's AI did to the training law before the first screen.** Two days of
  upgrades fit the history's law for Germany and the save shows another; that it changed
  after the last day is `inferred` from that.
- **Which technology keys change one unit type's cost or time** (section 6).
- **What the pride of the fleet's bonus is doing in reinforcement** (section 4).
- **The country's second list of units**, which both shares walk beside its own:
  units lent to it are the obvious reading, and it is not read.
- **Whether `ic_efficiency` is the key** behind the technology's part of the discount, as a
  reading and not only as a fit.
