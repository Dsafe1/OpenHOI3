# The build queue: ordering a unit, building it, and what comes out

How the original builds units, written as rules this project can implement. No addresses
and no offsets. Each rule says how sure it is - `confirmed`, `likely`, `inferred` in the
fact base's vocabulary - or **measured**, meaning checked on 2026-10-08 against savegames
of one unmodded game played as Germany: the save of 1 January 1936, two days in, and four
later ones up to 20 April. Where this project chooses its own behaviour the paragraph is
marked **Ours**.

Sources: `reversing/findings/FINDINGS-buildqueue.md` (read for this spec),
`reversing/findings/FINDINGS-production.md` (the day's loop),
`reversing/findings/FINDINGS-oob.md` (what `status` is),
`reversing/findings/FINDINGS-unitdaily.md`, section 6 (what a unit costs and takes).

The production share and what a point of IC costs are `production.md`; what a brigade
costs and how long it takes, before the queue's own term, is `upkeep.md`, section 6; the
units themselves are `units.md`.

All arithmetic is in thousandths and every division truncates, except where it says the
shares' kind of number (`numbers.md`, section 2).

## 1. A line

A country's queue is a list of lines, and **its order is all the priority there is**: the
first line is paid first. `confirmed`

A line builds one unit, or several of the same unit one after another. It holds:

| | |
| --- | --- |
| what the unit is made of | one or more brigades, ships or wings, each with a type, a name and **a model**: the level it will have of each technology of its type |
| `size` | how many of the unit are still to come |
| `cost` | the IC a full day's work takes |
| `duration` | the days of work one unit takes |
| `progress` | the days of work done on the one being built |
| `status` | how much of a day it was paid the last time the queue was worked through: 1 for all of it |
| `manpower` | the men one unit took when it was ordered |
| the experience gathered, and how much of the building that was | section 4 |
| whether they are reserves, and the share of a brigade that leaves | 1 plus the country's `reserves_penalty_size`, kept from when the line was queued |

A unit is all land, all sea or all air, and what it is - a division, a fleet, an air unit -
goes by its first brigade. `confirmed`

## 2. What a line costs and how long it takes

For each brigade of the unit, with `levels` the sum of the levels its model gives it:

    cost = build_cost_ic * (1 + 0.01 * levels - discount)
           reserves: * (1 + reserves_penalty_size)
    days = build_time * (1 + 0.01 * levels + unit_recruitment_time - discount) * (1 + the speed modifier of its kind)
           the two brackets multiplied out never under 0.05; whole days

with the discount, the modifiers and the brigade's own figures as `upkeep.md`, section 6,
has them. `confirmed`

- **Every level a brigade is built with, of any of its technologies, makes it a hundredth
  dearer and a hundredth slower to build.** It is the one term the queue has that an
  upgrade does not: upgrades are priced with `levels` at nothing. `confirmed`
- **A unit of one brigade costs what the brigade costs a day and takes what it takes.**
- **A unit of several takes as long as its slowest brigade, and costs a day what would pay
  for every brigade's own days over that time:**

      duration = the most of the brigades' days
      cost     = (the sum of each brigade's cost * its days) / duration

  Infantry at 2 IC for 100 days with artillery at 3 IC for 60 is `(200 + 180) / 100`,
  3.8 IC a day for 100 days. `confirmed`

**When it is worked out.** Not daily. `confirmed`

- when the line is queued;
- for every line left in the queue, on a day anything in it is finished;
- for a line going on to the next unit of its series;
- for every line of a new game (section 5);
- **for every line of a country, on any change of its laws.**

**The work done on a line is left alone** when it is priced again: a change to a slower
training law makes a nearly finished ship less nearly finished, and one to a faster law
can leave a line with more days done than it takes, which finishes it the next day.

So what a practical's decay does to a cost reaches the queue late. (measured: Germany's
destroyers cost 4.860 a day from January until a cruiser is finished in March, and 4.941
from then - its destroyer practical having fallen from 5 to 4.811 in the two months.)

(measured, and exact) All thirty lines of the start save have this cost and these days
under the laws the save shows. The Graf Spee is built with twelve levels between its seven
technologies: `6 * (1 + 0.12 + 0.05)` is its 7.020 IC and `280 * (1 + 0.12 + 0.05 + 0.20)`
its 383 days, Germany being on a policy that takes 15% off industrial efficiency with four
levels of the technology that gives 2.5% back, and on specialist training.

*Ours:* the original has one more term in each, what technology has done to that one unit
type's cost and time, which no file of Their Finest Hour sets (`upkeep.md`, section 6).
And it has a second way to price a line, for a brigade in the field being turned into
another type: not built.

## 3. Ordering

    for each brigade of the unit:
        men = build_cost_manpower * (1 + reserves_penalty_size for reserves)      the bracket never under 0.05
    the line's manpower = the sum
    the country's manpower -= that * the number ordered                           not below nothing

`confirmed`

- **The men are taken when the unit is ordered, for the whole series at once.** The IC is
  paid day by day.
- **Reserves take the share of their men the conscription law leaves.**
- The line goes to the back of the queue, priced as section 2, with no work done and a
  `status` of 1.
- **The original's command does not ask whether the country has the men**: the pool stops
  at nothing. **What refuses an order is the window it is given from**, which disables its
  button: `builders.md`, sections 2, 4 and 6.
- **A brigade ordered has the country's own level of every technology of its type** - and a
  ship the level its builder was set to of each, anything from nothing to the country's
  (`builders.md`, section 7). `confirmed`
- **A unit of several is named as it is ordered, and every brigade, ship and wing**:
  `builders.md`, section 8.

*Ours:*

- **An order is refused** if the country has not the men for the whole series, if a type
  does not exist, if its file says `is_buildable = no`, if it names in `usable_by` countries
  that do not include this one, if the country has not unlocked it or may have no more of
  it, if the brigades are not all of one branch or are more land brigades than a division
  may have, or if equipment is asked for that is better than the country's. The original's
  command refuses none of this; its windows do (`builders.md`, section 9).
- The original also takes a province and a unit to join, and can build a unit at once for
  nothing, which is how its history files make a unit. None of the three is built.

## 4. A day

The production share - the third of the six, after consumer goods - with the IC it is
given (`production.md`, section 1) in the shares' kind of number. For each line in order:

    need = the line's cost, turned into the shares' kind of number
    if nothing is left:        the line is paid nothing
    else
        share  = what is left / need, no more than 1
        wanted = share * need
        if the stockpile can pay for `wanted` IC (production.md, section 2):
            the resources are charged
            what is left -= need               the whole of it, paid in full or not
            the line is paid `share` of a day
        else
            the line is paid nothing, and what is left stays as it was

`confirmed`

- **A line is paid a whole day or what is left, and the line the share runs out on is the
  last to get anything.** What comes off is the day's whole cost, so there is no sliver
  for the line after it.
- **A line the stockpile cannot pay for is skipped that day, and a cheaper line after it
  can still be paid.**
- **The share's need** - what its slider shows - is the sum of every line's cost, each
  turned into the shares' kind of number before it is added.

**When the need is counted** (`confirmed`, read 2026-11-15; `reversing/findings/FINDINGS-production.md`;
`FINDINGS-laterstart.md`, section 18). The share keeps the figure and counts it again in exactly these
places: at the end of each day's share, after anything finished has had the queue priced again; when a
line is ordered - a unit, a single unit, a building, a convoy, a brigade's change of type - or cancelled;
and at the end of entering's run of the shares with nothing to spend (`upkeep.md`, section 8). [exe]
**Nothing else counts it.** So a line priced again without one of those - by a change of law (section 2)
- leaves the need at the queue's old prices until the next midnight or the next order, and a queue
emptied by annexation or exile leaves the need as it was. Entering's run, for every country and for a
new game and a loaded one alike, takes out the building lines that may no longer be built, as the head of
a day's share does, and counts the queue; nothing is paid and nothing moves on, a line with nothing to
spend being passed over before it could be paid or finish. [exe]

So **a new game's first screen shows the second day's need**, the entry's own count having come before
the days - or a later order's - at the second day's prices, and the changes of law the install's Lua
made as the game was set up, run after the days (`politics.md`, section 12), price the lines again
without counting them. *Measured* (`firstdays country=`, no save keeping the need): at 1936 Germany's
need is 13.984 and Britain's 13.573 on the first screen, where the queue at the prices the start save
writes - every line's price the save's, 30 of 30 - is 16.159 and 15.523; until 2026-11-15 the change
of law counted it, and the first screen showed those. At the three later starts every country's need
is its queue at the save's prices, no change of law there moving a price (55, 61 and 45 lines of 55,
61 and 45 priced as the save). **A loaded game's** need is its queue as the save prices it, less any
building line its entry takes out: the Ireland game's saves of 6 September 1939, 23 May 1940 and 16 July
1941 give Germany 203.881, 228.526 and 272.038, and Britain 175.828, 207.889 and 243.138, no line taken
out (`load ... needs=GER,ENG,IRE`). That a change of law leaves the need stale is the original's and
kept (`docs/enhanced.md`).

**Being paid** `amount` of a day, in thousandths:

    fraction  = amount * 1000 / duration                       how much of the whole building that is
    the experience gathered += rate * fraction / 1000
    the building counted    += fraction
    progress += amount
    status    = amount, or 1 for a whole day, or nothing for a line with no cost that was paid less

where `rate` is the country's `unit_start_experience`, and for reserves that times the
line's share of a brigade. `confirmed`

**So a unit is built with its country's starting experience at the rate of each day it was
paid for.** A change of training law half way through shows in what comes out.

A line with `progress >= duration` after its day delivers its unit (section 6).

The original also spends, before any of this, the IC it was lent, which costs no
resources. *Ours:* there is no lend-lease.

(measured, and exact) Two days of this on Germany's three ships give the start save's
`progress`, experience, building counted and `status` on all three. And with the queue's
13.982 IC in, **Germany's industry used 121.187 IC on the save's last day, which is the
save's own figure to the thousandth**: 44.823 on consumer goods, 31.191 on supplies, and
31.191 on upgrades. Over the four later saves, 31, 28, 31 and 19 days apart, every figure
of Germany's lines agrees: 10 of 10.

## 5. A new game

An order of battle may hold `military_construction` blocks: units being built as the game
starts (`units.md`, section 3). Thirty of them in the base game, all of one ship or wing but
Belgium's, which is one brigade.

    military_construction = {
        country = GER
        heavy_cruiser = { name = "Graf Spee" historical_model = 3 }
        cost = 5.60
        progress = 0
        duration = 6
    }

**As the game starts, each line is priced as section 2, and what its file called its
duration is taken as the days it still has to go:** `confirmed`

    progress = the days it takes - the file's duration            not below nothing

- **The file's `cost` and `progress` are thrown away.** The line costs what the country's
  laws and technology make it cost.
- A line with further to go than it takes starts from nothing.
- **A brigade's `historical_model` is turned into its model then**: the model's level of
  each technology of its type, in the type's order - and *nothing* for a technology the
  model does not list, so that the levels after it move up a place. `confirmed`, and it is
  not what the original does for a unit in the field (`units.md`, section 4), where an
  unlisted technology is left alone. No model of the base game's thirty lines leaves one
  out.
- **A file's line of reserves is not given its law's share of a brigade**: it is queued
  before its `is_reserve` is read, so it is priced as reserves, and gathers experience and
  teaches its practical as regulars would - unless the file gives `factor`. `confirmed`,
  and (measured) on Belgium's, which gathers its experience at the full rate.
- **A file's line builds one unit**, whatever `size` it gives. `confirmed`
- It takes numbers as `units.md`, section 3, has them: one for the line, one for its unit,
  two passed over, one a brigade.

(measured) The Graf Spee's file says 6. By Germany's history laws it takes 313 days, and
two days in the save has it at 309. Germany's other two, with 61 to go of 206 and 244, are
at 147 and 185.

**The laws a line is priced under as it starts are the ones the new game brought forward**
(`politics.md`, section 13) and the ministers its history gives. The Lua's changes of law
come after the two days and price the line again, the work done left alone (section 2;
`politics.md`, section 12). `confirmed` for the order, and (measured) 30 of 30 lines start
where the start save's are, less at most two days' work (section 9).

*Ours:* built as read, every country alike, the player's too.

## 6. Finishing

    if the country is not the rebels and owns no province:   the line is dropped, and nothing made
    what was never counted of the building is counted now:
        the experience gathered += (1 - the building counted) * rate

    the unit: a fleet, an air unit or a division, by its first brigade, with the line's number for it
    each brigade, with the number, the name and the model its line gives it:
        experience   = the greater of what its line gave it and the experience gathered, no more than 100
        strength     = max_strength * the line's share of a brigade, and no more than it may have
        organisation = default_organisation * (1 + its branch's organisation modifier)
        the country's practical for its type gains completion_size
            reserves: * the line's share ;  and as research gains one (research.md, section 6)
    the unit is called what its line is, for a division; else what its first brigade is

    the unit joins those waiting to be placed
    progress -= duration ;  size -= 1
    a line with more to come is priced again and, for reserves, takes its law's share afresh;
        a line for a unit of several takes a new name, as a new order is named (builders.md, section 8),
        and its brigades keep theirs

`confirmed`

- **A new brigade has its own organisation and nothing else**, whatever its country's
  officers and whether or not it is a reserve: it is given it before it is put in its unit,
  and the ceiling of `organisation.md`, section 2, counts neither for a brigade in no unit.
- **Each division of a series has a name of its own** (measured, 2026-11-02, not read): in the reference Ireland
  game Japan's series that gave "1 'Tokyo' Hoheishidan" went on as "7 'Asahigawa' Hoheishidan" and stands in the queue
  of 1 July 1937 as "12 'Kurume' Hoheishidan, size 2" - the line carries the name of the unit still to come - and the
  'Sendai' series gave 2 'Sendai', 6 'Kumamoto' and 11 'Zentsuji'. The names follow the country's list as new orders take
  them, which is what naming afresh from where the country left off gives. The brigades of all of them are "1st Inf
  Brigade", "2nd Inf Brigade" and the rest, unchanged. Until then every division of a series here took the line's one
  name. Whether a ship or a wing of a series is named afresh is not known: no save shows a series of them finished.
- **The practical is gained in full, once, when the unit is done.**
- **A series keeps what was left over** of the day that finished a unit.
- **A unit finished is not on the map.** It waits to be placed (section 7). While it waits
  it draws supplies (`upkeep.md`, section 2) and its brigades call for officers
  (`organisation.md`, section 1); it loses no men to rotation, and is neither reinforced
  nor upgraded.
- After any unit is finished, every line left is priced again (section 2).

(measured, and exact) The Graf Spee is finished in the middle of March. The 1 April save
has it waiting to be placed with an experience of 24.940 - the 3.040 the 1 March save
shows, fifteen more days at `25 * 0.002`, and `(1 - 0.154) * 25` for the days its file
started it with - a strength of 100, and **an organisation of 30, where Germany's officers
would allow it 42**. Germany's cruiser practical goes from 4.811 to 6.276 over the month:
1.6 gained, then April's decay. Four more units finished between the saves, in countries an
AI runs, have the experience this gives: 5 of 5.

**The unit's waiting place - its deployment - takes the number after the unit's**, which a
line passes over for it: a file's line, as above, and a line ordered in play here takes one
for itself, one for its unit, two passed over and one a brigade the same way (since
2026-11-05, `savegame.md` section 9; the original's own in the saves leave the two too). A
save writes the deployment's number, and the original finds a deployment by nothing else.
`confirmed`

*Ours:* the original gives the next unit of a series the number two after the last, which
collides with the first brigade's. Here the next of a series takes fresh numbers for its
unit, passing over two after it, and for its brigades. And a line may be
aimed at a unit in the field, which the finished brigade then joins: not built.

## 7. Cancelling, moving and placing

**Cancelling a line gives back the men of every unit of it still to come**, and nothing
of the IC spent. `confirmed`

**A line is moved** to the front of the queue, to the back, or one place either way. A
line already at the end it is sent towards stays. `confirmed`

**A finished unit is placed in a province** by a command of its own. The original asks:
`confirmed`

- for a fleet, a naval base there; for an air unit, an air base;
- that the province is in the same *area* as the country's capital - an area being a
  block of land under one owner.

**An area was read on 2026-10-09** (`supply.md`, section 2): the capital and every province
the country *controls* that touches it through such provinces, each with more than one level
of infrastructure. `confirmed`. Until then this took it for land the country owns and holds;
so land taken from an enemy and joined to the capital may now be placed in, and land behind
a belt of bad roads may not. *Ours:* a country that does not hold its own capital may place
nowhere. A fleet or an air unit placed takes the province as its base. A unit
placed answers to nothing above it.

**A unit placed starts at no organisation**: every brigade, ship or wing of it goes to nought as
it is put on the map, joining a unit or not, and climbs back by the hour from there - the waiting
unit's own figure is thrown away. The original sets each to its most instead where a byte of its
custom-game settings is set (`CUnitDeployment::DeployAt`), which nothing here can set, there being
no custom games. `confirmed` on the listing and the running game: Germany's 214.
Infanterie-Division, waiting at 50 in the July 1941 save, is at nought at 10:00 when the AI places
it and 0.186 an hour later, in every run - and so is held back at noon, where ours, placing it at
50, had committed it and sent it to 1306 (`reversing/findings/FINDINGS-liveread.md`, section 99).

**Or it joins a unit already in the field**, by the same command given that unit as its
target. `confirmed`, from `reversing/findings/FINDINGS-deploymenu.md`:

    the unit is placed in the province the other stands in
    if it has exactly one brigade, ship or wing, and is of the other's service:
        that one is taken out of it and added to the other, and the emptied unit is gone
    else:
        it is put under the other

So a ship ordered alone joins a fleet, a wing an air unit and a brigade a division by the
one rule, and a division is put under a headquarters. Who may join whom is the deploy
menu's to decide, and is in `mapunits.md`, section 6.

*Ours:* where the unit could not be placed in the province its target stands in, the
original places it in the nearest province of the capital's area where it could, and
then joins them. Here the command is refused: the menu lists only units standing where
the new one may be placed, so it takes a change between the listing and the order. And
the command asks what the menu asks, where the original's asks nothing (`builders.md`,
section 9, is the same choice). **But for a line dropped on the order of battle browser's row** (since 2026-11-13,
`mapunits.md`, section 6), which may name any unit of a higher level: then the command takes the original's way - the
nearest province of the capital's area where the target's will not do - and asks nothing more.

## 8. Ours

- **The queue is `OpenHOI3.Sim.Construction`**: `CBuildQueue` has the rules, a line is a
  `CConstruction` of `CBrigadeOrder`s, and four commands change it - order, cancel, move,
  place. `CDeployment` has where a finished unit may go and whom it may join.
- **Only the player's queue moves by anything but the day.** There is no AI, so no other
  country orders, cancels or places anything; a unit one of them finishes waits for ever.
- ~~**Nothing calls the pricing on a change of law**~~ - a change of law prices the queue again
  (`politics.md`, section 9), a new game's Lua changes among them, after its two days.
- ~~**Convoys are not built.**~~ The original's queue holds three kinds of line, and since 2026-10-15 this one holds all
  three: units, buildings (`buildinglines.md`) and convoys (`convoylines.md`). A convoy line is priced once, when it is
  ordered, and passed over by every pricing of the queue after.

## 9. Against the savegames

`tools/OpenHOI3.SaveCheck` reads the lines as a new game does, gives each country the laws
and the technology its save shows, and compares. The start save:

| | agree |
| --- | --- |
| each line's number, its unit's and each brigade's | 30 of 30 |
| the model each brigade will be built with | 30 of 30 |
| what a line costs a day, under the laws the save shows | 30 of 30 |
| the days a line takes, under the laws the save shows | 30 of 30 |
| progress, experience, building counted and `status` two days on: the player's country | 3 of 3 each |
| the same: the others | 4, 12, 12 and 14 of 27 |
| where a line stood as the game started, by the save's less at most two days' work | 30 of 30 (16 until 2026-11-14) |
| what the player's industry used on the last day, the queue with it | 1 of 1 |

and, as `OpenHOI3.SaveCheck research <install> <earlier> <later>`, each pair of later
saves run forward from the earlier with every line paid in full, for the countries whose
lines were:

| | agree |
| --- | --- |
| progress, experience and building counted, over 31, 28, 31 and 19 days: the player's country | 10 of 10 each |
| the same: the others | 26 of 26 each |
| cost and days at the later save: the player's country | 10 of 10 each |
| the same: the others | 24 and 25 of 26 |
| the experience of a unit finished in between | 5 of 5 |
| a finished unit waiting to be placed: strength, and organisation | 1 of 1 each |

**Where the lines started is answered** (2026-11-14): six countries' lines - Britain's, the
United States', Japan's, France's, Belgium's and the Netherlands' - stood where neither their
history's laws nor the save's put them, because the original priced them under the laws its
own new-game pass had brought forward (`politics.md`, section 13), and the Lua's changes came
after the days. The check takes where a new game here put each line, which since the pass of
2026-11-02 is that. **What still disagrees is the AI's**: the countries an AI runs have had
their shares moved since the last day, so their two days' work on a line is not the check's.
A new game as a player gets it, with its own production minister, has every line where the
save does, all 30 (`OpenHOI3.SaveCheck firstdays`). The two lines whose price differs at a
later save are in countries where something else was finished in between - a building or a
convoy prices every line again too, and the check does not work those lines forward.

## What is not built

- ~~**The windows a unit is ordered from.**~~ Built 2026-10-08: `builders.md`.
- ~~**Anywhere to place a unit from.**~~ Built 2026-10-09: the deploy menu, `mapunits.md`,
  sections 5 and 6.
- **An AI**, to order, to place, and to change the laws that price a queue.
- ~~**Convoys**, the queue's third kind of line.~~ Built 2026-10-15: `convoylines.md`; buildings are
  `buildinglines.md`.
- **Turning a brigade in the field into another type**, and building a brigade for a
  division that exists.
- **Lend-lease**, and so the IC a queue may spend without resources.
- ~~**Unlocking unit types by technology.**~~ Built 2026-10-08: `builders.md`, section 2.

## What is not established, in one place

- ~~**What refuses an order** in the original.~~ The windows do: `builders.md`.
- ~~**How a builder window makes a brigade's model.**~~ `builders.md`, section 7.
- ~~**What bounds an area**, for placing.~~ Read, 2026-10-09: `supply.md`, section 2.
- **The second rule for placing** that one kind of session uses in place of the area.
- ~~**Why six countries' lines start where they do**~~ - priced under the laws the new
  game brought forward (section 5).
- **What a line aimed at a unit does**, past that it joins it.
