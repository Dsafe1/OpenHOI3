# Leaders

Who commands the units: the generals, admirals and air marshals of `history/leaders`, their
skill and traits, which of them are in service, and which unit each is given. Written as rules
this project can implement. No addresses and no offsets. Each rule says how sure it is -
`confirmed`, `likely`, `inferred` in the fact base's vocabulary. Where this project chooses its
own behaviour the paragraph is marked **Ours**.

Sources: `reversing/findings/FINDINGS-leaderassign.md`,
`reversing/findings/FINDINGS-commandreach.md`, `reversing/findings/FINDINGS-battleleaders.md`,
`reversing/findings/FINDINGS-seaairleaders.md`, `reversing/findings/FINDINGS-battlegains.md` and
`reversing/findings/FINDINGS-leaderwindow.md`, read for this, whole;
`reversing/findings/FINDINGS-leaders.md` (experience, skill, traits, promotion);
`reversing/findings/FINDINGS-history.md` (the leader's history and its five keys).

Code: `CLeader`, `CTrait`, `CLeaders` and `CCommandReach` in `lib/OpenHOI3.Sim/Leaders`;
`CCombatLeaders` in `lib/OpenHOI3.Sim/Combat`, beside the battles that ask it;
`CLeaderReader` in `lib/OpenHOI3.Content/History` and `CTraitReader` in
`lib/OpenHOI3.Content/Definitions`; the panel's lines in `CUnitPanelText`;
`OpenHOI3.SaveCheck`'s `CLeaderCheck`. The window a leader is chosen in: the four commands
`CAssignLeaderCommand`, `CPromoteLeaderCommand`, `CRemoveAllLeadersCommand` and
`CToggleAutoAssignLeadersCommand` in `lib/OpenHOI3.Sim/Commands`; the runtime's sort, `CStdSort`,
at the root of `lib/OpenHOI3.Sim`; `CLeaderWindowText`, `CLeaderTraitFilter` and `ELeaderSort` in
`lib/OpenHOI3.Interface/Screens`; `CLeaderSelectionPanel`, `CLeaderEntry`, `CTraitIconEntry`,
`CTraitFilterRow` and `CConfirmDialog` in `src/Screens`.

Leaders were cut in four on 2026-10-11 - **the cut is this project's**: who they are and who
commands what (sections 1 to 7, built); how far a commander's command reaches, and what it does
to organisation and to what a unit uses (section 8, built the same day); what a leader does in a
battle, with the experience and the traits he gains there; and the window a leader is chosen
in (section 12, built the same day, with his place in a battle's window). The third was cut
again, the same day and also this project's: a land battle (section 9, built); a battle at sea
and in the air (section 10, built the same day); and the experience and traits a battle gives
(section 11, built the same day). **All four are built.**

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
   country, any rank, any kind, in service or not. Nothing is asked. **Where no file declares a
   leader N, the line does nothing at all** - nothing is logged or made, and the unit keeps what an
   earlier `leader` line gave it, or none, so that step 4 gives it one
   (`reversing/findings/FINDINGS-modifierkinds.md`, section 5). `confirmed`. **Ours**: such a line
   is reported. No order of battle of Their Finest Hour has one; BlackICE has 33, none in a start file.
   **The lines go on in the order the file reads them**, each as it is read, and putting a leader
   on takes him off whatever unit holds him - another country's included. So a leader named twice
   ends on the unit whose line is read later, and a headquarters' own line is read before those of
   the units under it, though their numbers - each unit numbered as its block closes - put them
   first: Finland's file of 1941.12.11 names Heinrichs on its army group and again on the army
   under it, and the army keeps him. *Measured* (`CLeaders.SetAtStart`).
3. **Its starting stockpile is worked out** (`stockpile.md`, section 2) - so the days of
   supplies and fuel in it are of what its units use with only the leaders of step 2
   (section 8).
4. **Every unit still without one is given the best of the country's free leaders**, in the
   order the units were made. This is not asked of the country's setting below.

**The countries go one after another, all four steps each**, in the countries' order. So a later
country's file that names a leader takes him off an earlier country's unit only after that
country has chosen, and the unit is left with none - its choosing passed it over as led, and the
next best went to the unit after it, and so on down. Italy's file names Rommel, Germany's, on its
Afrika Korps: at 1941.6.22 Germany's choosing gives him to 9. Armee, its first army without one,
Italy takes him, and the save's 9. Armee has no leader with every other army's leader one place
on; at 1941.12.11 Italy's and Finland's files, naming German generals, leave Wbf. Norwegen and
three German divisions without one, and Romania's file takes back Ionescu, whom Germany's names on
a division Romania lent it. *Measured*: with the countries taken in turn, every unit of the three
later start saves has the save's leader but one fleet at each, below (2026-11-15; 6 units differed at
1941.6.22 and 118 at 1941.12.11);
the 1936 start's 1,555 are unchanged. Ours keeps the steps apart - every country's step 2 first,
then everything between, then a country at a time its stockpile and its choosing
(`CLeaders.AssignAtStart`) - and holds back only the lines that could take a leader from an
earlier country, his being an earlier country's or an earlier country's file naming him, to that
country's turn; the rest go on as the units are read. **Not ours**: a fleet's leader at a later
start where the AI regroups the fleet before the first screen - France's Escadre de Mediterrean at
1941 and the Soviet 10ya Flote at 1939 hold Abrial and Isakov in the save, the more skilled leader
a merge keeps, where the choosing gave Decoux and Arushanyan for their `seawolf`, the first ship
being a submarine (`agents.md`).

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
game starts, and **put back on every day for every country nobody plays**, with its three automatic convoy switches -
maintaining, supply and resource convoys - so a save's `no` on such a country lasts until its first midnight and only a
player's checkbox keeps it off (built 2026-11-21, `CAutomationSwitches`; `reversing/findings/FINDINGS-leaderassign.md`,
8.1a) - gives one the same way to: `confirmed`

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
leader (section 12), and only where he puts one on - taking a leader off costs nothing.
`confirmed`

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

The capital that differed is Moscow, whose figure fits the Comintern's faction modifier: with factions'
modifiers applied (`factions.md`, section 1) it is 28 of 28. 95 land units start out of radio range,
none of them Germany's - a look, since a save keeps nothing of it.

## 7. On screen

The panel of a selected unit (`unitpanel.md`, section 3) has its leader's name - in red where
he has not the rank for it - or `NO_LEADER`, and his portrait; and a row of a unit under it
gives its leader's name in brackets. `confirmed` *Ours:* a portrait the install has not got
leaves the file's picture, where the original would show what an absent texture shows.

A counter carries its unit's name and not its leader's: a decision made here
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

**Each rank's skill has a part of its own.** An account of the original,
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

## 9. In a land battle

`landcombat.md` has each rule where it acts; `reversing/findings/FINDINGS-battleleaders.md`
gathers them. In short: `confirmed`

- **A unit's own leader**, where he has the rank for it and some skill, adds his skill x
  `COMBAT_LEADER_IMPACT` (0.05) and his offence or defence trait and his trait for the ground
  to its attack and defence (`landcombat.md`, section 4) - the division's "5% a point". He also
  takes a hundredth a point off where it shatters (section 6) and, defending, puts his skill
  into the order of its front (section 3), whatever his rank.
- **Every other trait** goes into the figure it names - river, landing, fort, experience,
  combined arms, dissent, supplies, dug in, night, envelopment - down the chain of command.
- **The corps commander** brings his divisions up from reserve sooner, ten a point by his
  reach, where the corps stands in their province or the next (section 3).
- **The theatre commander** takes a hundredth a point off every unit's crowding under him,
  wherever he is (section 4).
- **A side's leader** - its highest ranked - picks its tactic: the one of less skill first,
  and `skill`, `skill_advantage` and `trait` in a tactic's conditions ask about him (section
  11).
- **A unit's march** is quicker or slower by the chain's `combat_move_speed` while it attacks,
  and by its `terrain_speed` for the next province's ground (`movement.md`, section 5).

## 10. At sea and in the air

`reversing/findings/FINDINGS-seaairleaders.md`, which gathers what the fact base had read
before and checks it off the instructions; `navalcombat.md`, `aircombat.md` and `bombing.md`
have each rule where it acts. `confirmed`

**Three leaders are asked, and not the same one each time:**

| who | what he does | rank asked |
| --- | --- | --- |
| a fleet's, an air unit's or a bombed unit's own leader | his skill x `COMBAT_LEADER_IMPACT` on every ship's or wing's attack and defence, last of the hour's modifiers | yes, and some skill |
| the same | draws a fleet's ships up closer, five less his skill times the spread (`navalcombat.md`, section 2) | no |
| the same | ten a point to a ship's or a wing's targeting - a point a hundred of taking the best target | no |
| the same | ten in a hundred a point that a bomber has a ship on its list (`bombing.md`, section 6) | no |
| a side's highest ranked, first of equals | a tenth a point on its positioning at sea, and on what it creeps up to | rank above 0 |
| the same | a point a hundred off the chance a ship that has lost the enemy shoots at a friend | rank above 0 |

The rank asked for the bonus is the land battle's ladder, which for a fleet or an air unit is
whether his rank's `NAVAL_RANK_*` or `AIR_RANK_*` commands as many ships or wings as it has
(section 4).

**No trait comes into the bonus at sea or in the air** - no offence, defence or ground, and no
`leader_defence`. **The traits that do come in**, each down the chain of command as on land:

| trait | where |
| --- | --- |
| `experience_bonus` | experience, x (1 + it), at sea, in the air and on both sides of a bombing |
| `out_of_supply_modifier` | lack of supplies, x (1 + it), the same |
| `night_attack` | added to the night's figure, the same |
| `spotting_chance` | x (1 + it) on each pair's figure when a fleet looks for another |
| `spread_out` | taken off the 0.3 of a ship's spread, to 0.1 at least |
| `submarine_attack` | x (1 + it) on `sub_attack` at a submarine |
| `disengage_timer` | the count a fleet leaves a battle at, 5 x (1 + it), cut to a whole number |
| `defender_softness` | x (1 + it) on the softness a bomber fires at, before it is held to 1 |
| `tactical_attack` | x (1 + it) on a bomber's attack at land units |
| `naval_attack` | x (1 + it) on a bomber's `sea_attack` at ships |
| `strategic_attack` | x (1 + it) on a wing's `strategic_attack` when it bombs a province |

**`dissent_impact` does nothing at sea or in the air**: dissent there has no trait, where on land
it has.

**Two technology figures went in beside the admiral**, because they sit in the same lines:
`targeting_chance` scales a ship's positioning when it looks for a target, and
`targeting_choice` is added to its targeting (`navalcombat.md`, section 4).

## 11. What a battle gives

`reversing/findings/FINDINGS-leaders.md`, sections 1 and 2, which had most of it, and
`reversing/findings/FINDINGS-battlegains.md`, which reads the rest and checks it. Built
2026-10-11: `CCombatExperience`, `CTraitGain` and `CCombatCondition` in `Sim/Combat`,
`CLeaders.AddExperience`, `CGainableTraits` and `CTraitProgress` in `Sim/Leaders`,
`CGainableTraitReader` in `Content/Definitions`.

### Experience

`confirmed`. **A battle is the only thing that gives a leader experience.** Every hour, in its
own hour (`movement.md`, section 1) after its order and before it marches, a unit that is in a
battle - or, for a fleet or an air unit, in a bombing - gains:

    enemy  = for each of its battles, and its bombing: the other side's units' average
             organisation, each unit's weighted by its brigades, ships or wings, times how many
             of them there are; nothing where the unit's country is on neither side
    size   = enemy x 0.01, / 4, held between nothing and 6;   nothing -> no gain, and no roll
    figure = size / 3 + a die of 1 to 6
             x EXP_GAIN_LAND, EXP_GAIN_NAVAL or EXP_GAIN_AIR, by its kind
             x (1 + the leaders' xp_gain, down the chain)
             x EXP_GAIN_CONVOY_MODIFIER, in a raid on a convoy
             x 0.01
    each brigade, ship or wing:  figure x EXP_GAIN_DIV, to a hundred at most - but a ship only
                                 while it has a target
    its leader:                  figure x EXP_GAIN_LEADER
    each headquarters above it, while it is of the same kind - land, or not: the share before,
             divided by its number of units, held between 2 and 5, to its leader if it has one

All of it in the original's 15 binary places, and the 0.01 is 328 of them - a hair over.

- **The die does most of it**: the enemy's size comes to 2 at most against a die of 1 to 6. In
  the base game a leader of a division gains about one point of experience an hour before his
  factor below; one five below his best learns six times as fast.
- **A raid on a convoy** gives its raider experience before each of its first three rounds.
- **Nothing else does**: no exercise, no day's pass, no spending.

**Into skill**, for each gain:

    if skill is 12 or more: nothing
    factor = 1 + (most skill - skill)^2 / 5;   a third of it where skill is at the most or past it
    experience += factor x gain
    above the floor of the next skill (section 2): one skill more - one, however far past

So **the most skill is no cap**: a leader goes on learning past it, at a third of the pace.

### Traits

`confirmed`, but for the end of a raid. `common/gainable_traits.txt` gives each trait a battle
earns: its kind of battle, the hours it wants and a condition. **Each kind of leader may earn
only what the trait's `allowed_leader` allows** - which a history's or a script's trait does
not ask (section 3).

- **Every hour each side banks the hour for each of its units' leaders**, at the head of its
  fire - once in an hour of the day, however many battles ask.
- **The hours are counted** when a unit leaves the side - for every leader on it - and at the
  battle's end, the attacker's first: for each trait of that kind of battle, in the order of
  the file, that the leader has not and has not finished, where its condition holds for his
  side and him then, `100 x hours / hours_required` per cent more, a trait wanting no hours
  whole. One made whole is his. The hours are spent whether anything was earned or not.
- **A new leader on a unit throws away the hours he had banked.**

**The conditions** are the tactics' (`landcombat.md`, section 11) and six more, all of the
battle and the side being counted:

| | holds |
| --- | --- |
| `terrain` | the battle's province is of that terrain |
| `province_temperature` | its temperature is under that many degrees |
| `has_combined_arms` | in a land battle, a unit of the side led by this leader is made for combined arms. **What follows the key is not read**: `= no` holds the same |
| `has_combat_modifier` | the side had that modifier in its last hour - written exactly, `BM_FORT_MODIFIER` |
| `is_convoy` | the battle is a raid on a convoy, or is not |
| `is_winner` | the battle is over and the side has not lost it - a battle broken off is won by both |

*Ours:* `is_winner` asks the side what the battle was decided as; the original asks it again
whether it has lost. A side that has lost has always been decided so here.

*Ours:* `has_combat_modifier` asks the lists of what the hour put on the side's units, where the
original keeps a flag for each on the side; they are made in the same places.

## 12. The window a leader is chosen in

The original's `leader_selection_panel`, from `unitpanel.gui`
(`reversing/findings/FINDINGS-leaderwindow.md`). `confirmed` except where it says. The
window as built was reviewed in play on 2026-10-11: "Everything looked good with the
leaders."

**Opening it.** The unit panel's `leader_button` (`unitpanel.md`, section 3) and its portrait
both open it: whatever window was up for the unit goes and **a new one is always made** - a
second click opens it afresh rather than closing it. The panel writes it each frame and ends it
the frame after it is hidden; a panel made again ends it too.

**Its head** is the leader the unit had as the window opened, **kept for the window's life**: his
portrait, name, skill as a number, rank on the strip and an icon for each trait (`trait_entry`,
its `trait_icon` the sprite `icon_` and the trait's key). With none it is the original's empty
leader - the name `NO_LEADER`, skill 0, rank 0 - and the two promotion buttons are greyed. The
original also sets the strip to the rank the unit asks for, and its first frame's update writes
the empty leader's 0 over it before it is ever drawn. *Ours:* rank 0 draws no strip; what the
original draws for frame 0 was not read.

**Its list**, a row for each leader (`leader_entry_land`, `_naval` or `_air` by his kind):

1. **the unit's owner's leaders in service**, in the order they were put in service;
2. of the unit's kind, with a rank, and commanding nothing or this unit;
3. that the trait filter passes, below;
4. **sorted** by one of four keys - **the name ascending**, by its bytes and the shorter first;
   **skill, the number of traits or rank descending** - with the original runtime's own sort,
   which is not stable: **leaders of equal skill come out in the order its partition leaves
   them**, so that is reproduced step for step (`CStdSort`, checked against the original's code
   run under an emulator on 1,360 lists);
5. turned round whole where the key was clicked twice: the key already chosen turns the order
   round, another starts it the right way. **The key outlives the window and the game**: every
   run starts by name, ascending.

A row shows his portrait, name, skill, rank and traits, and **its button may be pressed only
where the unit may take him** (section 4, the first test) - a general of a corps' rank is listed
for a division and cannot be chosen. Its promotion buttons are always live.

**The trait filter** is a flag for each trait and one for "no trait": a leader with no traits is
listed by that flag, one with traits where any of his is on, and a trait never asked about counts
as off. Its window (`leader_trait_filters`) has a row for "no trait" (`NO_TRAIT_FILTER`) and then
each trait meant for the kind of leader, in the file's order. A click ticks or clears a row; the
other mouse button shows that trait alone - or, where it is already the only one on, everything
again; the filter button opens and closes the window, and its other mouse button turns everything
on. The game keeps the traits that are on, for each kind of leader, from one window to the next;
**where it kept none - the first time, or every trait was off - everything is on**. Which mouse
events the original's "other" ones are was not read: they were taken for the right button from
where they sit among the events, and **it was confirmed on 2026-10-11 that this is
correct** - a right click on the filter button turns every trait on, and on a row shows that
trait alone.

**What the buttons do**, each a command, so that a game shared between players does the same:

| button | posts | then |
| --- | --- | --- |
| a row's | `CAssignLeaderCommand(unit, leader)` | hides |
| `noleader_button` | the same with nobody | hides |
| `removeall_button` | the dialog `DefaultConfirm`, `CONFIRM_REMOVEAll_TITLE` and `CONFIRM_REMOVEALL_DESC`; agreed, `CRemoveAllLeadersCommand` for the **player's** country | hides at once, whatever is answered |
| the head's `promote`, `demote` | `CPromoteLeaderCommand` for the head's leader, a rank up or down | stays up |
| a row's `promote`, `demote` | the same for the row's leader | stays up |
| `auto_assign` | `CToggleAutoAssignLeadersCommand` for the **player's** country, whose setting it shows | |
| `back_button` | nothing | hides |

**The commands:**

- **giving a leader** is refused where the unit is gone, or where he is named and may not take
  it, commanding nothing else; nobody is always allowed. He is taken off whatever he commanded,
  and then put on with his banked hours thrown away and the unit's brigades left
  `NEW_LEADER_ORG_HIT` of their organisation - section 5. Nothing asks whose unit it is;
- **promotion** moves the rank one way and keeps it between 1 and 4. It costs nothing - there
  is no loss of skill (`reversing/findings/FINDINGS-leaders.md`) - and **a general who commands a
  unit is taken off it**, whichever way; an admiral or an air marshal keeps his;
- **removing all** leaves every unit of the country with nobody, at no cost of organisation; the
  original passes over a unit lent to another country, and none is lent here;
- **the checkbox** turns the country's `auto_assign_leaders` the other way (section 5).

In a battle's window each side shows its highest ranked leader's portrait and name, and the
side whose leader has more skill has the arrow of initiative (`combatview.md`, section 4).

## 13. The window's tooltips

Read whole on 2026-10-12 (`reversing/findings/FINDINGS-leadertooltips.md`); built in
`CLeaderWindowTooltips`, tried with `--click`, and they have not been reviewed in play.
`confirmed` except where it says. How a tooltip is shown is `tooltips.md`.

**The order it is asked in.** The trait filter, where it is up and the element is in it - and its
answer stands, empty or not; then the row the element is in, whose answer stands where it says
anything; then the window, by the element's name; **and what the window leaves unsaid falls to the
header of the unit's panel** (`unitpanel.md`, section 10), by name. So:

| element | what it says |
| --- | --- |
| the head's `selected_photo`, a row's `photo` | that leader's own tooltip (`unitpanel.md`, section 10.5); the head's empty leader's is `INACTIVE`, `NO_LEADER`, skill 0 at its most and no experience |
| `gaintrait_info`, at the head and in a row | where the leader is part of the way to a trait, `TO_GAIN_TRAITS_DESC`, a line break and the lines of progress; otherwise `NO_TRAIT_TO_GAIN_DESC` |
| `trait_icon`, at the head and in a row | the trait's name, and after a moment what it does, below |
| a filter row's checkbox `background` | what its trait does - nothing for the row of no trait - and after a moment `TRAIT_FILTER_ENTRY_DESC` |
| `filter_traits` | `TRAIT_FILTER_DESC` |
| a row's `leader_button` | **the header's leader button's**: `UW_LEADER_BUTTON_IRO`, after a moment `UW_LEADER_BUTTON_DRO` - "Assign new leader." |

Everything else says nothing. A row's name and skill lie over its button, and the original's
topmost element answers - with nothing, so the button speaks only where the mouse is off them; a
filter row's name is `allwaystransparent` in the file, and its checkbox speaks under it.

**What a trait does** is a line for each of its effects, in its file's order, with a line break
between and none after:

    the label, by the kind:  TRAIT_ + the effect's key in capitals - TRAIT_XP_GAIN,
                             TRAIT_SUPPLY_CONSUMPTION and so on - but TRAIT_TERRAIN_SPEED and
                             TRAIT_TERRAIN_ATTACK with TYPE the terrain's text, and none at all
                             for paradrop_mission and terrain_defence
    " §G" where the value helps, else " §R" - more is worse for supply_consumption,
          winter_attrition, encirclement_bonus and envelopment_bonus
    "+" where the value is above nothing
    the disengage timer as its figure, 0 places; the rest the figure times 100, 0 places,
          cut and not rounded, + "%§W"

**How many a leader may command** is said only where he commands a unit and it is a fleet or an air
unit, measured against that unit, and ships or planes by it - so a leader commanding nothing, as
most rows' do, does not say it. That corrected the unit panel's reading, which had it by the
leader's kind.

**The original shows them.** The routine is asked only by the unit panel, for an element inside
the panel's window, and the bytes read do not show how the leader window comes to be inside it -
but a screenshot of the running game, 2026-10-12, has a trait's name, `Engineer`,
in the box over a row's trait icon.

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
11. **A unit whose own leader has not the rank, or no skill, gets none of the leader's bonus**
    (section 9) - and so none of the offence, defence and ground traits its commanders' chain
    would bring it either; the other traits reach it all the same.
12. **`fort_defence` and `surprise_chance` do nothing in a land battle**: the first is reached
    only for an attacker, the second is read and thrown away. An enhanced game gives each a meaning of ours
    (2026-11-26, `docs/enhanced.md`): `trait_fort_defence` puts the first on a defender behind a whole level of
    fort, and `trait_surprise_chance` makes the second a chance at the counter-tactic (`landcombat.md`, section 11).
13. **A tactic's `trait` condition asks only the side's leader's own traits**, where every other
    use of a trait takes the chain's.
14. **An admiral too junior for his fleet still draws it up, picks its targets and finds ships
    for its bombers** (section 10): only the bonus asks his rank.
15. **`dissent_impact` does nothing at sea or in the air** (section 10), where on land it makes
    dissent more or less. An enhanced game's fix `trait_dissent_impact` reads it there too, 2026-11-26.
16. **A raid on a convoy never counts the hours its raider's leader banked in it** (section
    11): the original takes the raid's battle off by hand and never ends it, so the hours wait
    for his next battle and are counted against that. So **`seawolf`, which wants a convoy
    battle, cannot be earned by raiding** - `inferred`, since the battle's own end was not
    read to the last call. An enhanced game's fix `raid_trait_hours` counts them as the raid ends, 2026-11-26.
17. **`has_combined_arms` reads nothing after it**: `has_combined_arms = no` holds where a
    unit has combined arms, as `= yes` does.
18. **`is_winner` holds for both sides of a battle broken off**: nobody lost it.
19. **A ship gains no experience in an hour it has no target**, and a bombing of a province
    with no units in it gives the bombers none - there is no enemy to measure.
20. **The portrait opens the window where the leader button cannot be pressed** (section 12):
    the original registers both with one handler and disables only the button - so a unit in
    a battle or falling back can have its leader changed by a click on his picture.
    `inferred` that the picture takes the click as a button does.
21. **Nothing asks whose unit it is** (section 12): a neutral's unit, or a friend's, can have
    its leader changed from its panel, out of its own country's leaders; only one standing where
    an enemy of the player holds the ground is refused.
22. **The original refuses the button to an expeditionary force and undoes it in the same pass.**
23. **Promoting or demoting a general takes him off his unit** (section 12), and the window's
    head keeps him - so its buttons go on acting on a leader the unit no longer has.
24. **A filter with every trait off is forgotten** (section 12): the next window has everything
    on.
25. **"Remove all" closes the window before the question is answered**, whatever the answer.
26. **Paradrop mission and terrain defence have no label in a trait's tooltip** (section 13):
    their lines are the colour and the figure alone.
27. **An effect of nothing is red**, and more encirclement and envelopment bonus is red, as more
    supply consumption and winter attrition are.
28. **A row's button says what the header's leader button says** - "Assign new leader." -
    having nothing of its own.

## Not built

- `winter_attrition` waits for attrition. The practical knowledge a battle gives.
- **The aggressiveness a side's leader picks a tactic with**: it comes from plans and a unit's
  own setting, neither of which exists here, so it is 0.
- **A unit aboard a ship or an aircraft**, which the leader button refuses: nothing carries yet.
- **The AI's own reshuffle of its leaders**, and the headquarters it makes; leaders made,
  killed or changed by events.

## Not established

- Which "first" `rank` the day's putting in service means where a history is not written in
  date order.
- Why Germany's stockpile of supplies two days in is 0.030 above the save's, where it was 3.264
  before section 8.
