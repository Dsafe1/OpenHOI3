# Leaders

Who commands the units: the generals, admirals and air marshals of `history/leaders`, their
skill and traits, which of them are in service, and which unit each is given. Written as rules
this project can implement. No addresses and no offsets. Each rule says how sure it is -
`confirmed`, `likely`, `inferred` in the fact base's vocabulary. Where this project chooses its
own behaviour the paragraph is marked **Ours**.

Sources: `reversing/findings/FINDINGS-leaderassign.md` and
`reversing/findings/FINDINGS-commandreach.md`, read for this, whole;
`reversing/findings/FINDINGS-leaders.md` (experience, skill, traits, promotion);
`reversing/findings/FINDINGS-history.md` (the leader's history and its five keys).

Code: `CLeader`, `CTrait`, `CLeaders` and `CCommandReach` in `lib/OpenHOI3.Sim/Leaders`;
`CLeaderReader` in `lib/OpenHOI3.Content/History` and `CTraitReader` in
`lib/OpenHOI3.Content/Definitions`; the panel's lines in `CUnitPanelText`;
`OpenHOI3.SaveCheck`'s `CLeaderCheck`.

Leaders were cut in four on 2026-10-11 - **the cut is this project's**: who they are and who
commands what (sections 1 to 7, built); how far a commander's command reaches, and what it does
to organisation and to what a unit uses (section 8, built the same day); what a leader does in a
battle, with the experience and the traits he gains there; and the window a leader is chosen
in.

---

## 1. Leaders as the files give them

Every file of `history/leaders` holds a block for each leader, named by his number, which is
**unique across the game** - an order of battle and a savegame name a leader by it alone.
`confirmed`

| key | |
| --- | --- |
| `name` | as written, in the files' own code page |
| `country` | whose leader he is |
| `type` | `land`, `sea` or `air` |
| `skill` | his skill, and the skill he is put back to |
| `max_skill` | not a cap - section 2 |
| `loyalty` | a figure |
| `picture` | his portrait, `gfx/pictures/portraits/` + it + `.tga` |
| `add_trait`, `trait` | a trait, each line one more; none is refused for his kind |
| `history` | dated blocks of `rank`, `loyalty`, `add_trait`, `remove_trait` and `skill` |

A save adds `id`, `current_experience`, `experience`, `gainable_trait` and `rank`. *Ours:*
any other key is reported, where the original drops it.

**His history is folded to the start date** as every history is (`map.md`, section 7): the
blocks dated on or before it, in date order, blocks of one date in their order in the file.
**A dated `skill` does nothing** - the original reads it and applies it to nothing.
`confirmed`

A country keeps its leaders in the order they were read: the files in the order the install
lists them, each file top to bottom. `confirmed` against the save, below.

## 2. Skill and experience

Experience is held in whole points with 15 binary places, and each skill has a floor:
`confirmed`

| skill | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| experience | 0 | 1000 | 2000 | 4000 | 8000 | 12000 | 18000 | 25000 | 33000 | 43000 | 60000 | 100000000 |

**A leader read from a file starts at the floor of his skill**; where that leaves him at
nothing, at the experience his file gives (in thousandths of a point); and then his skill goes
up for as long as his experience is **strictly above** the next floor. Neither is ever
lowered, so the file's skill is the least he has. `confirmed`

`max_skill` is not a cap: it sets how fast he learns (`reversing/findings/FINDINGS-leaders.md`,
section 1). Nothing during play changes it. **How a leader learns is not built** - he learns
only in battle, which is the third piece.

## 3. Traits

`common/traits.txt`: a block for each trait, `allowed_leader = { land sea air }` - all three
where it is missing - and its effects. **A trait has at most sixteen effects**; the original
drops the rest in silence. *Ours:* they are reported. Each effect is one of 31 kinds, numbered
in the order the comment at the head of the file lists them, `xp_gain` 0 to `terrain_defence`
30; the three terrain kinds are blocks of `type` and then `value`. `confirmed`

`allowed_leader` is asked only of the traits a leader earns in battle; a trait his file or a
script gives him is his whatever it says. `confirmed`

## 4. Who may command what

There are two tests, and **the one that counts is looser than the one that chooses**.
`confirmed`

**May he be given it** - when a leader is chosen for a unit:

- he is in service (his rank is not 0) and of the unit's kind: a land unit takes a `land`
  leader, a fleet a `sea` one, an air unit an `air` one;
- he commands no other unit;
- and a land unit asks for **one rank exactly**:

| | rank |
| --- | --- |
| a division | 1, and only a division of **more than one brigade** |
| a corps | 2 |
| an army | 3 |
| an army group, a theatre | 4 |

  A fleet or an air unit takes any rank.

**Has he the rank for it** - what the panel writes his name in red for, and what a battle
asks before his skill counts:

| | rank |
| --- | --- |
| a theatre, an army group | at least 3 |
| an army | at least 2 |
| a corps | at least 1 |
| a division | anything |
| a fleet, an air unit | his rank's command at least its ships or wings |

**A rank's command** is `NAVAL_RANK_1` to `NAVAL_RANK_4` ships, or `AIR_RANK_1` to
`AIR_RANK_4` wings, by his rank, a rank that is none of 2, 3 or 4 counting as 1. Their Finest
Hour gives 6, 12, 18 and 30 ships, and 4, 8, 12 and 16 wings. `confirmed`

## 5. Who is given whom

### As a game is made

A country at a time, once its units are read: `confirmed`

1. **Every one of its leaders whose rank is not 0 is put in service**, in the order it keeps
   them (section 1). His rank is what his history makes it by the start.
2. **A unit its order of battle gives `leader = N` is given leader N - whoever he is**: of any
   country, any rank, any kind, in service or not. Nothing is asked.
3. **Its starting stockpile is worked out** (`stockpile.md`, section 2) - so the days of
   supplies and fuel in it are of what its units use with only the leaders of step 2
   (section 8).
4. **Every unit still without one is given the best of the country's free leaders**, in the
   order the units were made. This is not asked of the country's setting below.

**The best** is worked out over the country's leaders in service, in the order it put them in
service, leaving out any with a unit and any the first test of section 4 refuses: `confirmed`

    worth = (skill + 1) x 1000
    a fleet or an air unit with more ships or wings than his rank commands: worth / 100
    and for every effect of every trait of his, what its kind is worth to the unit:

| kind | adds | where the unit |
| --- | --- | --- |
| `defence_modifier` | 10000 | is slower than 3 - its slowest brigade |
| `offence_modifier`, `combat_move_speed` | 10000 | is faster than 4, or else its mix of brigades is worth more than 50 |
| `river_attack`, `fort_attack`, `amph_attack` | 20000 | has an attack above nothing in that adjuster, its brigades' mean |
| `combined_arms_bonus` | 50000 | has any worth in its mix of brigades |
| `submarine_attack` | 20000 | has a submarine for its first ship |
| `defender_softness` | 5000 | has more soft attack than hard |
| `strategic_attack` | 10000 | has more strategic attack than air attack |
| `naval_attack` | 10000 | has more sea attack than air attack |
| `tactical_attack` | 10000 | has more soft and hard attack together than air attack |
| `paradrop_mission` | nothing | - see below |
| any other | nothing | |

"Its mix of brigades" is the combined arms bonus before its country's technology
(`landcombat.md`, section 4); the unit's attacks are its brigades' that still count, added up
(`landcombat.md`, section 2). The effect's own figure plays no part. **The highest worth wins,
and of equals the first in service**; anybody who may be given the unit is better than nobody.
A unit nobody may be given keeps none.

### During play

**A country that chooses its own leaders** - `auto_assign_leaders`, on for every country as a
game starts - gives one the same way to: `confirmed`

- **a unit put on the map** from the build queue (`buildqueue.md`, section 7);
- **a unit a lone brigade, ship or wing joins**, where it has none - while the unit that gave
  it up still holds whoever it was given when it was put down, so he is not to be had - and
  that one, empty, is then gone and its leader free;
- a new headquarters, and a unit a brigade is moved into - neither of which is built here.

A unit put under a headquarters gives that headquarters nothing. **A unit that leaves the game
frees its leader.** `confirmed`

**Putting a leader on a unit** takes him off whatever he commanded and the unit's old leader
off it. Two things may come with it: the hours of battle he had banked towards a trait are
thrown away, and every brigade of the unit keeps only `NEW_LEADER_ORG_HIT` of its
organisation (0.5) - unless a custom game's setting forbids it. **Neither comes with the
choosing above, nor with an order of battle's leader**; both come when a player changes a
leader, which is not built. `confirmed`

### Each day

A country's day, just after its manpower grows: **a leader whose history first gives him a
rank today, and who is not in service, is put in service at rank 1** - whatever rank the
history gives. `confirmed`. "First" is the earliest dated `rank` in his history: in the base
game that is always the first written, which is what the original takes; which it means where
they differ was not read. A leader whose history has no `rank` is never put in service.

## 6. What the save says

`OpenHOI3.SaveCheck <install> <save>` sets the leaders of a new game beside a savegame two
days into one (`Germany1936_01_01_00.hoi3`): `confirmed`

| | agree |
| --- | --- |
| each country's leaders in service, in order | 101 of 101 |
| a leader's rank, experience and traits | 3303 of 3303 each |
| a unit's leader, the country played | 65 of 65 |
| a unit's leader, a country the AI runs | 1490 of 1490 |

The base game's orders of battle give **8** units a leader; the save has **1355** with one,
and 31 of those are headquarters the original's AI made in its two days, which took leaders
nobody else was given. So **the choice of section 5 is the original's, unit for unit**, and
whatever its AI does about leaders in those days moved nobody.

And with section 8 built, what the leaders do (**measured**, the same save):

| | before section 8 | with it |
| --- | --- | --- |
| organisation of a brigade under an army with a commander | 142 of 764 | 764 of 764 |
| what a capital's units use, where the save gives one there a leader | 17 of 28 | 27 of 28 |
| the same, where the save also has a unit the files do not give | 19 of 35 | 33 of 35 |
| supply networks the same in every province and figure (`supplypass`) | 171 of 234 | 184 of 234 |

The capital that differs is Moscow, whose figure fits the Comintern's faction modifier, which is
not known to reach a country (`upkeep.md`, section 1). 95 land units start out of radio range,
none of them Germany's - a look, since a save keeps nothing of it.

## 7. On screen

The panel of a selected unit (`unitpanel.md`, section 3) has its leader's name - in red where
he has not the rank for it - or `NO_LEADER`, and his portrait; and a row of a unit under it
gives its leader's name in brackets. `confirmed` *Ours:* a portrait the install has not got
leaves the file's picture, where the original would show what an absent texture shows.

A counter carries its unit's name and not its leader's: the maintainer's decision
(`docs/compatibility.md`, entry 7).

The radio icon of the panel's status row (`unitpanel.md`, section 3) and the line of a unit on
the map (`mapunits.md`, section 8) say whether it is out of radio range: section 8.

## 8. How far a command reaches

A headquarters' command reaches the units under it by radio. Everything is in thousandths, with
every division dropping its remainder, as the original works it. `confirmed`

**Its radius.** A theatre, an army group, an army or a corps reaches in full as far as

    radius = (RADIO_<level>_LEADER_DISTANCE x radio_strength of its first brigade) / 1000, / 1000

in the game's kilometres, which are quarters of a pixel of the map - the measure an aircraft's
`range` is in (`fleetsandair.md`, section 3) - where the defines are `RADIO_THEATHRE_`,
`RADIO_ARMYGROUP_`, `RADIO_ARMY_` and `RADIO_CORPS_LEADER_DISTANCE`, 1000, 300, 200 and 100 in
Their Finest Hour, and `hq_brigade` has a radio of 2: **2000, 600, 400 and 200 km**. A
headquarters with no brigade reaches nothing.

**One link** - how much of a headquarters' command reaches a unit directly under it:

    d2 = the square of the distance between their provinces' centres, the short way round
    if the unit is nowhere:                                       0
    if the headquarters is nowhere, or is a division or below:    1000
    if d2 is 0 or at most radius^2:                               1000
    if d2 is over 4 x radius^2:                                   0
    else, with d the whole root of d2:                            1000 - (d - radius) x 1000000 / (d x 1000)

so **all of it within the radius, the radius over the distance out to twice the radius - from
1000 down to 500 - and then nothing at all.** A corps reaches a division 300 km off by 667, one
400 km off by 500, and one a kilometre further by nothing. A fall that ended at nothing on
twice the radius would divide by the radius where this divides by the distance; whether that
was meant is not known, and this is what the original does.

**Up the chain** the links multiply, each measured from the unit below it: a division's reach
from its army is its corps' reach of it times the army's reach of the corps.

**Out of radio range** is any link between a unit and the top of its chain letting less than all
through. A unit nobody commands is never out of range. It is what the radio icon's second frame
and a broken dark red line on the map say; nothing else asks it.

**A trait down the chain.** What one kind of trait effect comes to for a unit is its own
leader's effects of that kind added up, and **half** of what the unit above it comes to, by the
link's reach - halved toward nothing, so -0.133 becomes -0.066. So a commander's trait is worth
half to the units under him and a quarter to theirs.

**What it does**, as built:

- **The army's commander adds his skill to the most organisation** a brigade may have, times
  the reach of the army: `organisation.md`, section 2. The army is the unit itself, or the army
  above it with at most a corps between; nobody else's skill counts there.
- **The army group's commander takes a twentieth off what a unit uses for each point of his
  skill**, times the reach of the army group, and **the leaders' `supply_consumption` traits**
  come down the chain to it: `upkeep.md`, section 1.
- A headquarters with nobody in command has a skill of nothing and gives nothing.

**Each rank's skill has a part of its own.** The maintainer's account of the original,
2026-10-11, set beside what is read:

| commander | his skill | his traits, to a unit under him | here |
| --- | --- | --- | --- |
| a division's | 5% combat efficiency a point | all | the third piece |
| a corps' | the chance a reserve division comes up (`landcombat.md`, section 3) | half | the third piece |
| an army's | 1 of organisation a point | a quarter | built |
| an army group's | 5% less supply used a point | an eighth | built |
| a theatre's | 1% less of the stacking penalty a point | a sixteenth | the third piece |

The shares of the traits are the halving at every step, counted from a division with every
level above it; a division straight under an army has half the army's, not a quarter.

**What the reach scales**, every place the original asks it
(`reversing/findings/FINDINGS-commandreach.md`, section 12): `confirmed`

- the army commander's skill in organisation, and the army group commander's in what a unit
  uses - through the whole chain up to him;
- the corps commander's skill in a reserve's chance of coming up - the third piece;
- every trait effect that comes down from above, each step by its own link - the leader's
  own traits are his unit's whole, at any distance;
- whether a unit is out of radio range, on screen; and the original's AI, which walks a
  headquarters towards a subordinate it does not reach in full.

**Nothing asks it at a theatre's level**, so the theatre commander's skill, whatever it does,
is not cut by distance; nor is a division's own leader's.

Every other trait effect the chain brings, and a commander's part in a battle, are the third
piece.

## Kept as read, though they look odd

1. **An order of battle's leader goes on whoever he is**: another country's, a rank that does
   not fit, a leader not in service.
2. **The day's putting in service gives rank 1, whatever the history says, and nothing later
   in a history is ever applied**: a general whose history makes him rank 2 in 1943 stays at
   1 unless promoted.
3. **A division of one brigade is never given a leader** by the choosing.
4. **`paradrop_mission` never counts towards choosing a leader**: the original asks it of the
   figures a unit is made of, which never carry the flag a brigade's type has. A paratroop
   general is chosen for paratroopers on his skill alone.
5. **`defence_modifier` suits only a unit slower than 3** - in the base game, one with a
   garrison brigade in it; militia, at exactly 3, does not count.
6. **The rank that counts is looser than the rank that is chosen**: an army group led by a
   rank 3 general is not in red, though none would be chosen for it.
7. **A trait's figure plays no part in the choice**, and a trait with two suitable effects
   counts twice.
8. **A dated `skill` in a leader's history does nothing.**
9. **A command's reach falls from half to nothing at exactly twice the radius** (section 8): a
   unit a step past it is reached by none of it.
10. **A new game's stockpile is reckoned before leaders are chosen** (section 5), so the days of
    supplies and fuel it is given are of what its units would use with none but the files'.

## Not built

- **What a leader does in a battle**, and the experience and traits he gains there - the third
  piece; with it every trait effect but `supply_consumption` that comes down the chain.
- **The window a leader is chosen in**, promotion and dismissal, and the setting
  `auto_assign_leaders` on screen - the fourth. Nothing turns the setting off yet.
- **The AI's own reshuffle of its leaders**, and the headquarters it makes; leaders made,
  killed or changed by events.

## Not established

- What an order of battle naming an unknown leader does in the original. *Ours:* it is
  ignored.
- Which "first" `rank` the day's putting in service means where a history is not written in
  date order.
- Why Germany's stockpile of supplies two days in is 0.030 above the save's, where it was 3.264
  before section 8.
