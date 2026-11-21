# Bombing: the eight missions, the bombing and what a bomb does

What an air unit on a bombing mission does each hour, how a bombing is fought and what it
leaves of a province, written as rules this project can implement. No addresses and no offsets.
Each rule says how sure it is - `confirmed`, `likely`, `inferred` in the fact base's vocabulary
- and where this project chooses its own behaviour the paragraph is marked **Ours**.

Sources: `reversing/findings/FINDINGS-bombing.md`, with `reversing/findings/FINDINGS-airbattle.md`
(the frame every air order shares, what a country knows of a province),
`reversing/findings/FINDINGS-airnaval.md` (what starts a bombing),
`reversing/findings/FINDINGS-airdefence.md` (the bomber's shot and the fire that comes back) and
`reversing/findings/FINDINGS-combatmods.md` (what the hour makes of each side).

**Nothing here was watched in the original, and nothing can be checked against a save**: none
of the reference saves has a bombing, and the rolls cannot be made again. It is the fifth of the
pieces air and naval combat was cut into (`navalcombat.md`); a carrier's aircraft, the raid on
convoys from the air (`navalmissions.md`, section 8.1, whose battle is a bombing of ships run by
this spec's hour) and the air side on screen are not in it.

**It is written to do what the original does, its oddities with it.** Section 12 gathers the
oddest, and none of them has been reviewed.

In the code: `lib/OpenHOI3.Sim/Missions` (`CBombingOrders`, `CBombingMissions`,
`CBombingProvinces`), `lib/OpenHOI3.Sim/Combat` (`CBombing`, `CBomberFire`, `CProvinceBombing`,
`CAntiAircraft`, `CBomberModifiers`, `CBombTargetModifiers`, `CRadarCover`) and
`lib/OpenHOI3.Sim/Provinces` (`CProvinceRepair`).

## In one paragraph

A bombing mission is an order on an air unit, asked every hour whether it is still going, and
the asking is where it flies. A unit flies out to a province of its list, bombs it for four
hours and is told to return the hour after; home again it flies once more, until its end hour.
**A bombing is a battle of its own kind**, one to a province: of the ground itself, which needs
no unit; of land units; or of ships. Each hour every wing of the bombers fires at one target
its unit's mission goes for, with the attack that mission names - and then **bombs the
province**: whole runs of its strategic attack, never fewer than one, each a die's worth off
the buildings, the stockpile and the day's production its mission names. The province fires
back with its guns and with every friendly unit's air attack, and ships and land units with
their own beside. Nothing a target has defends it. Bombed buildings and mines come back a
little every day, faster on good roads.

## 1. The missions

Eight, in the order the orders window lists them: strategic bombing, logistical bombing,
runway cratering, installation strike, ground attack, interdiction, port strike, naval strike.
`confirmed`.

| | may be given for | goes for | fires | bombs |
| --- | --- | --- | --- | --- |
| strategic bombing | land an enemy holds, by a unit with a strategic attack | nothing | nothing | industry, the guns; the day's crude oil, metal, energy and rare materials |
| logistical bombing | the same | nothing | nothing | roads, the guns; the supplies and fuel lying there |
| runway cratering | the same | aircraft at their base | hard attack | the air base, the guns |
| installation strike | the same | aircraft at their base | hard attack | the radar station, the guns |
| ground attack | land an enemy holds | land units | soft and hard attack | nothing |
| interdiction | land an enemy holds | land units | soft and hard attack | nothing |
| port strike | any land | ships | sea attack | the naval base, the guns |
| naval strike | any water | ships, aircraft at their base | sea attack | nothing |

- **Every mission that bombs buildings bombs the anti-aircraft guns too.**
- **An installation strike bombs radar and nothing else.** The original's own text for it
  names rocket sites and reactors as well; the routine names one building.
- A building is known by what a level of it gives its province - `ic`, `infrastructure`,
  `air_capacity`, `naval_capacity`, `radar_level`, `local_anti_air` - and not by its name, so
  a mod's building with one of those is bombed as that.
- The orders window lists a mission only for a unit whose wings have the attack for it and
  for the right kind of province (`fleetsandair.md`); whether it may then be given is the
  second column.

**Which provinces a mission lists.** The list comes with the order (`aircombat.md`, section 2).
A province is kept on it by the mission's own test, **which does not ask how far it is**:

    ground attack                 land
    interdiction, strategic,      anything but land whose holder is not at war with the unit's
        installation                  country
    logistical                    the same; and not a province with neither supplies nor fuel
                                      lying in it and no roads built
    runway                        the same; and an air base built
    naval strike                  water that is no lake
    port strike                   a province whose holder is at war with the unit's country

So a mission may list a province beyond the unit's range; flying out it passes its range, is
turned for home and loses its order, as any air order does (`aircombat.md`, section 1).

## 2. The hour of a strike: ground attack, interdiction, naval strike

A unit away from its base when the order is given is sent home first. Then, each hour, with
`strike` the province it is flying at:

    told to return already                                             -> nothing
    in its base's province and not fit to fly                          -> nothing
    in a battle, or in a bombing                                       -> nothing
    not fit to fly: told to return                                     (and on)
    given no fuel                                                      -> told to return
    with a way to go                                                   -> nothing
    past its end hour: told to return                                  (and on)
    in its hours of grace after a bombing                              -> told to return

    no strike - or a strike that is land and no longer an enemy's:
        in its base's province and fit: strike = the next province (section 4)
        no strike                                                      -> told to return
        land that is not an enemy's                                    -> told to return
                                                                       -> nothing more
    not in the strike province:
        it is given the way there
        the light at the hour it will come into the next province, over the strike
        province, is against its mission's time:
            not in its base's province: told to return; in it: stopped
    in the strike province:
        past its end hour                                              -> told to return
        strike = the next province; none                               -> told to return

- **A sortie is a flight out, a bombing and a flight home.** The bombing starts on arrival
  (section 5); while it lasts the hour does nothing; the hour after, the hours of grace send
  the unit home.
- **The strike province is kept.** Home again the unit flies at the same province. Another is
  picked only where the strike has stopped being an enemy's, or where the unit stands in it
  with no bombing - it arrived and nothing started.

"Fit to fly", "told to return" and the frame's own asking after this are `aircombat.md`'s,
section 1. `confirmed`.

## 3. The hour of a bombing: the four strategic kinds and the port strike

    told to return already, or in a battle                             -> nothing
    in a bombing: the strike is forgotten                              -> nothing
    not fit to fly: told to return                                     (and on)
    given no fuel                                                      -> told to return
    with a way to go                                                   -> nothing
    in its hours of grace: told to return                              (and on)
    past its end hour: told to return                                  (and on)

    no strike:
        in its base's province and fit: strike = the next province
        no strike: told to return
    a strike, and the unit is in it: told to return
    a strike elsewhere: the way there, and the light, as a strike's

- **The strike is forgotten the moment a bombing has it**, so every sortie picks afresh.
- **A port strike with no strike in mind and no enemy's province left on its list is done.**
- Told to return for being unfit or past its hour, the hour runs on and may still give the
  unit a way out, as air superiority's does. `confirmed`.

## 4. Which province

`next` is the province after the strike in the mission's list - the first where there is no
strike, the first again after the last, the strike itself where that is not on the list. Each
mission looks only at the provinces its own test of section 1 lets on. `confirmed`.

- **Ground attack: `next`.** With two provinces or more it goes round them in the list's
  order and weighs nothing. Only where `next` is the strike itself - a list of one - does a
  score count: the province with the most enemy land strength in it, of those its country
  sees into and three wings are not already bound for.
- **Interdiction: the last province of its list that its country sees into** and that is not
  the strike, whether or not an enemy is in it. One it does not see is never flown at.
- **Naval strike: `next`**, and for a list of one the same "last it sees" as interdiction's.
- **Strategic bombing: the most industry and resources for the fewest guns** - its `ic` and
  its day's crude oil, metal, energy and rare materials, over its anti-aircraft level where it
  has any; the first among equals; a province three wings are already bound for, counting
  the unit's own, is passed over.
- **Logistical: the most supplies lying there and roads**, over the guns the same way.
  **Runway: the most air units based there**, over the guns. **Installation: the most
  radar**, over the guns. **Port strike: the most ships in port and naval base**, where its
  holder is at war with the unit's country. For these four the first province stands until a
  better is found.

"Sees into" is `aircombat.md`'s section 6, at 2 or better.

## 5. What starts a bombing, and who is in it

An air unit that enters a province, or stands in one with no way to go, looks at it
(`aircombat.md`, section 7) - and where its mission bombs there, also for a bombing: it joins
the one the province has if that will have it, and otherwise starts one. `confirmed`.

**Where a mission bombs.** Six of them only in their strike province. **Ground attack** also
in any province with units in it that its country sees into where an enemy land unit is
holding ground - in a battle, or under bombs, with the other side still there. **Interdiction**
only in provinces its country sees into that have units in them: its strike province, and any
where an enemy land unit is on the move. So those two bomb on the way.

**Starting one.** Not in a bombing already, and not in its hours of grace:

    a land province an enemy holds, and a mission that bombs
        buildings                                           -> of the ground; no unit needed
    otherwise the first unit of the province, in the order they came, that is
        an air unit at its own base, for a mission that goes for those
        a fleet, for a mission that goes for ships
        a land unit, for a mission that goes for land units
        - a fleet at sea only where the bombers sight it (navalcombat.md, section 1)
        - and that the unit is at war with, and whose own order does not refuse:
          a raider is never bombed, and a patrol by its stance (navalcombat.md)
                                                            -> of ships for a fleet, else of land units
    the province has a bombing already                      -> none is made

**A bombing goes at the front of the list of battles**, where every other battle goes at its back, so the bombings'
hours are run first and their dice drawn before the battles'. `confirmed` - read for the savegame
(`reversing/findings/FINDINGS-savedbattle.md`, B.2; `savegame.md`, section 7); until 2026-11-05 a bombing went at the
back here.

**Who is taken in.** The unit that starts is the bombers' first, and the unit it found the
other side's. Then everybody else standing there is offered a side - **a land unit only to a
bombing of land units, a fleet only to one of ships** - and, for a mission that goes for
ships over a province with a port, **every fleet in the port**, whatever the kind. A unit joins
the side the other side is against; an air unit joins the bombed side only at its own base,
and only where the bombers' mission goes for aircraft there.

- **A second bomber unit joins only a bombing whose other side has units and no aircraft
  among them.** Against a side with nobody on it - a bombing of the ground alone - nobody is
  "against" it, and it stays out. Two units sent at one city's industry bomb it in turn.

**A unit in a bombing is not in a battle.** A land unit under bombs marches and attacks as
before, and a bombing is kept apart from a unit's battles. Whoever leaves the province leaves
its bombing. **An air unit in a bombing stays over it**: it does not move until it is over,
unless it is falling back.

## 6. The bombers' hour

Each hour, the bombers first at every step: two dice nothing reads; what the hour makes of
each side (section 9); the bombers' fire and their bombs; the fire that comes back (section
8); the settling. `confirmed`.

For every unit of the bombers, in the order they joined, **the list of targets is made
afresh**:

- **Ground attack first looks for units that are fighting.** Each land unit of the other side
  that is in a battle is taken by a roll: out of a hundred, no more than a tenth of its
  country's `frontline_focus` where the unit is on a front, of its `reserve_focus` otherwise
  - so one in a hundred with neither technology, and five more a level. If any is taken, the
  list is those units' brigades and no others.
- **Otherwise the list is everything the mission goes for**: aircraft at their base, land
  units, or ships - every brigade and wing with strength left, 1024 at most. **But a ship goes
  on the list only by a roll of its own**: out of a hundred, no more than ten for each point
  of the unit's own leader's skill, whatever his rank, bettered by the country's
  `naval_air_target_chance`.

Then each wing of the unit:

    no target on the list                                              -> it fires nothing
    target = one of the list, drawn once for all its shots
    attack = by the mission (section 1); soft and hard by the target's softness, as on land,
             with the unit's leaders' traits (below)
    n      = attack x what the hour makes of the wing's attack
    shots  = the whole of n, and one more by a roll against its fraction
    each shot:
        the target would have no strength left                         -> no more shots
        a roll nothing uses
        avoided by CHANCE_TO_AVOID_HIT_AT_NO_DEF, 99 in a hundred at most
        organisation = a die of 3 to 9;  strength = a die of 1 to 3, and one time in ten x 10
        a ship: both over its hull
        a brigade: strength x (1 + a hundredth for each brigade, ship and wing on the bombed
                   side) x 0.4 x BOMB_REGIMENT_DAMAGE_MODIFIER
                   organisation x 0.8 x BOMB_REGIMENT_DAMAGE_MODIFIER
        a ship:    strength x 0.3 x BOMB_SHIP_DAMAGE_MODIFIER
                   organisation x 0.5 x BOMB_SHIP_DAMAGE_MODIFIER
        a wing:    strength x 0.5 x BOMB_WING_DAMAGE_MODIFIER
                   organisation x 0.7 x BOMB_WING_DAMAGE_MODIFIER

- **Two rolls to a wing and five to a shot that hits**, in that order: its target, the
  fraction; the unused one, the avoid roll, the two dice, the one time in ten.
- The six shares are the original's own and no file sets them.
- A brigade and a wing lose a hundredth of what is rolled against them when the hour is
  settled; a ship all of it, and a ship left with under a tenth of a point of strength sinks.
- A land unit under bombs is settled as in a battle: officers, the men who come back, and
  whether it shatters (`landcombat.md`, section 6).

**The leaders' traits in the attack**, each down the unit's chain of command (`leaders.md`,
section 10): against land units the target's softness is x (1 + `defender_softness`) before it
is held to 1, and the attack it makes is x (1 + `tactical_attack`); against ships `sea_attack`
is x (1 + `naval_attack`); against aircraft at their base nothing. `confirmed`

## 7. What the bombs do to the province

For every wing of the bombers, after its fire, in a bombing of any kind. `confirmed`.

    n    = strategic_attack x (1 + the unit's leaders' strategic_attack)
           x what the hour makes of the wing's attack
    runs = the whole of n, and never fewer than one
    each run rolls a die of 1 to 3 and takes that many times:
        off each good the mission bombs lying in the province:
            (the roads' most - the roads now) x BOMB_STRATEGIC_RESOURCES_INFRA_FRACTION
                                              x BOMB_STRATEGIC_RESOURCES
            for supplies and for fuel, times what lies there over a week of what the
            province's units ask in a day, 1.2 at most
        off each good's daily production the mission bombs: BOMB_STRATEGIC_PRODUCTION
        off each building the mission bombs: its damage_factor x BOMB_STRATEGIC_BUILDINGS
            x the share of it that is still in working order
    nothing is taken that is not there, and what lies in the province and what it makes
    are held between nothing and 99999

- **A wing with no strategic attack still makes one run an hour**, and what the hour holds
  against a wing - the night, the crowd - cannot take it under one.
- **The fuller a stockpile against its week's need, the more of it burns**; with nobody
  drawing on the province it is the plain figure.
- **The worse the roads, the more of the stockpile is lost**: with roads at their most a
  logistical strike destroys nothing lying there and only wears the roads.
- **A building loses less the more of it is gone**, so bombing never quite finishes one.
- `damage_factor` is a building's own, 1 unless its file says otherwise: a quarter for roads
  and for guns in the base game.
- A capital's pool is its country's stockpile (`stockpile.md`), so a logistical strike on a
  capital burns the country's supplies and fuel.

**A building has two levels**: what was built, and what of it is in working order. The
province's modifier - its IC, its roads, its guns, its radar - is made from the second, when
the day's pass makes it again; whether a province *has* a port or an airfield is asked of the
first. `confirmed` for the modifier, and since 2026-11-21 for each rule that asks "how much naval base"
(`reversing/findings/FINDINGS-bombing.md`, section 13), all of it [exe]:

| what is asked | which level |
| --- | --- |
| is this a port: an area's list of harbours, an area landlocked or fit for a depot, both ends of a convoy | built, above nought - a port bombed to nothing is still a port |
| the order of an area's harbours | **working**, fraction and all |
| an area's air bases, which and in what order | built, in whole levels |
| the depot's score: the naval base's term and the roads' most | built |
| what a harbour can land in a day (`convoys.md`, section 2) | **working**, fraction and all |
| the transports a convoy wants | built |
| a convoy's way beside an enemy: the naval base's term | built |
| the same: the air base's term | built says whether, **working** says how much |

**Ours**: the original sorts an area's harbours only when the area's provinces change or a province gets its first
naval base level, so a port bombed since keeps its place until then; here the areas are made whole whenever they are
asked for (`supply.md`, section 9), and the order follows the working level at once (`docs/enhanced.md`).

## 8. The fire that comes back

The bombed side lists the bombers' wings with strength left. With none it fires nothing.
`confirmed`.

**The province's own fire**, in every bombing:

    guns  = its anti-aircraft level x (1 + its holder's provincial_aa_efficiency) x STATIC_AA_SCALE
    units = the air attack of every unit standing in it whose country is a friend of its
            holder by the original's test, military access counted - a fleet in its port
            counted twice, a carrier's air group with its carrier; at sea, where nobody
            holds the province, every unit of a country that is nobody's puppet
    n     = guns + 1.5 x units
    shots = the whole of n, and one more by a roll against its fraction; each at a wing
            drawn at random: the unused roll, the avoid roll,
            organisation a die of 3 to 10, strength a die of 1 to 2, one time in ten x 5

- **The units' air attack counts whether or not they are in the bombing.** Land units in a
  city whose factories are bombed fire through this and through nothing else.
- **A fleet in port is on both of a province's lists, and is summed from both** -
  `inferred`, from two readings put together.

**Each ship**, in a bombing of ships: its `air_attack` times what the hour makes of its
attack, in shots at wings drawn at random - organisation 3 to 9, strength 1 to 3, one time in
ten x 10. **Each land unit**, in a bombing of land units: the unit's whole `air_attack`, **as
it is** - organisation 4 to 12, strength 2 to 5, one time in ten x 10. An aircraft at its
base fires nothing of its own.

## 9. What the hour makes of each side

**The bombers**, each wing, on attack and defence alike unless said: the naval surprise (the
attack alone: against ships, `AIR_COMBAT_NAV_SURPRISE_BONUS` for the first
`AIR_COMBAT_NAV_SURPRISE_ROUNDS` hours where a roll under `AIR_COMBAT_NAV_SURPRISE_CHANCE`
gave it in the first); **the air battle** - `AIR_COMBAT_ON_BOMBING` for each battle the unit
is in, times the strength attacking over the strength defending in its air battles, each side
taken for a wing at least; supplies; experience; mission efficiency; the radio
(`combat_efficiency`); dissent; **crowding** - `AIR_STACKING_PENALTY` for every wing of the
bombers, `AIR_STACKING_PENALTY_MAX` at most; radar - the highest radar level a friend holds in
the province or beside it, times `RADAR_COMBAT_IMPACT` and one plus the country's
`radar_impact`; the weather and the night on the attack alone; and last the leader's bonus.
`confirmed`.

- **Every wing crowds the others**, where an air battle counts all but one.
- No nearness of the base and no fighter's bonus, which an air battle has.
- **The leaders**, on both sides of a bombing, as in an air battle (`leaders.md`, section 10):
  experience x (1 + `experience_bonus`), supplies x (1 + `out_of_supply_modifier`), the night
  and `night_attack`, and `COMBAT_LEADER_IMPACT` a point of the unit's own leader's skill where
  he has the rank for it - a land unit's by the land battle's ladder.
- The difficulty, on both sides, since 2026-11-06 (`frontend.md`, section 4.3).

**The bombed side**, each brigade, ship and wing: the capital ships' missing screens and the
fort, on the defence alone; experience; mission efficiency; supplies; the radio; dissent;
crowding - a hundredth for each brigade, ship and wing on the side, less the country's
`unit_cooperation`; the night on the attack alone; the leader. **Of all of it only a ship's attack is
ever asked for.** A land unit fires by its unit's figure, which the hour has just put back to
plain; and nothing in a bombing reads a defence - so a fort and a fleet's screens are noted
and decide nothing. `confirmed`.

## 10. The end

**Four hours.** At the fourth the bombers are asked whether they have lost - their wings
averaging under one organisation, or next to no strength left - and if so fall back as from
an air battle (`aircombat.md`, section 8). **The bombed side is never asked.** Then the
bombing is over, everybody in the province looks at it again, and the bombers - found in a
bombing at the start of each of their hours - are for two hours in their hours of grace,
which is what turns them for home. `confirmed`.

**The hours of grace end with the first move**: every pass of the march of a unit that is in
no battle wipes them, for a battle and for a bombing alike. So a bomber on its way home may
start another bombing in a province it passes, where its mission bombs there. This
**corrects `aircombat.md`**, which had the hour hold wherever the unit went.

## 11. What comes back each day

Each land province, at the end of the day's pass, after every country's. `confirmed`.

    roads = its infrastructure x (1 + what is said of roads there and in its country),
            between a hundredth and one
    rate  = (0.1 + roads) x (1 + its holder's repair_rate)
    each good's daily production: up by the rate, to what it could yield
    each building that is repaired: up by BUILDING_REPAIR_SPEED x the rate, to what was built

A province on full roads gets back 1.1 of each good's production a day and 0.11 of a level of
every building; one with no roads a tenth of that. A building is repaired unless its file
says `repair = no`.

## 12. Worth a look in the original

Built as read, and odd enough that somebody who knows the game should say. None has been raised
for a decision.

1. **A bomber with no leader has a ship on its list one time in a hundred**, each hour, each
   ship. Naval strikes and port strikes then wear the port and hardly touch the ships. Its
   leader's skill counts since 2026-10-11, whatever his rank: ten in a hundred a point.
2. **A fleet in its port fires its air attack at bombers three times over** through the
   province - twice in the sum, times one and a half - and the port's own guns beside. A port
   strike on a battle fleet's base is very costly: 200 shots an hour over Wilhelmshaven.
3. **Two units cannot bomb one province's ground together**: the second finds nobody to be
   against and turns for home.
4. **A wing with no strategic attack bombs as hard as one with a little**: one run an hour,
   interceptors flying with the bombers included.
5. **Interdiction flies only at ground its country sees into** (at 2 or more, `intelligence.md`), and takes the last such
   province of its list whether or not anybody is there; ground attack goes round its list
   and weighs nothing.
6. **Ground attack and interdiction keep their province** from sortie to sortie, and the
   strategic kinds pick afresh each time.
7. **A land unit under bombs fires its plain air attack**, by day or night, supplied or not.
8. **Nothing defends a target** - no air defence, no fort, no screens - which is the air
   battle's oddity again (`aircombat.md`, section 9, item 3, which was called a bug
   in the original).
9. **An installation strike bombs radar only.**

## 13. Trying it, and what is not here

`OpenHOI3.SaveCheck bombing <install> [mission] [attacker] [defender] [days] [seed]` puts two
countries at war, takes the first one's air unit with the most of the attack the mission
fires that has a province of the second's worth the mission within its range, gives it the
mission, and tells the days: each sortie in a line, the first hour by hour, and at the end
what the province has lost. It is a trial and not a check. With nothing said it is Germany's
strategic bombing of Poland for three days. On the start date:

    strategic bombing: four wings from Berlin over Poznan, an hour away. One factory and two
        levels of guns; the province fires 18 shots an hour. A sortie every seven hours; the
        first leaves 0.19 of the factory and 1.64 of the guns. After eleven sorties in three
        days the factory is at 0.11 and the guns at 0.44, with a tenth of a level coming back
        a day, and the bombers have lost 5.5 of 400 strength
    ground attack: the same wings on five Polish units in Poznan, by night at 0.44 of their
        attack, their air marshal's skill of 3 adding 0.15: a division loses about a
        hundredth of a point of strength an hour, and the bombers 16 of their 400 in three
        days to the province's fire. Their air marshal gains 341 experience; the strategic
        bombing, against no units, gives him none, and a twentieth of the way to carpet_bomber
    port strike: British strategic bombers on Wilhelmshaven take the naval base from 10 to
        1.0 in two days, and the German battle fleet in it fires 200 shots an hour: the
        bombers are down from 40 organisation to 4. No ship is hit
    naval strike: the same bombers find the battle fleet off its port twice in two days, and
        hit nothing
    interdiction: never flies - Germany sees into no Polish province

In the client `--mission=GER:strategicbombing:<province>` - or `groundattack`, `interdiction`,
`navalstrike`, `logisticalstrike`, `runwaycratering`, `installationstrike`, `portstrike` -
gives that mission to the country's air unit with the most of what it fires that has the
province in its range. **The orders window gives all eight too**, for the one province
clicked; checked with `--click`. A selected unit's panel names the mission with what it is
doing after it - `Resting`, `Attacking` a province or `Returning` - which are the original's
three texts and **Ours** in when each is shown: `Attacking` while the unit is in a bombing or
on its way to one, `Returning` when it has been told to, `Resting` otherwise. **A bombing has
a window and a row in a unit's panel, and no marker on the map** - the original gives it none:
`combatview.md`, section 11, where the window's list of what is being bombed is. The readout
of a selected province says there is a bombing.

### Not built

- ~~**A carrier's aircraft**: the order that keeps them over their fleet, their bombing of the
  enemy's ships beside a sea battle, and a carrier as a base at all.~~ Built on 2026-10-16
  (`transport.md`, sections 13.1 and 13.2): carrier protection goes for ships in its carrier's
  province alone, and a fleet in a battle sends it in.
- **The reserve.** The supply flight and the paradrop are built since 2026-10-16 (`transport.md`,
  section 13), the atom bomb since then (`nukes.md`), and the raid on convoys from the air - a strike
  whose battle is a bombing of a convoy's ships, its wings taking every ship with no roll - since
  2026-11-14 (`navalmissions.md`, section 8.1).
- **What a strategic bombing does beyond the province**: the threat it adds to its country,
  and what it takes off the bombed country's unity.
- ~~**The practical** a bombing earns~~ - built 2026-11-18 (`research.md`, section 10; `CCombatPractical`); the difficulty is built since 2026-11-06 (`frontend.md`, section 4.3). The
  leaders are built since 2026-10-11 (`leaders.md`, section 10).
- **The part of intelligence that lets a country see into ground beside its units**
  (`aircombat.md`, section 6): without it interdiction has nowhere to fly, and ground attack
  bombs only where it was sent.
- **The damage a land battle does to a province's buildings** when a defender falls back
  (`landcombat.md`), which the two levels of a building now allow.
- The AI's own giving of missions.

### Not established

- What the bracket of a bombing mission's text says when, in a unit's panel.
- Whether flying costs a wing organisation: nothing read takes any.
- **Set beside the running game, 2026-11-10** (`reversing/findings/FINDINGS-liveread.md`, section 9): three land bombings of August 1938, an hour each, from
  the original's generator at each one's start. Every wing's products, its list of targets, every draw to the end of the bombing, every wing's and every unit's
  strength and organisation after it, and what lay in the province are the original's. A bombing's mission now counts in its wings' figures, which it did not.
- Whether damage to a building shows in its province's modifier at once or with the day's
  pass. Here it is the day's pass.
