# A battle at sea

What brings two fleets to battle, where their ships stand in it, what an hour of it does, and
how it ends. This is the first piece of what stage 3 has left: **fleet against fleet**. A
fleet's missions - patrol, intercept, the hunt for convoys - are `navalmissions.md`; the
landing, the battle in the air and bombing are pieces of their own (the readme's *Next up*).

Everything is from `reversing/findings/FINDINGS-seabattle.md`, which joins
`reversing/findings/FINDINGS-navaldetection.md` (the sighting, positioning, breaking off) and
`reversing/findings/FINDINGS-combatmods.md` (what the hour makes of a ship) to a reading of the
rest. **It was read out of the original and nothing of it was watched**: no savegame at hand has
a battle at sea in it, and the original's rolls cannot be made again (`landcombat.md`,
section 8). Each rule says how sure it is; what this project does of its own is marked *Ours*.

In the code: `lib/OpenHOI3.Sim/Combat`, the classes whose names begin `CNaval`, with the hour's
frame in `CCombats`.

## In one paragraph

Every hour a fleet of a country at war that is going nowhere, and every fleet that sails into
a sea province, looks at the first enemy fleet there and rolls to see it. A sighting starts a
battle: the fleet that saw is the attacker, the fleet it saw the defender, and every other
fleet there joins the side whose enemies are its own. Each ship is put at a **distance** of its
own. Each hour every ship that can shoot picks a target - by its side's **positioning**, a ship
in reach, or nothing, or now and then a friend - fires, and then closes the distance or opens
it. A ship whose damage passes its strength is sunk. The battle is over when a side's ships are
out of organisation, when a side has gone, or when both sides break off, which a roll decides
that gets likelier by the hour and much likelier at night.

## 1. What starts one

`confirmed` (read; `FINDINGS-seabattle.md`, section 1) unless marked.

**When a fleet looks.** On entering a sea province, and each hour that it has no path, is not
falling back and is in no battle, for the province it is in. A fleet in port looks at nothing
and is not found: battles between fleets are fought at sea.

**Looking at a sea province**:

1. a battle between fleets is being fought there: the fleet is **offered** to it and joins the
   side whose enemies are all its own (`landcombat.md`, section 1) - **with no sighting**;
2. still in none: it looks for one to **start**.

**Starting one.** The first unit in the province, in the order they came, that is a fleet of a
country at war with the fleet's own and is not falling back, is the one it tries - **and only
that one**: a failed sighting of it ends the look, whoever else is there. Then, in order:

    the other fleet has no ship                                         -> no
    the fleet's wait after its last battle is still running (section 6) -> no
    its country is played by no person, and its order is none or a
        change of base                                                  -> no
    it has submarines, and the other has more screens than a third
        of them, cut to a whole number                                  -> no
    its country knows the sea province at level 9                       -> a battle
    the other fleet is in a battle already                              -> a battle
    otherwise a roll for each pair of ships, theirs outside and its
    own inside, the first that passes -> a battle:
        seen    = its ship's surface_detection
                  - against a submarine half its sub_detection
        figure  = seen x the greater of 1 and the other ship's visibility
                  / 2,  or / 3 where the other fleet is under way
                  x 1.25 for knowing the province at level 2 or more, and twice
                    over again for each of the levels 6, 7 and 8
                  x (1 - rain x SPOTTINGRAINMODIFIER - cloud x SPOTTINGCLOUDMODIFIER)
                  and never under 1
        passes if  roll(10000) < the whole part of the figure

- **An idle fleet of a country nobody plays never starts a battle.** It waits to be found; an
  idle fleet of a player's looks every hour. That is the original's, and with no AI to give
  fleets missions it means the enemy's fleets fight only when found.
- **A ship is one of**: a capital ship (`capital = yes`), a transport (`transport = yes`), a
  submarine (`is_sub = yes`), a carrier (a `carrier_size` above nothing) - asked in that order,
  so a carrier that is a capital ship counts as one - or else a screen.
- **A fleet under way is harder to see**, and each pair's figure is cut to a whole number
  before the roll: with nothing in its favour a pair has one chance in ten thousand.
- **How well a country knows a sea province** is worked out once a day: 2 where it had a unit
  there at the day's turn, 9 where that unit was a patrol shadowing somebody - a sighting with
  no roll - and nothing for water a fleet has only just sailed into. `navalmissions.md`,
  section 9, has it, and what radar would add.
- **A fleet's mission bears on all of this**: a raider starts no battle, a patrol that is not
  aggressive leaves alone a fleet with many more heavy ships, an escort and a patrol find a
  raider by their efficiency against its own, a shadowed fleet is twice as easily seen, and
  water a convoy was attacked in is watched the harder. And **the first enemy fleet** above is
  the first whose order lets it be made the defender: a raider never is, and a patrol by its
  stance. All of it is `navalmissions.md`, sections 3, 4 and 9.

*Not built:* what a leader's `spotting_chance` adds; and a fleet carrying units that are
fighting or under way, which is seen at once.

## 2. Where the ships stand

`confirmed` (`FINDINGS-seabattle.md`, section 2).

**Every ship has a distance of its own**, in the units of its file's `distance` times a
hundred: a battleship's 0.32 is a reach of 32. Two ships are in reach of one another when their
two distances together are no more than the shooter's reach (section 4).

When a fleet joins a side, each of its ships, in order:

    half = half its reach
    x    = 0.3 x half x 5 x (1 - the ship's positioning), and 2 at least
    d    = half + roll(whole part of x)
    a capital ship:   the first of the fleet's sets the fleet's line at its d
    any other:        d, or -d at even odds, added to the line
    its distance is d, and never under 1

A fleet that joins a battle already begun starts **a hundred further out**, its line taken from
the first capital ship of its side that has a distance.

*Not built:* the leader's skill comes off the 5 - an admiral of skill 5 draws his ships up at
exactly half their reach - and his `spread_out` trait off the 0.3.

**Positioning** is one figure for each side, rolled when the battle begins and then crept up:

    at the start:  roll(20) / 100 + 0.4 - the side's stacking penalty, not under nothing
    each hour:     where it is under the greater of 0.4 - the penalty and 0.1, 0.02 more

    stacking penalty = (the hulls of all the side's ships added up - NAVAL_STACK_POS_HULL_LIMIT)
                       x NAVAL_STACK_POS_PENALTY_HULL_MULT
                       between nothing and NAVAL_STACK_POS_PENALTY_MAX

In the base game sixteen hull are free and each one more costs 0.04, to 0.8 at most: a fleet of
eleven battleships fights without a penalty and one of thirty with 0.8. *Not built:* a tenth
for each point of the admiral's skill.

## 3. The hour

`confirmed` (`FINDINGS-seabattle.md`, section 3; `FINDINGS-landbattle.md`, section 3). A battle
at sea is fought in the same place in the hour as one on land and by the same frame, the
attacker first at every step:

1. the count of rounds goes up; the first round's two dice and every round's two;
2. **what the hour makes of every ship** (below);
3. **the attacker's ships shoot and move, then the defender's** (sections 4 and 5);
4. the damage is settled and the sunk ships taken out (section 5);
5. from the fourth round: has the attacker lost, and if not has the defender (section 6);
6. the round's count; and then **the roll to break off** (section 6).

### What the hour makes of a ship

`confirmed` (`FINDINGS-combatmods.md`, section 6; `FINDINGS-seabattle.md`, section 3). Each
thing is put on the **ship**, not the fleet, and multiplied in as on land (`landcombat.md`,
section 4). **Only a ship's attack is ever read**: its defence is worked out, shown, and does
nothing. In the original's order:

| | which ships | attack | defence |
| --- | --- | --- | --- |
| **poor screens** | the capital ships of a fleet with fewer screens than capital ships | - | -(1 - screens / capital ships) / 3 |
| **surprise** | an attacking fleet's submarines | `NAVAL_COMBAT_SUB_SURPRISE_BONUS` | - |
| **experience** | every ship | a hundredth for each point of its own experience, a pride's counting for `PRIDE_BONUS_EXP` more | the same |
| **mission efficiency** | the ships of a fleet whose order is more than a plain move | its country's efficiency at that mission (`navalmissions.md`, section 10) | the same |
| **dissent** | every ship | -`COMBAT_DISSENT_IMPACT` x its country's dissent / 100 | the same |
| **lack of supplies** | the ships of a fleet given less than all its supplies | -`COMBAT_SUPPLY_LACK_IMPACT` x the share it went without | the same |
| **the weather** | every ship | -(wind speed x `NAVALWINDSPEEDMODIFIER` + rain x `NAVALRAINMODIFIER`) | - |
| **the night** | every ship | `BASE_NIGHT_PENALTY` + the ship's own `night` figure - its attack figure on the attacking side, its defence figure on the defending - where that is below nothing | - |

- A screen is a ship that is neither a capital ship, a submarine nor a transport.
- **The submarines' surprise is rolled in the first round only**: each attacking fleet with a
  submarine rolls `roll(100) < NAVAL_COMBAT_SUB_SURPRISE_CHANCE` until one succeeds, and from
  then every submarine of that fleet and of the fleets after it has the bonus for the first
  `NAVAL_COMBAT_SUB_SURPRISE_ROUNDS` rounds - five times its attack on top, in the base game,
  at three chances in ten, for three hours.
- **There is no weather yet**, so its row comes to nothing.

*Not built:* the difficulty setting, and the leader - his skill, and his traits on experience,
the night and supplies.

## 4. Who a ship shoots at

`confirmed` (`FINDINGS-seabattle.md`, section 4). A ship with a `sea_attack` and any
organisation picks a target each hour:

    p = (the ship's positioning + its side's) x 100, a whole number;  half that at night for
        the roll marked *

    it has a target from the hour before that is still there:
        roll(50) > p + 10                 -> it loses it
        at night: roll(100) > 80          -> it loses it      (rolled only at night)
        still held:
            another country's ship: its fleet is still on the other side -> keep it
            one of its own country's:  roll(100) < 75                    -> let it go

    with none:
        roll(80) > p *:                   the ship has lost the enemy
            roll(100) < 7:                it takes one of its own side for the enemy (below)
            otherwise                     -> no target
        otherwise, of the enemy ships it can reach:
            none                          -> no target
            roll(100) <= its targeting    -> the best of them
            otherwise                     -> any one, by a roll

- **Which it can reach**: an enemy ship whose distance and its own together are no more than
  its reach (its `distance`, x `1 + rain x FIRINGRANGEMODIFIER`), that this hour's notes have
  not already sunk; a submarine only where the ship has a `sub_attack`, and - unless the
  submarine is itself shooting at something - only on `roll(100) <= 5 x sub_detection`.
- **The best** is the one with the largest `priority x (0.25 + a hundredth of the strength it
  has lost)`: the most valuable by the unit file's `priority`, and of two alike the more
  damaged; the first of equals.
- **A ship keeps its target from hour to hour**, and with a positioning of 0.4 or more never
  loses it by day.
- **Friendly fire is the original's.** A ship whose side's positioning fails it has seven
  chances in a hundred of shooting at its own side. The original's walk to pick which ship does
  not stop where its roll points: it ends on **the last ship of the side's last fleet** (the
  shooter left out), unless the roll pointed past every ship, and then there is no target. It
  is built so. Such a target is kept a quarter of the time each hour.

*Not built:* the admiral's skill comes off the 7 and is the ship's targeting, with a tenth of a
technology figure, so the best target is taken on a roll of 0 alone; a technology figure scales
`p`.

## 5. The shot, the damage, and the distance

`confirmed` (`FINDINGS-seabattle.md`, section 3). **Every roll here is the game's own
generator**; a side has no stream of its own at sea.

    attack = sea_attack;  against a submarine, sub_attack
    shots  = attack x what the hour makes of the ship's attack
             whole shots, and the rest a chance of one more
    for each shot, while this hour's notes have not sunk the target:
        organisation die = NAVAL_COMBAT_ORG_DICE_SIZE + 5 x the ENEMY side's stacking penalty
        strength die     = NAVAL_COMBAT_STR_DICE_SIZE + 3 x it
                           each a whole number, the rest a chance of one more
        one roll that nothing reads
        roll(100) < CHANCE_TO_AVOID_HIT_AT_NO_DEF, 99 at most   -> a miss
        organisation = roll(organisation die) + 1
        strength     = roll(strength die) + 1
        roll(100) < NAVAL_COMBAT_CRITICAL_HIT_DAMAGE_CHANCE:
            strength x NAVAL_COMBAT_CRITICAL_HIT_DAMAGE_MUL
        both / the target's hull
        strength x NAVAL_COMBAT_STR_DAMAGE_MODIFIER,  organisation x NAVAL_COMBAT_ORG_DAMAGE_MODIFIER
        both are noted on the target
        the target's notes now pass its strength: this ship has sunk it

**A ship has no defence.** `sea_defence` and `surface_defence` are read by nothing in the
original's fight (`reversing/findings/FINDINGS-airdefence.md`); `hull` is what protects a ship,
as a plain divisor, and every shot is avoided at the one flat chance. A crowded fleet is hit
harder: its penalty widens both of the dice thrown against it.

In the base game a hit takes `1 / hull` organisation and between 0.12 and 1.2 over the hull in
strength - ten times that one time in ten - of a ship with a hundred strength and thirty-odd
organisation: a battleship gives way long before it sinks.

**Then the ship moves**, whether or not it shot:

    its fleet is not breaking away and it has organisation left:
        a fleet with a carrier in it                       -> it stays where it is
        it has a target:  further out than half its reach  -> closer by a quarter of its speed
                          otherwise                        -> it stays
        it has none:                                       -> closer by its whole speed
        and closer again by what the enemy's ships are faster than its own side's, on average
    otherwise:  further out by half its speed over its own hull
    never nearer than 1

The speed is the ship's `maximum_speed`; the averages are over the ships of the fleets on each
side that are not breaking away. A ship with no `sea_attack` never has a target, so **a
transport runs in at its whole speed** until it is at 1; that is the original's.

**At the end of the round**, each side:

- a ship whose notes pass its strength is counted as lost;
- every ship's notes are taken off it **whole** - a brigade's are divided by a hundred, a
  ship's are not - and neither more than it has;
- **every ship under 0.1 strength is sunk**: out of its fleet, and onto its side's list.

*Ours:* a fleet whose last ship is sunk is removed from the game at once; what the original
does with the empty fleet was not followed.

*Not built:* a pride of the fleet that is sunk gives the ship that sank it 5 experience and
fires `on_enemy_pride_sunk` and `on_our_pride_sunk`; a round gives each country
`NAVAL_DOCTRINE_INCREASE` of a practical; a transport's damage is shared out over the land
units it carries, and what a fleet can no longer carry goes overboard; the leader's
`submarine_attack` trait; and a carrier's aircraft, which the original sends into the battle
from here.

## 6. The end

`confirmed` (`FINDINGS-seabattle.md`, section 5).

**A side has lost**, asked from the fourth round, the attacker first: it has under 0.01
strength in all, or **its ships average under one organisation**. Its fleets fall back and the
battle is over.

**Falling back.** A beaten fleet goes to the province next to it that is worth most, among
those it may stand in by a way that is not closed to it:

    the port its sea province belongs to, where it may fall back there      10000
    a sea province                                                          1000, and 2000 for
        each province beside THAT one that is land with a naval base of a level or more that
        the fleet may stand in
    the first found is kept, and replaced only by one worth more

It is given the way there and is falling back until it arrives - nobody's enemy, starting
nothing (`landcombat.md`, section 7). **It keeps its order.** A fleet with nowhere to go, or no
way there, is removed from the game.

**Breaking off.** After the round, from the third:

    roll(1000) < rounds fought + 11, or + 21 at night
                 + wind speed x NAVALWINDSPEEDMODIFIER + rain x NAVALRAINMODIFIER:
        every fleet, the attacker's first: its count goes to 1, or up by one
        the first fleet whose count has reached 5 -> the battle is over

A fleet's count also goes up by one every hour once it is above nothing, as the last thing its
side does when it shoots. So **the first roll that passes sets every fleet counting and their
ships opening the distance, and the battle ends at the next roll that passes four hours or more
later** - with nobody beaten and nobody falling back. In the base game the first roll passes
after a day or so of daylight, and about twice as soon by night.

**Leaving by order.** A fleet in a battle that is told to go somewhere is not given the way: the
province is kept, and its count is started. When the count reaches 5 - four hours on - it sets
off, leaves the battle, and is falling back where the other side still has somebody. **And no
fleet leaves a battle at sea in its first four rounds**: until then nothing gives it a path.

**The wait.** A fleet let out of a battle - at its end, or by leaving - starts no battle for
`8 + roll(8)` hours. Entering another sea province ends the wait; being found does not need
it. A fleet that fell back out of a lost battle has none: it is falling back instead.

*Not built:* the leader's `disengage_timer` trait scales the 5; and the battle's "decided"
message, its history entry and the on-actions (`landcombat.md`, section 7).

## 7. Trying it, and what is not here

**A trial.** `OpenHOI3.SaveCheck seabattle <install> [attacker] [defender] [seed]` takes the
strongest fleet of each of two countries, puts them at war and in one sea province, has the
first played by a person, and says how it went: the hour of the sighting, where the ships
stood, each day's hits and what was left, and the end. It is not a check - there is nothing to
set it against. With no tags it is Germany against the United Kingdom. On the start date,
with a seed of 1 unless one is given:

    Germany at the United Kingdom,     two heavy and six light cruisers and a destroyer against
    Helgoland Bight:                   the 26 ships of the Mediterranean Fleet, six battleships
                                       and two carriers among them: sighted in the first hour;
                                       positioning 0.56 against 0.02, the British paying 0.8 for
                                       their crowd; the Germans are out of organisation after 17
                                       hours and fall back to Wilhelmshaven with little over half
                                       their strength; one British destroyer sunk, and none German
    the United Kingdom at Germany,     the same two with the sides changed: the Germans give way
    seed 2:                            after 24 hours; a British destroyer and a heavy cruiser sunk
    Japan at the USA:                  five ships against six, sighted after three hours, both
                                       break off after ten; two Japanese heavy cruisers sunk
    Italy at France, seed 3:           eighteen ships against nine: the French give way after 18
                                       hours with three ships left of nine

The crowded fleet's sunk destroyers are worth a second look when somebody can watch the
original: a side at a positioning of 0.02 finds almost no targets, and what it does shoot at is
often its own last ship (section 4).

**In the client** there is no AI, so no enemy fleet is ever at sea: `--sail=ENG:10531` sends
the strongest fleet of a country that can reach that sea province there, and with
`--war=GER:ENG` the player's own fleet has something to find.

**On the map** a battle at sea has the land battle's marker, its row in a fleet's panel, and
the same window with halves of its own - a list of each side's ships, each ship's picture
standing along its row by its distance: `combatview.md`, section 10. *Ours:* the readout of a
selected sea province with a battle in it says who fights whom, with how many ships, and how
many each has had sunk.

### Not built

- **Missions** are built since 2026-10-10 and are `navalmissions.md`: reserve, patrol,
  intercept, sortie, the raid on convoys and their escort - and with them the battle a raid
  fights, in which the attacker shoots with `convoy_attack` and nobody is asked whether they
  have lost. **Transport and invasion** are not: they wait for carrying.
- **Aircraft**, a carrier's own among them, and every kind of bombing.
- **Intelligence** beyond a unit's own part in it (section 1).
- **Leaders**, wherever one is named above.
- **The weather itself.**
- **The AI**: no fleet of a country nobody plays moves or looks for a fight.

### Not established

- **Nothing here was watched.** The order ships are drawn up in and who is shot at decide the
  course of a battle, and both rest on the reading alone.
- ~~What the byte the original keeps on a battle and reads in five places is set by.~~ By the
  raid on a convoy, and by nothing else: `navalmissions.md`, section 8.
- What keeps a unit out of a battle for the count the original runs down an hour at a time
  beside the fleet's own wait.
