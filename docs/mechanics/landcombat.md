# A land battle

What starts a battle between land units, who stands on its front, what an hour of it does,
what ends it, and what it leaves behind. It was built in pieces: first **a battle exists** -
units meet, wear each other down, one side gives way, and the ground changes hands - and
then **what an hour's circumstances make of a unit's attack and defence**: the ground, a
river, a fort, the night, the weather, experience, being dug in, crowding (section 4). The
third piece put the battle **on screen** - its marker on the map and its window, which are in
`combatview.md` - and with it came **the two sides' tactics** (section 11), which the window
shows and which nothing picked before.

Everything is from `reversing/findings/FINDINGS-landbattle.md`, which joins three earlier
readings: `reversing/findings/FINDINGS-combat.md` for a shot and its damage,
`reversing/findings/FINDINGS-shatter.md` for shattering and falling back, and
`reversing/findings/FINDINGS-unitdef.md` for what a unit's brigades add up to. **It was read
out of the original and nothing of it was watched**: no savegame at hand has a battle in it,
and the original's rolls cannot be reproduced yet (section 8). Each rule says how sure it is.

In the code: `lib/OpenHOI3.Sim/Combat`, with the unit's summed figures in
`lib/OpenHOI3.Sim/Units/CUnitTotals.cs` and the generator in `lib/OpenHOI3.Sim/CRandom.cs`.

## In one paragraph

A land unit with somewhere to go looks, every hour, at the province it is walking to. With
an enemy standing there it starts a battle for that province **without entering it**: it
fights from where it stands, and its walk goes on underneath at a third of its speed, held at
the far end until the battle is over. The first hour, each side's units are put on the
**front** - the best first, until the battle's width is used up - and the rest are
**reserves**. Each hour both sides shoot, front against front, and then the damage is taken
off. A front unit with no organisation left falls back on its own. From the fourth hour a
side with nobody on its front has lost; its units fall back, the battle is gone, and the
winner, if it was the attacker, walks in the next hour and takes the province.

## 1. What starts a battle

`confirmed` (read; `FINDINGS-landbattle.md`, section 1) unless marked.

**The look ahead.** Each hour, before it moves, a land unit with a path looks at the first
province of it:

    an enemy land unit stands there that is not falling back
    and the unit has no battle for that province
    and it was given some of its supplies:
        not falling back:   an attack delay is running and it has not taken a step -> nothing
                            otherwise -> look at the province (below)
        falling back:       an enemy stands where the unit is too:
                                the province ahead is its own country's and a friend's land
                                unit holds it -> nothing;  otherwise the unit is lost
                            no enemy where it is -> it stops falling back, and looks

**The look where it stands.** Each hour, after it has moved, a land unit with no path, not
falling back and in no battle looks at the province it is in.

**Looking at a province** is, in order:

1. the unit has no battle there and one is being fought: the unit is **offered** to it;
2. it still has none: it **starts** one, if it finds an enemy;
3. the unit is in no battle at all, is not falling back, the province is land with an owner
   and **the unit stands in it**, and no enemy land unit stands there that is not falling
   back: **the province is taken** (section 7).

**Starting one.** The first unit in the province, in the order they came, that the unit may
fight is the defender; the unit is the attacker; and every other unit in the province is
offered. Nothing is started where the province already has a battle. Two units may fight when
they are two units of two countries at war, both land units, neither falling back.

*Ours:* the order of a province's units at the start of an hour is country by country, and
the original's is the order they arrived in, which no file gives.

**Being offered.** A unit joins the side whose enemies are all its own:

    every defender would fight it, and not every attacker   -> it joins the attackers
    every attacker would fight it, and not every defender   -> it joins the defenders
    otherwise                                               -> neither

A side with nobody on it would fight nobody. So a third country at war with both sides joins
neither, and one unit of a country on a side is enough for its countrymen to follow.

A unit that joins a side whose front has been set up waits in **reserve**.

*Not built:* a unit loaded on a transport is left alone by all of this; rebels have a side of
their own; and an undeclared war counts where it covers the province.

## 2. What a unit fights with

`confirmed` (`FINDINGS-unitdef.md`). A unit has one set of figures made from its brigades':

| | |
| --- | --- |
| added up | `soft_attack`, `hard_attack`, `defensiveness`, `toughness`, `officers`, `max_strength`, `combat_width`, `build_cost_manpower` |
| averaged, over all its brigades | `softness`, organisation |
| the best brigade's | `armor_value`, `ap_attack` |

**`toughness` is added in twice.** It is the original's, and looks like a slip; a unit's
toughness is double the sum of its brigades'.

A brigade that holds no frontage - a headquarters, engineers, anti-air - stops counting
towards the sums once it has under 0.1 strength or under 1 organisation (`units.md`); it
still counts in the averages.

**A unit can fight** while its brigades that hold frontage average at least 0.1 strength and
at least 1 organisation. A unit with no such brigade cannot: a headquarters never stands on
a front.

## 3. The front

`confirmed` (`FINDINGS-landbattle.md`, section 2) unless marked.

**How wide the battle is.** One figure for both sides:

    width = what the two tactics add                        (section 11)
          + BASE_COMBAT_WIDTH for the first province the attackers come from
          + ADDITIONAL_COMBAT_WIDTH for each further one

10 and 5 in the base game. An attacker's province is the one it is in - or, for one already
standing in the battle's own province, the one it was in before.

**How much of it a unit takes.** Its brigades' `combat_width` added up, times one plus its
country's `combat_width` modifier, and never less than 1.

**The first hour** the battle is fought, before anyone shoots, each side's front is set up:

    a unit that cannot fight                 -> reserve
    the rest, ordered, the largest first:
        while width - the front's widths >= 0:    onto the front
        whoever is left                           -> reserve

**The test is made before a unit is added**, so the front may end one unit over: with a
width of 10, three divisions of four brigades all stand.

The order is by

    attacker:  (soft_attack + hard_attack + toughness     + 1) x average organisation x strength
    defender:  (soft_attack + hard_attack + defensiveness + 1) x average organisation x strength

and of two units with the same figure the one that joined first is first. `likely` for the
defender's: its terms were read off the decompiler alone. *Not built:* the defender's adds its
leader's skill.

**Every hour**, as the first thing a side does before it shoots - so the attacker's front is
seen to before anyone has shot, and the defender's after the attacker has:

1. **a front unit that can no longer fight falls back** out of the battle (section 7);
2. with the front over the width, and still over it **without its last unit**, that unit goes
   back to the reserves - one an hour;
3. otherwise, while there is room and there are reserves, **one comes up** if any may.

**Which reserve comes up.** Every reserve that can fight rolls once, in a battle past its
first hour - **nobody comes up in the first**:

    chance  = (0.010 + the country's reinforce_chance technology
                     + its attack_ or defend_reinforce_chance modifier)
              x average organisation / 100                          in thousandths, cut
    it may come if  roll(100) <= chance x 100, cut to a whole number

and of those that may, the best scored comes:

    score = (soft_attack + hard_attack + defensiveness + toughness)
            x (average organisation / 100) x (strength / 100)
    a thousandth of that for a unit from a province its side already fills the base width from

So with nothing but the base, an infantry division's chance is one in a hundred an hour; the
technology (0.05 a level in the base game) and a minister (0.1) are what make reserves
arrive. The province a unit "is from" is taken differently here from the width: the one it
was in before, for a unit **outside** the battle's own province. As read; it looks odd.

*Not built:* a division whose corps headquarters is in its province or the next adds the
corps leader's skill to its chance.

**The maintainer's account of it agrees**, 2026-10-09, from playing the original: "As far as
I know all units which attack at the same time get into the combat, unless the width is
exhausted, then they go into reserve. If a unit joins later then it is put into reserve, and
it has a chance to join each tick (or day). I think that chance is called the 'combat
reinforcement chance'. The order in which they join from the reserve is therefore based on
that chance." Each of those is a rule above: the front is filled once, in the first hour; a
unit that joins a battle already begun waits in reserve; and it comes up by a roll against
its chance - every hour, as read, not every day. What he does not speak to is the order the
first hour puts units on the front in, and the score that picks between two reserves whose
rolls both let them; those rest on the reading alone.

## 4. The hour

`confirmed` (`FINDINGS-landbattle.md`, section 3). The battles are fought **last in the
hour**, after every unit has moved and every unit's organisation has been seen to, each battle
in the order they began. A battle seen for the first time has its fronts set up, and then, in
this order, attacker before defender at every step:

0. **the tactics are picked** where they are due (section 11): four rolls;
1. the count of rounds goes up;
2. the first round only: one roll of ten for each side;
3. **one roll of ten for each side**, every round. They are rolled and kept; what the
   original does with them was not found, and nothing here reads them;
4. **what the hour's circumstances make of every unit** (below), and each starts the round
   with no defence spent;
5. **the attacker shoots, then the defender** (section 5);
6. the damage is settled, the attacker's units and then the defender's (section 6);
7. **from the fourth round**: if the attacker has lost, the battle is decided against it and
   its units fall back; otherwise the same for the defender. Either way the battle is over;
8. otherwise the battle's hour counter goes up.

**The attacker is asked first**, so where both fronts give way in the same hour the attack
has failed. A battle is never decided before its fourth round, whatever the sides.

*Not built:* each country on a side gains `LAND_DOCTRINE_INCREASE` of a practical each
round; the war's date of last action is set.

### What the hour makes of a unit

`confirmed` (`reversing/findings/FINDINGS-combatmods.md`, section 5;
`FINDINGS-landbattle.md`, section 8) unless marked. Every round each unit's list of what is
for and against it is emptied and made again, and each thing on it is **multiplied in**:

    attack  = attack  x the greater of (1 + its attack figure)  and 0.01      cut
    defence = defence x the greater of (1 + its defence figure) and 0.01      cut

A unit's **attack** multiplies how many shots it fires, and its **defence** how many shots it
can meet (section 5). No single thing leaves a unit under a hundredth of what it had. On land a
thing's two figures are the same but for the weather and the night. In the original's order,
which is what the last thousandth depends on:

| | who | how much |
| --- | --- | --- |
| **a river** | an attacker whose way into the province crosses one | `RIVER_CROSSING_PENALTY` + the unit's `river` attack figure, where that is below nothing |
| **a landing** | an attacker whose way is a strait | `AMPHIBIOUS_LANDING_PENALTY` + the unit's `amphibious` attack figure, where below nothing, and no worse than -0.99 |
| **forts** | an attacker | the province's `fort_level` - its `coastal_fort_level` for a landing - in **whole levels** x `BASE_FORT_PENALTY`, + the unit's `fort` attack figure; never in the attacker's favour |
| **several battles** | a defender in more than one | -`MULTIPLE_COMBATS_PENALTY`, once however many |
| **experience** | every unit | a hundredth for each point of its brigades' average experience (a pride of the fleet counting for `PRIDE_BONUS_EXP` more) |
| **combined arms** | a unit whose mix of brigades counts | what the mix is worth (`builders.md`, section 6) |
| **dissent** | every unit | -`COMBAT_DISSENT_IMPACT` x its country's dissent / 100 |
| **lack of supplies** | a unit given less than all its supplies | -`COMBAT_SUPPLY_LACK_IMPACT` x the share it went without |
| **dug in** | a defender | its levels x `DIG_IN_FACTOR` |
| **crowding** | every unit of a crowded side | below |
| **the ground** | every unit | the terrain's `attack` for an attacker and its `defence` for a defender, + the unit's own figure for that terrain |
| **the weather** | every unit's **attack** | below |
| **the night** | every unit's **attack** | `BASE_NIGHT_PENALTY` + the unit's `night` attack figure, where below nothing |
| **envelopment** | a defender attacked from more than two provinces, on anything but land with no land beside it | -`ENVELOPMENT_PENALTY` for each direction past the second |
| **its own ground** | a unit fighting in a province that is its country's core | the country's `territorial_pride` |

- **A unit's own figure** for a terrain, a river, a fort, a landing or the night is the
  average of its brigades' (`units.md`).
- **The weather and the night are against the attack only**: a unit shoots worse and stands as
  well. Read out of the original again for this, which corrected the fact base; and
  `reversing/findings/FINDINGS-combat.md` had watched the night do it.
- **Crowding.** The directions of a battle are the provinces **the attackers stand in**, for
  both sides:

      n = the side's front + its reserves that can fight - 3 x directions
      what is left = (1 + BASE_STACKING_PENALTY) multiplied in once for each unit of n after the first, 99 times at most
      the penalty  = 1 - what is left

  So three units are free for each direction of the attack - to the defender as well - and in
  the base game each one more takes a tenth of what was left.
- **The weather** (`reversing/findings/FINDINGS-weather.md`), of the province fought over:

      rain = precipitation x (wind speed + 5) x LANDRAINIMPACTMODIFIER, at most LANDRAINIMPACTCAP
      cold, below LANDLOWTEMPERATURETHRESHOLD:
             -(the threshold + the temperature) / 20 x LANDLOWTEMPERATUREIMPACT
             x (1 - the country's winter_effects)
      heat, above LANDHIGHTEMPERATURETHRESHOLD:
             LANDHIGHTEMPERATUREIMPACT, x (1 - the country's jungle_effects) on ground
             with any humidity

  The cold's arithmetic is the original's own and jumps at the threshold. **Nothing makes
  weather yet**, so every province has none and this comes to nothing until it does.
- **Night** is by the province's own clock (`reversing/findings/FINDINGS-weather.md`;
  `reversing/findings/FINDINGS-temperature.md`):

      its hour = (its point's x x 24 / the map's width + 12 + the game's hour) mod 24
      how far north = the middle of its bounding box above the map's middle, over half the
                      map's height; nought anywhere south of the middle
      longer = how far north x the month's figure / 2, in whole hours
               (-6 -3 0 3 6 12 6 3 0 -3 -6 -12, January first; south of the middle, six months on)
      day from 6 - longer to 18 + longer, both hours counted; night otherwise

  So the game's clock is the clock of the map's middle; the equator and everything south of it
  has twelve hours of day all year; and at the far north June has no night and December one
  hour of day.
- **Digging in.** A land unit of a country at war with no path and in no battle gains a level
  a day while it has fewer than `UNIT_DIGIN_CAP`; each hour a unit with a path, or of a country
  at peace, has none.

*Not built:* shore bombardment (fleets do not fight); the difficulty setting, which is the
player's alone; mission efficiency; the leader, and every leader's trait that adds to a figure
above; encirclement, which wants the areas a country's ground falls into; paratroops; the
landing craft that ease a landing at a port; and three technology figures - one off the
crowding, one on the chance to avoid a hit, one on the dig-in cap.

## 5. The shooting

`confirmed` (`FINDINGS-combat.md`; `FINDINGS-landbattle.md`, section 3).

A side's hour takes **one draw of the game's generator**, which starts that side's own rolls
(section 8). Then each unit on its front, in order:

    target = one of the enemy's front units, by a roll over how many there are
    hard = hard_attack x (1 + the country's hard_attack modifier)
    soft = soft_attack x (1 + the country's soft_attack modifier)
    hard = hard x (1 - the target's softness)        soft = soft x the target's softness
    shots = (hard + soft) x what the hour makes of its attack
                                                     whole shots, and the rest a chance of one more

So **what a unit's attack is worth depends on who it drew**, and it fires all its shots of the
hour at that one unit. For each shot, while the target has a brigade left standing on the
hour's notes:

    defence = the target's defensiveness, where the shooter's side is attacking
              the target's toughness,     where it is defending
              x what the hour makes of the target's defence
    does it land:  the whole part of the defence is how many shots the target can meet this
                   hour, and the rest a chance of one more;
                   a shot it can meet  -> avoided at BASE_CHANCE_TO_AVOID_HIT, and one is spent
                   every shot after    -> avoided at CHANCE_TO_AVOID_HIT_AT_NO_DEF
                   (no chance above 99;  it lands if  chance <= roll(100))
    if it lands:
        organisation = roll(LAND_COMBAT_ORG_DICE_SIZE) + 1
        strength     = roll(LAND_COMBAT_STR_DICE_SIZE) + 1
            - the two ARMOR_ON_SOFT dice instead where the target's best ap_attack is under
              the shooter's best armor_value
        both x (1 + the shooter's side's tactic + its own strength)
        both x the two ARMOR_DEFLECTION_FACTORs where the shooter's best ap_attack is under
              the target's best armor_value
        organisation x LAND_COMBAT_ORG_DAMAGE_MODIFIER,  strength x LAND_COMBAT_STR_DAMAGE_MODIFIER

**The defence runs out.** 70 per cent of the shots a unit can meet are avoided and 48 per
cent of the rest, in the base game - so being outnumbered compounds: the second attacker's
shots find the defences already spent.

**A unit's own strength** adds a tenth to its damage for each tenth of full strength it has,
cut, and never less than one tenth: a unit at full strength hits **twice** as hard as the
roll, one at half strength one and a half times.

A unit meets two draws of the game's generator for every shot fired at it - the fraction of
its defence, then the chance - and the dice are the side's own.

*Not built:* a technology figure of the target's country is added to the better chance to
avoid a hit.

## 6. The damage

`confirmed` (`FINDINGS-combat.md`; `FINDINGS-landbattle.md`, section 3;
`FINDINGS-shatter.md`).

**A hit is spread over the target's brigades and only noted**: the strength by each
brigade's share of the unit's full strength, the organisation evenly, one part each.

**At the end of the round** each brigade's notes are taken off it:

    strength     -= the lesser of  notes / 100  and what it has
    organisation -= the lesser of  notes / 100  and what it has

**A brigade loses a hundredth of what its hits came to.** So one hit by a full-strength unit
with no tactic takes, in the base game, between 0.15 and 0.60 organisation off a unit, shared
out, and between 0.012 and 0.024 strength - which is what makes a battle a matter of days.

Then, for each unit, with `men` the strength it lost times a hundred:

- **manpower comes back**: `men x build_cost_manpower / max_strength x the country's
  casualty_trickleback / 100`, the unit's sums;
- **officers are lost**: `men x (the unit's officers / its brigades / 100) x
  OFFICER_COMBAT_LOSS`, off its country's pool and not below nothing;
- **it shatters** where it lost anything and what it has left, over the most its brigades
  have ever had, is under its threshold:

      threshold = the lesser of  1 - officer ratio / 2  and  0.5
                - average experience x 0.003

  A country with officers to spare has units that hold together longer; one short of them
  shatters at a half, like everyone else.

**A shattered unit** leaves every battle it is in and stands at its country's capital, under
its theatre. *Ours* in part: the original picks the capital, or a supply depot where the
unit stands - there are no depots yet - and a country with no capital loses the unit here.

*Not built:* the leader's skill takes a point a level off the threshold, and a technology
figure adds to the trickleback.

## 7. The end

`confirmed` (`FINDINGS-landbattle.md`, section 4; `FINDINGS-shatter.md`).

**A side has lost** when its units have next to no strength left in all, or **nobody is on
its front**, or its front's units average an organisation of one or less. Because a front
unit that cannot fight has already fallen back, the usual end is the empty front: the last
unit on it gave way and no reserve came up. **Reserves do not save a side by being there.**

**Falling back.** A unit leaves the battle, and:

- **an attacker stops where it stands.** It was fighting from the province next door; it
  loses its path and its order, and it is not marked as falling back;
- **a defender** goes to the province next to it that is worth most:

      a province it may stand in, by a way that is not closed, holding no enemy land unit,
      and - where an enemy holds it - only for a unit with an organisation of one or more
      worth = 1000 for each of its neighbours held by the unit's country or a friend
            +  500 for each held by a country that is neither friend nor enemy
            halved if any neighbour is held by an enemy
      it has to be worth more than 10; of two worth the same the first found is kept

  It is given the way there, set off as far along as the best of the defenders had already
  walked times `RETREAT_PROGRESS`, and marked as falling back.
- **with nowhere to go, or no way there, the unit is removed from the game.**

A unit falling back is nobody's enemy: it starts no battle, is not offered to one, holds no
ground, walks a tenth faster and is not slowed for want of fuel, and **stops falling back
where it arrives**. Its ordeal is in section 1: with enemies both where it is and where it is
going, it is lost.

**After the last hour** the battle is gone, and every unit standing in its province looks at
the province again (section 1). A winning attacker is not standing there: its walk was held
at the far end, so **it arrives in its next hour**, and looking at the province it now stands
in, takes it.

**Taking a province.** A country's land unit stands in a province with no enemy beside it:

    whoever holds the province is no enemy of that country   -> nothing changes
    its owner is no enemy either                              -> it goes back to its owner
    otherwise                                                 -> the country holds it

*Not built:* a puppet's conquest goes to its master; a government in exile's to the head of
its faction; and a province none of whose neighbours the country holds goes to a friend who
holds one. And what a change of holder does besides - the original rebuilds both countries'
neighbours on the spot; here the day's passes find it.

**Leaving by order.** A unit in a battle that is given somewhere to go leaves every battle it
is in, and where it was **holding ground** - in a battle whose other side still has somebody -
it is falling back from then on. A unit halted in the middle of an attack breaks the attack
off. The last unit of a side to leave decides the battle against that side, and the battle's
own hour ends it - not before its fourth. And each hour a unit leaves any battle for a
province that is neither where it stands nor next to it.

*Not built:* each defending front unit that falls back may damage the province's buildings
(`COMBAT_PUSHBACK_CHANCE_FOR_DAMAGE`, `COMBAT_PUSHBACK_DAMAGE`) - buildings have one level
here and no damage; the winners' relations with the province's owner improve by 5; the
on-actions `on_battle_won` and `on_battle_lost` fire; and the messages.

## 8. Chance

`confirmed` for the two generators (`reversing/findings/FINDINGS-revolt.md`, section 1.2;
`FINDINGS-landbattle.md`, section 3), *ours* for how they are started.

**The game has one generator**, and everything that rolls draws from it in turn: a Mersenne
Twister with two things about it that are the original's own - its shifts to the right keep
the sign, and its twist leaves alone a word that is both odd and negative. A draw is never
negative, so a roll is a plain remainder.

**A side's shooting has its own**: `x = x * 214013 + 2531011`, two steps a draw, the upper
fifteen bits of each multiplied together. One draw of the game's generator starts it each
hour, so a side's shooting costs the game one draw however many shots it fires - and the
rolls for whether a shot lands, which are the game's, cost two each.

**The original seeds its generator afresh every hour** from a figure the hour's tick
carries, which is how every machine in a game rolls alike.

*Ours:* the game holds a seed, and an hour's rolls start from the seed and the hour it is.
How the original turns a figure into its generator's first state was not read, and neither
was where its hourly figure comes from - so **an hour of the original cannot be rolled
again here**, and would need every roll that comes before the battles' in the hour as well:
the units' pass and the AI's both draw. A battle here can be checked for what it does on
average and for what does not depend on the dice, not hit for hit.

## 9. The attack delay

`confirmed` (`FINDINGS-landbattle.md`, section 6). A land unit has a delay, in hours, and a
count of the hours it has been attacking. Each hour, before it moves:

    attacking = it is in a battle, has a path, and has taken at least a step

    attacking:
        the count goes up
        no delay yet:   delay = UNIT_ATTACK_DELAY + roll(UNIT_ATTACK_DELAY_MODIFY) hours
                              + (1 - officer ratio) x 72 hours
                              - the country's attack_delay technology, in whole hours
                        and at least 12 hours
        delay above 1:  delay -= 0.25 + (1 + the country's combat_movement_speed)
                                        x 2 x the tactics' movement speed
                        and not under 1
    not attacking:
        delay -= 1, not below nothing
        in no battle, with a delay and a count:
            count <= UNIT_ATTACK_DELAY_PERIOD:   delay = delay x count / that period
            delay at least 1;  the count is cleared

So in the base game an attack starts a delay of **168 to 177 hours** - 24 less for each level
of the doctrine that shortens it - of which each hour of fighting takes a quarter of an hour;
and **an attack of twelve hours or fewer has its delay cut to that share** once it is over.
After that it runs down an hour an hour.

**What the delay does** (`movement.md`, section 4): a unit with a delay running that has not
taken a step does not start a battle and does not set off at all; a unit that arrives with one
running starts its next leg from nothing; a unit already walking keeps walking.

**The maintainer, 2026-10-09**, asked whether a delay really stops all movement: "Yes,
however it does not stop the movement into the province which was attacked. So the unit may
arrive with the cooldown still active." That is what the rule above comes to: the attacker
has walked while it fought, so it is under way when the battle ends, arrives, and stands
there until the delay has run out.

*Not built:* a paratrooper's delay is `PARATROOP_MISSION_DELAY`; a technology figure and the
leader's trait make the tactics' speed count for more.

## 10. Trying it, and what is not here

**A war.** There is no diplomacy yet, so `CStartWarCommand` puts two countries at war and does
nothing else - it is not a declaration: nothing is asked and nobody is called in. The client
takes it as `--war=GER:POL`. On the start date Italy and Ethiopia are at war, and nobody else.

**A trial.** `OpenHOI3.SaveCheck battle <install> [attacker] [defender] [seed] [name]` finds the
first division of the one country that stands next to a division of the other, sends it, and
says how the battle went, with what the hour makes of the two by day and by night. It is not a
check - there is nothing to set it against. On the start date, with a seed of 1:

    Italy at Ethiopia, Belet Uen at Gurbo:   across a river, one division against three: it
                                             attacks at 0.345 by day and 0.172 by night, and
                                             fails after 182 hours, 318 men lost against 32
    Japan at China, Tangshan at Tianjin:     into a town, at 0.69 by day against a defence of
                                             1.265: the defence breaks after 347 hours, the
                                             beaten division falls back, and Tianjin is held
                                             by Japan the next hour
    Germany at France, Karlsruhe at          across the Rhine into ten levels of fort, at 0.04
    Wissembourg:                             by day: it fails after 201 hours, 443 men lost
                                             against 12

It prints the two tactics whenever a pick changes either: mostly the plain attack against the
plain defence, with an assault or a shock for a day now and then. A sixth argument is a part
of the attacking unit's name, for a trial with a division other than the first found.

Two infantry divisions alike in everything, with nothing for or against either, fight for
about a week and lose a twentieth of their strength each. The maintainer, 2026-10-09: "yep,
that seems right".

**On the map.** Ground one country owns and another holds is **striped**, as the original's
is (`map.md`, section 10). The battle's marker, its window and its row in a unit's panel are
`combatview.md`. *Ours:* the readout of a selected province with a battle in it says who is attacking
whom, for how long, and how many stand on each front; it is scaffolding.

### Not built

- **Of what the hour makes of a unit**, the seven things section 4 lists under its table.
- **The weather itself**: its part in a battle is built and there is none to play it.
- **Leaders**, wherever one is named above - and with them most of what makes one tactic
  picked over another (section 11).
- **Experience** from a battle, and the practical knowledge a round gives.
- **Air units**: a battle in the air and bombing are their own pieces. Fleets fight one
  another at sea since 2026-10-10 (`navalcombat.md`), and have no part in a land battle
  yet: shore bombardment is not built.
- **Supply**: a unit fights and walks by the supplies `upkeep.md` hands it, which do not
  depend on where it is.
- **The AI**: nothing moves unless the player moves it.

### Not established

- **Nothing here was watched.** The order of a front and who comes up from reserve decide who
  is shot at, and so the course of a battle. The maintainer's account of how the original
  plays (section 3) agrees with how the front is filled and that reserves come up by chance;
  the order of the first hour's front and the score between two reserves are still the
  reading's alone.
- What the two rolls of ten a side makes every hour are for.

## 11. Tactics

Each side of a land battle fights by a tactic, which the battle's window names
(`combatview.md`, section 4). From `reversing/findings/FINDINGS-combatview.md`, section 11,
and `reversing/findings/FINDINGS-landbattle.md`, section 5. `confirmed` unless marked.

**What a tactic is.** A block of `common/combat_tactics.txt`, read in `CCombatTacticReader`:

| key | |
| --- | --- |
| the block's name | `tactic_assault`: the key of its name in the player's language, and what a technology adds to its weight under |
| `base` | its weight before anything is added |
| `aggressiveness` | how aggressive a leader it suits, from -1 to 1 |
| `countered_by` | the name of the tactic that counters it; it may be declared later in the file |
| `picture` | the file under `gfx/pictures/tactics` it is shown with |
| `trigger` | what must hold of a side for it to pick this |
| `combat_width` | what it adds to the battle's width (section 3) |
| `attacker`, `defender` | what it adds to the damage of that side's hits (section 5) - of either side's tactic: an attacker's assault may add to the defender's too |
| `movement_speed` | what it adds to the attackers' pace and to how fast their delay runs down (section 9) |

The tactics are numbered from 1 in the order of the file. A key that is none of these is
reported, as the original logs it. *Ours:* a tactic declared twice keeps its first
declaration; what the original does with the second was not read.

**When.** At the top of a battle's hour, before the round is counted: where either side has
no tactic, and again whenever the rounds fought so far are a whole number of
`TACTIC_SWAP_FREQUENCEY` hours - 24 in the base game. So both sides pick in the first hour
and afresh every day of the battle. Only where both sides still have a country.

**Who picks first.** Each side's highest ranked leader has a skill. The side with the
**lower** skill picks first, favouring nothing; the other then picks **favouring the tactic
that counters the first's**, by the difference in skill. With equal skill the attacker picks
first and the defender second, favouring nothing.

**A pick**, for each tactic whose `trigger` holds of the side:

    weight = base + what the technologies of the side's first country add to this tactic
    the one favoured:   weight x (1 + INITIATIVE_PICK_COUNTER_ADVANTAGE_FACTOR x skill difference)
    every one:          weight x (1 - AGGRESSIVNESS_SELECTION_IMPACT x d)
                        d = how far the tactic's aggressiveness is from the leader's, at most 1

and then one roll picks among the tactics that weigh anything, each as likely as its weight:
with the weights in the file's order, the first whose running total is above the roll's
remainder by the whole. A side for which nothing weighs anything has no tactic.

- **A pick makes two rolls**, so both sides' make four: one before the weights, which the
  original takes for judging the conditions, and the pick itself.
- **What a technology adds**: a technology names a tactic at its own top level,
  `tactic_assault = 3`, among its other effects, and a country's tactics weigh that much more
  for each level it has. `inferred` for "each level": it is what every other figure of a
  technology does, and how these reach the country was not read.
- **The leader's aggressiveness** comes from the stance of the headquarters above him, or from
  his own unit's setting. *Not built:* leaders; every side's skill and aggressiveness are 0.
  So the attacker always picks first, nobody is favoured, and a tactic with an aggressiveness
  of its own - a reckless assault - weighs half.

**The conditions** a `trigger` is made of, each judged on the side that is picking, with
`AND`, `OR` and `NOT` around them:

| key | holds when |
| --- | --- |
| `is_attacker = yes` | the side attacks |
| `frontage_full = yes` | the side's front takes **more** than the battle's width - a front that fills it exactly is not full |
| `reserves = yes` | the side has a unit in reserve |
| `has_armour_unit = yes` | a unit **on the side's front** has a brigade whose type says `is_armor`; one in reserve does not count |
| `skill_advantage = 2` | the side's leader has that much more skill than the other's, or more |
| `skill = 3` | the side's leader has that much skill, or more |
| `trait = panzer_leader` | not read; the side's leader has the trait, by its name |

Each of the first four may be written with `no`, and is then the other way about. *Ours:* a
condition that is none of these is reported and **never holds**, so that a tactic is not
handed out on the strength of a test nothing made. With no leaders, `skill` and
`skill_advantage` hold only where they ask for nothing and `trait` never.

**What two tactics do.** One tactic **counters** another when it is the one the other names
in `countered_by`. Whenever tactics are picked, four figures of the battle are made again
from nothing: each side's tactic adds its `combat_width`, `attacker`, `defender` and
`movement_speed` - **unless the other side's tactic counters it**, in which case it adds
nothing at all. Two tactics may counter each other, and then neither does anything.

In the base game, on the start date, that comes to this: an attacker fights by the plain
attack (a weight of 15, and a twentieth more damage) with the odd day of an assault or a
shock (1 each, more with the doctrines), and a defender by the plain defence; the tactics
that turn a battle - a breakthrough, an ambush, a counterattack - all ask for a leader.

