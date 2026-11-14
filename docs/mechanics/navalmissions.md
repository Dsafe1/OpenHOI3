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

**It is written to do what the original does, its oddities with it.** The oddest are gathered
in section 12; the maintainer has said they are the original's, bugs or not, and they are not
tidied.

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
roll. A friend is of the patrol's own real faction, or has a **friendly agreement** in its record towards the patrol's country: an
alliance, military access, a vassal bond either way or a guarantee either way (`reversing/findings/FINDINGS-liveread.md`,
section 41; `CCountryLists.HasFriendlyAgreement`, since 2026-11-11). And a fleet is let go when its shadow's
order is taken away; what the original does then was not followed.

**The convoys.** A patrol in no battle that has not been told to return to base and is not
falling back counts twelve hours and then tries the convoys of the water it is in, exactly as a
raider does (section 8) - the first try in its first hour. **That a patrol sinks convoys is the
reading's**; the record had the call under the raid order until this reading.

**In a battle** a patrol's stance decides two things (`navalcombat.md`, section 1): one that is
not aggressive starts no battle with a fleet that has more than five thirds of its own carriers
and capital ships; and found by somebody, an aggressive patrol can be made the defender by
anyone, a passive one by nobody, and a defensive one only by a fleet weaker than itself.

### 3.1 What a new shadow tells the player

`confirmed` (`reversing/findings/FINDINGS-navalmissions.md`, section 4, the addition of 2026-11-14). A patrol that takes
up a fleet raises `PATROLTRACK` - log and map, **no popup**, category combat, in Their Finest Hour's
`interface/messagetypes.txt` - where:

    the fleet taken up is not the one it held as the hour began - the same fleet found again is no news,
        and one found after an hour of shadowing nobody, or the other of two fleets, is news again
    and the patrol's country is the player's on this machine

    the player's country's standard values: COUNTRY, MONARCHTITLE, TITLE, a value for each post
        of its government (messages.md, section 3)
    MONARCHTITLE  the patrol's country's ruler's title, again
    OUR_FLEET     the patrol's name
    OTHER         the shadowed fleet's country's name
    OTHERS        the shadowed fleet's own name
    PROV          the province's name
    province: the patrol's, which is the shadowed fleet's;  first country: the patrol's;  second: the
        shadowed fleet's;  unit: the patrol

**It carries the patrol, but its Goto centres on the province** (`messages.md`, section 4.1). Two things are the
original's and kept: a patrol that loses sight of a fleet for one hour, the sighting roll failing, tells of it again
the next - so a patrol beside a fleet it can barely see tells of it over and over - and the log line "`$OUR_FLEET$ found
$OTHERS$ fleet in $PROV$.`" puts the fleet's own name where it reads like a country's adjective ("Schlachtflotte found
46th Navy fleet in Channel Approach.") - the text's, and a mod may change it. Both are listed in `docs/enhanced.md`.

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

*Not built:* a lend-lease convoy loses efficiency. The raider's country's threat for each ship
sunk (`diplomacy.md`, 5.3) and the strategic warfare figures (`politics.md`, section 6;
`strategicwarfare.md`) are built, and so is the experience a raid gives, since 2026-10-11
(`leaders.md`, section 11): before each of its first three rounds, at `EXP_GAIN_CONVOY_MODIFIER`.

**A convoy's ship sunk outright counts for nothing** (`confirmed` as the count's walk, the
removal being every battle hour's own): the count walks the throwaway fleet as the rounds leave
it, and a ship that went under a tenth of a point of strength has left it. A fleet's raid hardly
ever does that; one from the air can, a bomb's tenfold roll finishing a transport ship. Kept so,
and listed in `docs/enhanced.md`.

### 8.1 The raid from the air

`confirmed` (`FINDINGS-navalmissions.md`, section 9 and its addition of 2026-11-14;
`FINDINGS-bombing.md`, sections 1, 2 and 6). The original's `air_convoy_raid`, "Convoy Raiding"
in the air orders window - **an order of its own, and alive**: the window offers it, and a
naval bomber given it raids.

**May be given** for any province that is not land, by an air unit whose wings have
`sea_attack` (the window lists it for one; `fleetsandair.md`, section 4). **Its list of
provinces takes any province at all**, unlike the naval strike's.

**Its hour** is a strike's (`bombing.md`, section 2), with one thing before it:

    no base                                       -> the order is over
    its hours to the next try, down by one - wherever it is
    in the province it flies at, with under one left:
        the try of section 8, by this unit, and twelve hours again
    then the strike's hour

The hours start at **nought**, so the first try is the first hour it is in its province.
**It bombs nothing and starts no bombing anywhere**: the strike's hour then finds it standing
in its province with no bombing, and picks again - and **its pick never takes the province it
is in**, so it leaves the hour it arrives, for another of its list or home where there is none
other. From its base it flies out again at once. With one province on its list it goes there,
tries if its hours have run, comes home and goes back - every other sortie finding its hours
not yet run, for a base near its water (`docs/enhanced.md`).

**Which province**: of the mission's provinces in order, the one the unit is in passed over,

    score = 1000 + 1000 x the transports of every convoy going through it whose owner is not
            the unit's and is at war with it - or either is REB, or an undeclared war covers
            the province
    the first that beats the best so far, which starts at nought

so the first province always stands until another has more, and one with no convoy at all
still scores. Its own convoys count nothing.

**The try** is section 8's with three differences:

- the attack is each wing's **`sea_attack`** - never `convoy_attack` - times a hundredth of its
  strength and a hundredth of its organisation; detection is the best `surface_detection`
  among the wings;
- a convoy is still **found** against the plain attack - one with its defence at or under it
  preferred - and only the test against its escorts **multiplies the attack by 5**: a defence
  above five times the attack is too strong;
- **the battle is a bombing of ships** (`bombing.md`) and not a battle at sea: the raider
  bombing, the convoy's throwaway fleet bombed, set up as the raid's - the raider in no bombing,
  the battle on its own list of battles for the rounds - every ship and wing at a distance of 1,
  and **five hours of it at once**, the raider's experience before the first three, and
  nobody asked whether they have lost. Each hour is a bombing's hour: the wings' fire, their
  bombs on the province - a sea province, so nothing - the province's guns and the friendly
  air attack standing in it (the convoy's own fleet among it, and, at sea, the raider), and
  each ship's own air attack back.

**What the wings fire at**: under this order, **every ship of the convoy's fleet with
strength left, with no roll** - where a naval strike's wing finds a ship only by its leader's
roll - and with its `sea_attack`, bettered by its leaders' `naval_attack` as a naval strike's.
A wing's hit on a ship is a bombing's: its two dice through the ship's hull, three tenths of
the strength and a half of the organisation, times `BOMB_SHIP_DAMAGE_MODIFIER`. **The convoy's
ships' own shooting does nothing to a wing of this battle but by the bombing's fire**: the
eightieth and the doubled organisation of section 8 are a battle at sea's.

Everything after the battle - the count, the losses, the convoy's escorts wanted, the province
and the convoy remembering the hour, the list of finished battles - is section 8's.

*Ours:* the air unit's hours to its next try are on the order's mission, as a fleet's are; the
original keeps them on the order and **neither writes nor reads them in a save**, so a loaded
game starts them at nought here as in the original (`docs/enhanced.md`, *A whole game's replay*).

### 8.2 What a raid tells the players

`confirmed` (`FINDINGS-navalmissions.md`, the addition of 2026-11-14). Three message types of
`interface/messagetypes.txt`, all three logged, put on the map and popped up with a Goto in
Their Finest Hour, category combat. Each is raised only where the country told is the player's
on this machine; the Goto centres on the province (`messages.md`, section 4.1).

**A convoy too strong** - the try found a convoy and its escorts beat the attack -
`CONVOY_TOO_STRONG`, to the **raider's** country:

    the raider's country's standard values: COUNTRY, MONARCHTITLE, TITLE, a value for each post
        of its government (messages.md, section 3)
    PROV        the raider's province
    LOCATION    the same
    CONVOY      "<start> - <end>", the convoy's two ports
    EVIL        the convoy's owner's name
    UNIT        the raider's name
    province: the raider's;  first country: the raider's;  second: the convoy's owner

**Ships lost** - the count took a whole ship or more - two messages, the owner's first:
`CONVOY_ATTACK_ON_US` to the **convoy's owner**, with the sound `convoys_1`, and
`WE_ATTACK_CONVOY` to the **raider's** country, with `convoys_2` (`sound.md`, section 7):

    PROV        the raid's province
    TRANSPORTS  the transports lost - as taken off, never more than the convoy had
    ESCORTS     the escorts lost, likewise
    CONVOY      "<start> - <end>"
    EVIL        the raider's country's name
    TITLE       the player's ruler's title
    province: the raid's;  first country: the convoy's owner;  second: the raider's

These carry **none** of a country's standard values, so their texts' `$TITLE$` is the one of
their own. The three are the same for a fleet, a patrol and an air unit. **A raid starts no
battle message** (`messages.md`, section 9).

### 8.3 A raider fought

`confirmed` (the same addition). A battle's first hour gives the strategic warfare figures:

- **at sea**, where the battle is **a raid's own** and a defending fleet raids convoys or
  patrols: `STRAT_RAIDER_FOUGHT_IMPACT` to the attacker's first country's convoy figure. The
  defender of a raid's battle is always the convoy's fleet, under no order, so **this is never
  given** - kept so, and listed in `docs/enhanced.md`. It had been built for every sea battle.
- **in the air**, where a defending unit raids convoys from the air: the attacker's wings times
  `STRAT_AIR_RAIDER_FOUGHT_IMPACT` over five, to the attacker's first country's convoy figure -
  beside, and not instead of, the bomber's of a strategic bombing (`politics.md`, section 6).

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

**How well a country knows a province** is `intelligence.md`'s, read whole since 2026-10-14: made
again once a day, and **raised at the end of every hour** for the provinces units left or
entered. For a fleet:

    a sea province one of its units is in                        2, by the hour's end
    the same, where that unit is a patrol shadowing somebody     9
    the same, where a battle between fleets is being fought      9
    its country's own and its friends' ground, a port among it   9

So a fleet that has just sailed into a province knows it at 2 by the end of that hour, where
this was first built to know nothing of it until midnight. The maintainer's word on 2026-10-10,
asked whether it was daily or hourly, was "I think its daily" - given before the hour's raise
was read; put to him once it was, his word on 2026-10-14 was "Keep it as read."

## 10. Mission efficiency

`confirmed` (`FINDINGS-navalmissions.md`, section 10). A technology gives a mission its figures
in a block named after the mission:

    convoy_escort = { efficiency = 0.05 }
    convoy_raid   = { reduction_modifier = -0.50 }

A country's efficiency at a mission is each such `efficiency` times the level it has of the
technology, plus the country's modifier of the mission, which a file writes by the mission's own key -
`convoy_raid = 0.25`, as three of the base game's event modifiers do - and the original names
`<mission>_eff` (`modifiers.md`, section 2; until 2026-11-14 this read a kind `convoy_raid_eff` no
file can set). It
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
  *end*". Transport and invasion are built since 2026-10-15, and are `transport.md`'s, sections
  7 and 10.
- **A unit's panel** says what the order is, as the original does: "Patrol(Searching)" or
  "Patrol(Shadowing)"; "Intercept(Waiting)", "(Attacking)" or "(Returning)"; "Escort *start* -
  *end* Convoy" (`inferred`: the escort's own text was not read); and for the rest the
  mission's name and the province.

## 12. Worth a look in the original

These are the places where the reading says something a player might not expect. Each is
built as read. **The maintainer, 2026-10-10, shown the six in a line each: "All those things
are correct, and they may be bugs, but thats ok."** So they are the original's by his word as
well as by the reading, and they stay; do not tidy them. (That he meant these six is how his
answer was taken - it came after a list that also asked three other things, section 13 and
`tick.md`, section 6. The second half of item 3 and the priority in item 4 were not in the
line he was shown.)

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

**A trial.** `OpenHOI3.SaveCheck raid <install> [raider] [victim] [days] [seed] [air | patrol]` puts two
countries at war, gives the first one's fleet with the most `convoy_attack` the order to raid
convoys in the water within its range that the most of the other's convoys go through, and
tells each sinking for so many days. It is not a check: there is nothing to set it against.
With no tags it is Germany against the United Kingdom for thirty days. On the start date:

    Germany's battle fleet, nine ships attacking at 11.7, sent to the Channel Approach, which 77
    of Britain's 78 convoys go through: there in two days; 24 raids that sank something in the
    month, 37 transports and no escort, of 670 transports; the convoys attacked are given
    escorts, 56 by the month's end, where none had any; the raiders end with 885 of their 900
    strength, and their admiral Raeder has gone from skill 4 to 5 on the raids alone

(Run again on 2026-10-10 once a new game ran its two days, which moved each figure by one or
so, and on 2026-10-11 once the leaders and the experience of battle were built.) Britain's convoys fall from 78 to 27 in the first week whatever the raider does: a country at
war unmakes the supply convoys it does not need (`convoys.md`, section 7).

On 2026-11-14, before and after the messages and the raid from the air were built, the same run
gave the same figures line for line - 24 raids that sank something, 37 transports and 5 escorts,
the AI moving the German fleet off after its month - and now ends by counting the messages raised:
`CONVOY_ATTACK_ON_US 28, WE_ATTACK_CONVOY 28`, the AI's other German raiders among them, the first
of each written as its log line ("The Lefkosia - Dover convoy lost 2 transport(s) and 0 escort(s)
in Channel Approach.").

**From the air**, `air` after the rest: the raider's air unit with the most `sea_attack` that
reaches such water from its base is given the raid on convoys from the air for it, and the
raider's country is played so that the AI leaves it be. `raid <install> ITA ENG 30 0 air`, on
2026-11-14:

    Italy's 3rd Comando Aerosiluranti, one wing attacking at 2.45 - 12.25 against escorts - from
    Roma at the Gulf of Tunis, which 39 of Britain's convoys go through: there in two hours, home
    and out again every six, a try every twelve; 5 raids that sank something by 6 January, a
    transport each, its organisation worn from 35 to under 15 by the province's fire and the
    destroyers' air attack, and at nought on the sixth day, when its order is dropped and it lies
    at Roma

(Germany's air units reach none of Britain's convoys' water in 1936.)

**A patrol**, `patrol` after the rest: the fleet is given a patrol of that water instead, its country played, and each
fleet the patrol takes up is told with its `PATROLTRACK` as it happens; the count at the end is everybody's, the AI's
patrols raising it too. On 2026-11-14 the plain run gave the same figures before and after `PATROLTRACK` was built -
30 raids that sank something, 46 transports and 12 escorts - with `PATROLTRACK 428` added to its count, and
`raid <install> GER ENG 30 0 patrol` the same 7 raids, 10 transports and no escort before and after, the Schlachtflotte
telling of seven fleets taken up in the Bristol Channel, the Channel Approach and the Western English Channel between 5
and 8 January ("Schlachtflotte found 46th Navy fleet in Channel Approach."), and `PATROLTRACK 74` in all.

**In the client** `--mission=ENG:patrol:10531` gives the strongest fleet of a country that may
have it a mission for a province - reserve, patrol, intercept, sortie or convoyraid, with
`:passive` or `:defensive` after the province. With `--war=GER:ENG` that gives the player's own
missions something to meet, there being no AI to send anything. `--mission=ITA:airconvoyraid:10568`
gives the air unit with the most sea attack that reaches the province the raid from the air.

### Not built

- **The AI's raid from the air**: whether the original's AI gives the order was not read; the
  AI here gives it to nobody. A carrier's aircraft in a battle are built since 2026-10-16
  (`transport.md`, section 13.2), and the air missions are `aircombat.md`'s and `bombing.md`'s.
- **Weather** and the **AI** - no fleet of a country nobody plays is ever given a mission but
  by `--mission`. The leaders a raid's battle asks are built since 2026-10-11
  (`leaders.md`, section 10).
- **Intelligence** beyond a unit's own part (section 9).
- **Several waypoints for a sortie.**
- ~~The **message of a patrol's new shadow**, `PATROLTRACK`.~~ Built 2026-11-14 (section 3.1), with a convoy too
  strong and ships lost (section 8.2).

### Not established

- Whether the intelligence levels are in practice worked out daily or hourly (section 9).
  The maintainer thinks daily, which is what is built.
- What the original's list of a country's naval bases holds, and in what order (section 1).
- What an order's two dates are set to by anything but the orders window; an order from the
  window never ends by date, and none is built.
- One byte on a fleet that keeps it out of a reserve's exchange, and one answer every naval
  order gives that nothing was found to ask.
- **Whether a fleet may be sent to a province** (`CMovementRules.MayMoveTo` against the original's own test): the Soviet
  Red Banner Baltic Fleet lies at Murmansk on a patrol of the Gulf of Riga in the Ireland game's saves of 16 July 1941.
  The original leaves it be - its `movement_progress` of 0.916, left from its last voyage, stands all day - where the
  patrol here asks whether it may go, is told yes, finds no safe way, and is given an empty path, which clears its
  progress. The original's patrol step sets the path whether a way was found or not, so it must be told no; what in
  the original's test refuses it (its range, the straits) was not read.
