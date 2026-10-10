# A fleet's missions

What a fleet does under each of the orders that are more than "go there": reserve, patrol,
intercept, sortie, the raid on convoys and their escort - and the battle a raid fights. This is
the second piece of what stage 3 has left (the readme's *Next up*); the battle between fleets
is `navalcombat.md`, and the orders that only send a unit somewhere are `fleetsandair.md`.

Everything is from `reversing/findings/FINDINGS-navalmissions.md`, with the sighting from
`reversing/findings/FINDINGS-navaldetection.md` and what a convoy is from
`reversing/findings/FINDINGS-convoys.md` and `reversing/findings/FINDINGS-supplyconvoys.md`.
**It was read out of the original and nothing of it was watched.** Each rule says how sure it
is; what this project does of its own is marked *Ours*.

**It is written to do what the original does, its oddities with it**, and some of them are odd
enough to want a look in the original by somebody who can watch it. They are gathered in
section 12 and not tidied.

In the code: `lib/OpenHOI3.Sim/Missions`, with the raid's battle in
`lib/OpenHOI3.Sim/Combat/CConvoyRaid.cs`.

## In one paragraph

A mission is an order the fleet holds. Giving it starts it, and every hour, before the fleet
moves, the order is asked whether it is still going - which is where a mission does everything
it does. All of them do nothing while the fleet is in a battle, and send a fleet of the player's
that is too worn for its stance to the nearest harbour. A **patrol** goes to its province and
then round the sea provinces beside it, shadows a foreign fleet it sights, and every twelve
hours tries the convoys of the water it is in. A **convoy raid** makes the same round, shadowing
nobody. An **intercept** waits in port and goes for an enemy fleet it sees in its province or
beside it. A **sortie** sails to its province and turns for home on arriving, going for enemy
fleets beside its way. An **escort** sails up and down the stretch of a convoy's route within
its range. A **reserve** lies in its port and gives its sound ships for the worn ones of the
fleets beside it. A raid on a convoy is a battle: the convoy's transports and escorts become a
throwaway fleet, and five rounds of the battle at sea are fought against it on the spot.

## 1. What they share

`confirmed` (`FINDINGS-navalmissions.md`, sections 1 and 2) unless marked.

**Which may be given.** Patrol, intercept, sortie and raid ask one thing: the fleet is in no
battle, the province is **sea**, and it is in range (`fleetsandair.md`, section 3). Reserve and
escort have their own tests, in their sections.

**Stance and priority.** Every order has a stance - aggressive, defensive or passive - and a
priority from 0 to 4. The orders window starts at aggressive and 0; an order made with nothing
said is defensive.

**The hour.** Each mission's hour begins the same way:

    the fleet has no base                    -> the order is over
    it is in a battle                        -> nothing this hour
    it is too worn to go on                  -> it heads for a harbour, and nothing else this hour
    then the mission's own hour

(The reserve has a frame of its own, and the sortie and the escort put one test of theirs before
the third line.)

**Too worn to go on.** Asked only of a fleet whose country **a person plays**; nobody else's
fleet ever breaks off for damage here.

    limit: a quarter;  half where the stance is aggressive;  a tenth where it is passive
    a ship is worn where it has lost more than the limit of its full strength
    the fleet is too worn unless fewer of its ships are worn than are not

The three limits are plain numbers in the original, not the `*_STRENGTH_LIMIT` defines.

**Heading for a harbour.** Only a fleet in a sea province with no path: of the harbours - land
with a port and a naval base - held by its country or by any country of its faction, the one
nearest by sea to where it is, by safe water. Its base and its order stay as they are. **In port
nothing more happens**: it lies there under its order until enough of its ships are mended, and
then its mission's hour runs again from where it lies. *Ours:* which provinces count as a
country's harbours and in what order, the original keeping a list that was not read; here the
ones it holds, by number.

**Safe water.** A fleet on a mission finds its way as any unit does (`movement.md`, section 3),
but a step into a sea province costs **three times** as much where a province beside that one
has a naval base and belongs to somebody at war with the fleet's country, and three times again
for an air base. As the original has it the neighbours are gone through in order, and once one
is found to be an enemy's every neighbour after it counts as well, whoever holds it.

**Told to return to base.** Several missions tell the fleet to return to base: the order
remembers it, and the fleet is given the plain way home. **Nothing ever takes that back.** The
original keeps the mark in one place and looks for it, to clear it, in another, so for as long as
the order lives it is set - and intercept, reserve and sortie each do less once it is. What that
comes to is in their sections. `inferred`: saving and loading the original would clear it.

**The look for an enemy fleet**, which an intercept and a sortie take at a sea province:

    of the fleets there at war with this one's country
    that it sights (`navalcombat.md`, section 1 - the same test as for starting a battle)
    and that have no more power than it has:
        the one with the most strength in all its ships; the first of equals

- **The sighting is asked from wherever the looking fleet is.** Land a fleet lies in is known to
  its country at 9, which is a sighting with no roll: **a fleet in port sees every enemy fleet
  in the provinces it looks at**, and one at sea rolls for each.
- **Power** is, for each ship, a hundredth of its strength times a hundredth of its organisation
  times `(0.5 + sea_attack + sea_defence) x hull` (`reversing/findings/FINDINGS-power.md`).

## 2. Reserve

`confirmed` (`FINDINGS-navalmissions.md`, section 3).

**May be given** for a province with a naval base that the fleet's own country holds, in range,
to a fleet in no battle.

**Giving it** makes the province the fleet's base at once, and tells a fleet that is not there
to return to base.

**Each hour** the order goes on while the fleet has a base and either was told to return to
base or lies in the province with no path. So a reserve sent for from elsewhere keeps its order
whatever it does, and one that was already in port loses it by leaving.

**Once a day**, for a reserve lying in its port: every other fleet of its country based there,
from priority 3 down to 0, changes ships with it -

    only a fleet lying in the same province
    limit = DEFENSIVE_STRENGTH_LIMIT;  PASSIVE_STRENGTH_LIMIT for a fleet whose stance is passive
    again and again:
        the fleet's ship with the least strength over its type's full strength, under the limit
        the reserve's ship of the same type with the most, and more than that one
        the two change fleets;   none of either -> done

In the base game a ship is worn under half its strength, or under three quarters for a passive
fleet. **An aggressive stance reads the defensive limit too**: the original never reads
`AGGRESSIVE_STRENGTH_LIMIT` here. **A fleet of priority 4 is never seen to**: the original
sorts the fleets into four lists and the window lets a fifth priority be set. A fleet at sea
gets nothing; it has to come into the port.

*Ours:* within a priority the fleets are taken in the order of their numbers.

## 3. Patrol

`confirmed` (`FINDINGS-navalmissions.md`, section 4).

**Each hour**, after the shared frame: the move, the look for somebody to shadow, and the try
at the convoys.

**The move.**

    shadowing nobody:
        in its province or one beside it, and with no path:    one step of its round
        otherwise, with no path and not in its province:       the safe way there
    shadowing a fleet that is under way to somewhere else than this one:
        the plain way to the province that fleet is going to next

**The round** is the sea provinces beside the order's province, **in the order of that
province's edges**, and then the province itself again:

    each hour it finds itself with no path it takes the next:  a neighbour that is land is
    passed over, and so is one out of range, each costing its hour;  after the last, back to the
    province itself where it is not there

**Shadowing.** Every hour the fleet it shadowed is let go, and the first fleet in the province
the patrol is in - in the order they came - that is another country's, no friend's, and that the
patrol sights becomes the one it shadows. **No war is asked for**: a patrol shadows neutrals. A
shadowed fleet is **twice as easily sighted** by anybody, and water a patrol shadows somebody
in is known to its country at 9 from the day's turn (section 9), which is a sighting with no
roll. *Ours:* a friend is the country itself and its faction; the original also lets off anyone
it has a "friendly agreement" with, which was not read. And a fleet is let go when its shadow's
order is taken away; what the original does then was not followed.

**The convoys.** A patrol in no battle that has not been told to return to base and is not
falling back counts twelve hours and then tries the convoys of the water it is in, exactly as a
raider does (section 8) - the first try in its first hour. **That a patrol sinks convoys is the
reading's**; the record had the call under the raid order until this reading.

**In a battle** a patrol's stance decides two things (`navalcombat.md`, section 1): one that is
not aggressive starts no battle with a fleet that has more than five thirds of its own carriers
and capital ships; and found by somebody, an aggressive patrol can be made the defender by
anyone, a passive one by nobody, and a defensive one only by a fleet weaker than itself.

## 4. The raid on convoys, as an order

`confirmed` (`FINDINGS-navalmissions.md`, section 5). The patrol's hour without the shadowing,
with two differences in the move: the round is taken **without a thought for the range**, and
not while the fleet lies at its own base - from there it first makes for its province.

**A raider starts no battle between fleets and is never made the defender of one.** It can
still be taken into a battle being fought where it is, on the side whose enemies are its own.

## 5. Intercept

`confirmed` (`FINDINGS-navalmissions.md`, section 6).

**Giving it** to a fleet that is not in its base's province tells it to return to base.

**Each hour**:

    no base                                          -> the order is over
    in a battle, or told to return to base           -> nothing
    too worn                                         -> a harbour
    after nobody, and not in its base's province     -> told to return to base
    falling back                                     -> it lets its target go, and is told to return
    after nobody, and in its base's province:
        the look of section 1 at the order's province, and then at each province beside it in
        the order of its edges; the first fleet found is its target, and it goes to where that is
    after somebody, and in no battle:
        the target is out of range                   -> told to return to base
        the target is under way and this fleet is not -> to the province the target goes to next
        otherwise, with no path: to the province the target is in - or, that being land,
                                                        told to return to base

So **an interceptor waits in port, sees from there without rolling, and chases**. And with
section 1's mark: **once it has been told to return to base, the order does nothing more.** The
fleet sails home and lies there under an order that reads "Intercept(Returning)", and does not
go out again until it is given an order anew. That happens to one given the order while at sea,
one whose target went into port or out of range, and one that was beaten. It is built so.

## 6. Sortie

`confirmed` (`FINDINGS-navalmissions.md`, section 7). A sortie has a list of waypoints, which is
the order's province alone - the original can load more from a save and nothing read adds any.

**Giving it** plots the way: to the waypoint by safe water; and then, where the waypoint has no
naval base and the fleet is **not** in its base's province, the plain way from the waypoint
home - **which replaces the way out**. A fleet ordered from its base is left with the way out. A
fleet ordered from anywhere else is left with a path that does not begin where it is, and halts.

**Each hour**:

    no base                                          -> the order is over
    in a battle                                      -> nothing
    in the last waypoint                             -> told to return to base, where the first
                                                        waypoint has no naval base; nothing else
    too worn                                         -> a harbour
    in the waypoint it was making for                -> on to the next, if there is one; a last
                                                        waypoint with a naval base becomes its base
    then: the look of section 1 at each sea province beside the one the fleet is in;
          the fleet found with the most strength is gone for

So **a sortie sails to its province and turns for home the hour it arrives**, and all the while
- on the way out, on the way home, and lying in port afterwards under the order - **goes for the
strongest enemy fleet it sights beside it** that is no stronger than itself. **Having gone for
one it does not take up its way again**: the original's test that would plot it afresh asks for
a fleet with no path that is in a battle, after having answered for every fleet in a battle
already. A sortie that turned aside on its way home lies at sea where it fought.

## 7. Escort

`confirmed` (`FINDINGS-navalmissions.md`, section 8).

**The beat** is the first unbroken stretch of the convoy's route, from its start, of sea
provinces **nearer the fleet's base by sea than its range**. Its two ends are what the fleet
sails between.

**May be given** where there is such a stretch, to a fleet in no battle, for a sea province.
The orders window offers one escort row for each of the country's own convoys through the
province.

**Giving it** sends the fleet to the first end by safe water.

**Each hour**: over, where the convoy is gone or its route no longer has a stretch in range; a
harbour, where too worn; and a fleet that is in the end it was making for turns for the other.
The first time it comes to the first end the turn "sends" it to where it already is, and it sets
off for the last end an hour later; that is the original's.

**It does nothing to the convoy.** What an escorting fleet is worth is the battle it brings a
raider to: its mission efficiency against the raider's when it looks for it (section 9), and
then a battle like any other. The escorts that defend a convoy in a raid are the convoy's own
count (`convoys.md`), which no fleet adds to.

## 8. A raid on a convoy

`confirmed` (`FINDINGS-convoys.md`; `FINDINGS-navalmissions.md`, section 9).

**The try**, every twelve hours for a patrol or a raider:

    attack = for each ship, convoy_attack x a hundredth of its strength x a hundredth of its organisation
    detect = the best surface_detection among its ships
    for each convoy whose way goes through the province the fleet is in:
        another country's, at war with the fleet's, with a transport,
        and roll(50) < detect:
            its defence is no more than the attack, or no convoy is picked yet -> this one
    none picked                                 -> nothing
    the picked convoy's defence > the attack    -> nothing: too strong

    defence = 5 for each escort, over the provinces of the convoy's way

So a ship's `surface_detection` is its chance in fifty of finding each convoy, and **escorts are
spread thinner the longer the route**: five on four provinces defend at 6.25, on twenty at 1.25.

**The fleet the convoy becomes.**

    scale      = a fifth of the provinces of its way, and 1 at least
    escorts    = its escorts / scale;      some but under one -> one
    transports = its transports / scale;   some but under one -> one
    ships      = escorts + transports, rounded to the nearest, a half to the even number
    the first whole `escorts` of them are `destroyer`s, the rest `transport_ship`s

The ships are new ones of those two unit types, at full strength, of the convoy's country. **The
destroyers have their country's technologies and the transport ships have none**: the original
fills both from a loop bounded by the destroyer's list.

**The battle.** The raider attacks and the convoy's fleet defends, in a battle at sea
(`navalcombat.md`) with four differences:

- every ship of both starts at a distance of 1, as near as ships get;
- **five rounds are fought at once**, in the hour of the try, and then the battle is gone -
  nobody joins it and it is on nobody's list;
- the raider shoots with **`convoy_attack`** in place of `sea_attack`;
- a ship of the convoy's does **an eightieth** of its strength damage and **twice** its
  organisation damage, and neither side is asked whether it has lost.

Everything else is as at sea: the dice, the positioning, who a ship shoots at - a raider's ships
that lose the enemy shoot at their own last ship seven times in a hundred here too, and with
their convoy attack - and the hourly roll to break off, which can leave the raider counting.

**The count.**

    lost = full strength less strength, added up for the transport ships and for the destroyers
    ships lost = lost x 2 / 100            for each of the two
    transports: over 0.3 of a ship and under one -> one
    either is one or more:
        the convoy loses both, rounded to the nearest, and no more than it has
        the hour is remembered on the convoy
    the hour is remembered on the province, whatever was lost

A ship of a hundred strength wholly lost counts as two of the convoy's. *Ours:* a ship of the
throwaway fleet that is sunk outright is out of that fleet and is not counted, as the reading
has it; a raid's shooting does not reach that in practice.

**What a lost ship costs** is in `convoys.md`: nothing at once, and from tomorrow the convoy
carries less by the share of the transports it would have that it has not. Three more things
follow an attack, each `confirmed` (`FINDINGS-supplyconvoys.md`) and built with this:

- **the day after**, the convoy looks for its way afresh, and for five days a step out of the
  water it was attacked in costs five times as much;
- it **wants more escorts**: five more for three days, three for a week, one for a fortnight;
- for a hundred days it is **given** escorts up to its share of what its country has, where a
  convoy never attacked is given none - and gives none back.

*Not built:* an air unit raids with `sea_attack` and five times the attack; the raider's country
gains threat for each ship sunk; a lend-lease convoy loses efficiency; the experience a raid
gives; the messages; and the strategic warfare figures.

## 9. What missions add to the sighting

`confirmed` (`FINDINGS-navaldetection.md`; `FINDINGS-navalmissions.md`, section 4).
`navalcombat.md`, section 1, has the test; with missions it gains:

    the fleet's order is a raid on convoys                                -> no
    its order is a patrol, not aggressive, and five times its carriers and
        capital ships are fewer than three times the other's              -> no
    its country knows the province IT is in at 9                          -> yes

    and in each pair's figure:
        x (1 + its mission efficiency - the other's),  for an escort or a patrol looking at a raider
        x NAVAL_INTERCEPTION_AFTER_ATTACK_FACTOR,      where a convoy was attacked in the province
                                                       the other is in, today or yesterday
        x 2,                                           where the other is shadowed by a patrol

**How well a country knows a province.** The original keeps a level from 0 to 9 for every
country in every province and works them all out afresh **once a day**, as the last thing of
the day. What is built of it:

    land a country's unit stands in                             9, at once
    a sea province one of its units was in at the day's turn    2
    the same, where that unit was a patrol shadowing somebody   9

A fleet that has just sailed into a province knows nothing of it until midnight. *Not built:*
radar, which gives up to 8, and everything else the original's intelligence does. *Not
established:* the original also runs the refresh every hour once a change of who plays a
country has asked for it, and nothing found ever stops that; whether an ordinary game is in
that state was not read. Here it is daily.

## 10. Mission efficiency

`confirmed` (`FINDINGS-navalmissions.md`, section 10). A technology gives a mission its figures
in a block named after the mission:

    convoy_escort = { efficiency = 0.05 }
    convoy_raid   = { reduction_modifier = -0.50 }

A country's efficiency at a mission is each such `efficiency` times the level it has of the
technology, plus the country modifier `<mission>_eff`, which nothing in the base game sets. It
is used twice: in the sighting above, and **on the attack and the defence of every ship of a
fleet whose order is that mission**, in any battle it is in (`navalcombat.md`, section 3); a
plain move is no mission.

**`reduction_modifier` does nothing.** The original reads it in one place, when a fleet is let
out of a battle, and throws the figure away. It is read from the files here and kept, so that a
mod's file loads, and used by nothing. The missions' names are the original's 32, and a key in
such a block that is neither of the two is reported.

## 11. The window and the panel

- **The window of orders** (`fleetsandair.md`, section 4) lists the missions for a fleet and
  ticks the ones that can be given; its stance and its priority go with the order. Under them
  comes a row for each of the country's own convoys through the province, "Escort *start* -
  *end*". Transport and invasion are listed and cannot be ticked.
- **A unit's panel** says what the order is, as the original does: "Patrol(Searching)" or
  "Patrol(Shadowing)"; "Intercept(Waiting)", "(Attacking)" or "(Returning)"; "Escort *start* -
  *end* Convoy" (`inferred`: the escort's own text was not read); and for the rest the
  mission's name and the province.

## 12. Worth a look in the original

Nothing here was watched, and these are the places where the reading says something a player
might not expect. Each is built as read.

1. **An interceptor goes out once.** After its first return to base it lies in port under its
   order and does nothing (section 5).
2. **A patrol sinks convoys**, every twelve hours, like a raider (section 3).
3. **A sortie ordered from anywhere but the fleet's base** is given a path that does not begin
   where it is, and stops (section 6); and one that turns aside for an enemy does not go on.
4. **A reserve never sees to a fleet of priority 4**, and treats an aggressive fleet as a
   defensive one (section 2).
5. **A raider's ships shoot at their own last ship** in a raid, with their convoy attack, by
   the friendly fire of `navalcombat.md`, section 4. In the trial below it costs a fleet of nine
   about a tenth of its strength in a month of raiding.
6. **An escort hesitates an hour** at the first end of its beat the first time (section 7).

## 13. Trying it

**A trial.** `OpenHOI3.SaveCheck raid <install> [raider] [victim] [days] [seed]` puts two
countries at war, gives the first one's fleet with the most `convoy_attack` the order to raid
convoys in the water within its range that the most of the other's convoys go through, and
tells each sinking for so many days. It is not a check: there is nothing to set it against.
With no tags it is Germany against the United Kingdom for thirty days. On the start date:

    Germany's battle fleet, nine ships attacking at 11.7, sent to the Channel Approach, which 77
    of Britain's 78 convoys go through: there in two days; 32 raids that sank something in the
    month, 45 transports and 5 escorts in all, of 670 transports; the convoys attacked are given
    escorts, 62 by the month's end, where none had any; the raiders end with 844 of their 900
    strength

(Run again on 2026-10-10 once a new game ran its two days, which moved each figure by one or
so.) Britain's convoys fall from 78 to 27 in the first week whatever the raider does: a country at
war unmakes the supply convoys it does not need (`convoys.md`, section 7).

**In the client** `--mission=ENG:patrol:10531` gives the strongest fleet of a country that may
have it a mission for a province - reserve, patrol, intercept, sortie or convoyraid, with
`:passive` or `:defensive` after the province. With `--war=GER:ENG` that gives the player's own
missions something to meet, there being no AI to send anything.

### Not built

- **Transport and invasion**, and everything about carrying land units by sea.
- **Aircraft**: the air convoy raid, a carrier's aircraft in a battle, every air mission.
- **Leaders**, **weather** and the **AI** - no fleet of a country nobody plays is ever given a
  mission but by `--mission`.
- **Intelligence** beyond a unit's own part (section 9).
- **Several waypoints for a sortie.**
- The **messages**: a patrol's new shadow, a convoy too strong, ships lost.

### Not established

- Whether the intelligence levels are in practice worked out daily or hourly (section 9).
- What the original's list of a country's naval bases holds, and in what order (section 1).
- What an order's two dates are set to by anything but the orders window; an order from the
  window never ends by date, and none is built.
- One byte on a fleet that keeps it out of a reserve's exchange, and one answer every naval
  order gives that nothing was found to ask.
