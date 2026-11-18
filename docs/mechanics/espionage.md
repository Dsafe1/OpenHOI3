# Espionage

What one country's spies do in another: how the espionage share of leadership trains them, how they are placed by
priority, the nine missions they are given and what each does, how they are caught, covert-ops points with the coup
and the province operations of `common/covert_ops.txt`, and the messages and script keywords on them. Read out of the
original in one wave (`reversing/findings/FINDINGS-espionage.md`, sections A to E) on an earlier reading of the same
file, with `reversing/findings/FINDINGS-government.md` B.6 for the coup and `reversing/findings/FINDINGS-diplostate.md`
for the threat. Cut by this project in four pieces, as politics was: spies and their day (this file, sections 1 to
14); what a country knows; the intelligence screen; the AI's spying.

Figures are thousandths unless said otherwise: a level of 10000 is ten spies. A **share** is thousandths of the whole.
Every product below is worked in 64 bits and every division truncates toward nought. The defines are the `country`
block's of `common/defines.lua`, with Their Finest Hour's values in brackets.

## 1. What a country holds

- **Its free spies**, the pool (`spies` in a save): what the espionage share has trained and nobody has placed. No
  ceiling.
- **A presence in every country**, itself included, by that country's place in the list of countries - the record of
  what it has there:
  - its **level**, the spies there, 0 to 10000 (`spy_allocation`; `MAX_SPY_LEVEL`, ten spies, is not a define but a
    figure of the original's code);
  - its **priority**, a whole number, 0 to 3 in practice (`spy_priority`);
  - **nine mission priorities**, whole numbers, each a weight, 0 to 3 in practice (`spy_mission`), and from them
    **nine shares**: `weight x 1000 / sum of the nine`, all nought when the sum is; worked out again whenever one
    changes. Weights 1, 1, 1 give 333 each;
  - **the hour of its last mission change** (`spy_date`);
  - **its covert-ops points** (`covert_ops_points`), 0 to `SPY_COVERT_OPS_MAX` (50), which belong to this target alone.
- **The spies it caught this month** (`spiescaught`), put back to nought at the turn of each month. Nothing in the
  game reads it but the screen.
- **Three cursors**, one for each priority band from 1 to 3, all starting at the list's first place (section 4).
  Never saved.

The nine missions, in the original's order, with the localisation key of each (`SPYMISSION_<name>_TITLE`):

| # | mission | |
| --- | --- | --- |
| 0 | `COUNTER_ESPIONAGE` | catching spies (section 6) |
| 1 | `MILITARY` | intelligence on the target's units (section 7) |
| 2 | `TECH` | stealing technology (section 8) |
| 3 | `BOOST_RULING_PARTY` | the target's ruling party's organisation (section 9) |
| 4 | `BOOST_OUR_PARTY` | the organisation of the spying country's party in the target (section 9) |
| 5 | `LOWER_NATIONAL_UNITY` | a modifier on the target (section 10) |
| 6 | `INCREASE_THREAT` | everybody's threat from the target (section 9) |
| 7 | `RAISE_NATIONAL_UNITY` | a modifier on the target (section 10) |
| 8 | `COVERT_OPS` | covert-ops points (section 11) |

## 2. A new game

1. **As the list of countries is made**, every country's own presence - its record about itself - is given
   priority 1 and counter-espionage priority 1. Every other presence starts at nought throughout. No history file
   sets anything about spies.
2. **As the relations nobody gave are given** (`diplomacy.md`, section 2), in the same loop: for each country A
   owning a province but REB, towards each country B in order that owns a province and is not REB, with `value` the
   whole number `AI_GenerateNonHistoricalRelation(A, B)` answered - whether or not the relation it would set was
   already set by the history:

       score = [value < -100] + [value < 0]
             + [B is A's neighbour]                       (CCountryLists.Neighbours)
             + [A's and B's acting capitals on one continent]
             + 2 x [both are in a faction, and not one]
             + [their ruling parties differ]
             + 2 x [they are at war] - 2 x [they are allied]
             + 3 x [B is A]
       score = score / 2, truncated, where A is not a major
       A's level in B = clamp(score x 1000, 0, 10000)

   A major is what the AI's offers already call one (`agreements.md`): `major = yes` in its file, or an IC above 100.
   So a minor's own presence starts at 2 spies and a major's at 4, and the presences of a country owning no province,
   and of everybody in it, at nought.
3. **The two days a new game runs** (`tick.md`, section 6) place spies like any other (section 4): in the start save
   the seven majors and China, whose gain reaches half a spy a day, have one more spy at home than the score gives.

## 3. The two commands

Both name an actor and a target; the target is the actor itself for the home presence. **Neither checks anything**
beyond both being countries, and both run on every machine alike.

- **A presence's priority** (`spy_priority`): the actor's presence in the target takes the priority, whatever it is.
  It counts from the actor's next day.
- **A mission's priority** (`spy_mission`): where the mission is 0 to 8 its weight is set and the nine shares worked
  out again; then, **whatever the mission and whether or not anything changed**, the presence's last mission change is
  set to the hour it is.

**There is no rule on which mission may go where.** The original has one - counter-espionage and boost ruling party
anywhere, raise national unity only at home, the six others only abroad - and only its Lua may ask it; nothing in the
game and neither script calls it, so a mission sent anywhere is stored and runs. `SPY_MISSION_CHANGE_DAYS` (7) is read
by nothing: there is no wait between changes.

## 4. The day: spies trained and placed

Once a day, for every country but REB, in the order of the list, as its espionage share of leadership is run
(`leadership.md`):

    max   = MAX_NUMBER_OF_SPIES / 1000, truncated - a count (10)
    pool += the share's leadership, in thousandths, x LEADERSHIP_TO_SPIES / 1000   (0.15; already built)

    band(p) = clamp(p.priority - [p.level >= trunc(max x 0.7 + 0.01) x 1000], 0, 3)   (7000 with max 10)

    count, for every presence p that is under max x 1000:   bucket[band(p)] += 1

    for band 3, then 2, then 1:
        if pool < 1000 or bucket[band] = 0: next band
        each = max(1, (pool / 1000) / bucket[band])          whole spies
        placed = false
        for every place i from cursor[band] to the list's end:
            skip a country that owns no province, a presence whose band is not this one, and one at max x 1000
            stop when pool < 1000
            give = min(max - level / 1000, each);  skip when it is not above nought
            cursor[band] = i + 1;  placed = true
            level = clamp(level + give x 1000, 0, 10000);  pool -= give x 1000
        if not placed, or cursor[band] has reached the list's end: cursor[band] = the list's first place

So **a priority-0 presence never gains a spy** from the share, **a priority-1 presence stops at 7** - at 7 it drops to
band 0 - and priorities 2 and 3 go on to 10. **A presence never loses a spy by itself**: only a catch (section 6), a
coup (section 11), an annexation (every country's presence in the annexed goes to nought) and `modify_spies`
(section 13) lower a level.

The original's list of countries has a placeholder, `---`, before REB, which these loops never look at and this
project's list does not have: "the list's first place" is REB's, which owns no province and so is never given a spy.

## 5. The day: the missions

Once a day, for every country in the order of the list **that owns a province or is a government in exile** - one
annexed keeps its spies abroad and they do nothing - late in its day, after the alignment's step and before its
election (`reversing/findings/FINDINGS-politics.md`, the rest of the pass). **A country's day is run whole before the
next's** (`reversing/findings/FINDINGS-liveread.md`, sections 120 and 121, a recording of July 1941): its spies are
placed (section 4), its mission looked at, its spies set to work, its undergrounds and its roll for a rising run, and
only then the next country's - so a spy one country places today is there for the catches of the countries after it
and not of those before, and each draw falls where the original's does. Just before its spies, every country that owns
a province and is not REB makes its mission's state afresh, there being none: two draws of the game's generator, the
scopes' seeds (`CCountryMission`; missions are not built, and no file of Their Finest Hour's or BlackICE's has one):

    for each country T in order, itself included, that owns a province:
        p = this country's presence in T
        if p.level > 0: run p's missions, the actor this country and the target T
    if this country's own presence has a counter-espionage weight above nought:
        catch spies (section 6) on its own presence, the actor and the target both this country

and a presence's missions are tested in this order, each run only where its weight is above nought:

1. counter-espionage (0): catch spies (section 6);
2. technology (2): section 8;
3. military (1): section 7, **only on a day of the month d (0 for the first) where d + T's number is a multiple of
   eight** - T's number in the original's list, which counts its placeholder, so REB is 1 and Germany 2 - three or four
   days a month, the pattern starting again each month;
4. boost ruling party (3), boost our party (4), increase threat (6): section 9;
5. covert ops (8): section 11.

Lower and raise national unity (5 and 7) are not run here: they are modifiers (section 10). **A country's own
presence runs every mission it has on the country itself**, so counter-espionage at home is rolled twice a day.

## 6. Counter-espionage: catching spies

With the presence p, the actor A whose it is and the target T it is in:

    base = SPY_DETECTION_CHANCE (0.1)  + A's counter_espionage where A is T
    c = p.level x base / 1000 x share[0] / 1000
    where A is T:  c = c x (1000 - 800 x (1000 - share[0]) / 1000) / 1000

so a country with every home spy on counter-espionage keeps the whole chance and one with none there a fifth. Then
for every country O in order but A that owns a province, with q = O's presence in T:

    skip where q.level < 1000                                    under one whole spy
    chance = c
    where O is A's friend (with military access counting):        IsFriend, below
        skip unless A is T;  skip unless q has a hostile mission;  chance += 1000000
    chance = chance x (1000 - T's espionage_bonus) / 1000
    where none of q's nine weights is above nought:  chance = chance x NO_MISSION_SPY_DETECTION_MULTIPLIER (0.1) / 1000
    caught when a draw % 1000 < chance, and then a second draw % 100 < 10
    caught: q.level = max(q.level - 1000, 0);  where A is T, A's spies caught this month + 1

- **A friend** is the original's `IsFriend`, in its order: itself; either the other's overlord, or one overlord over
  both; one real faction; an alliance in the other's record towards it; fighting on one side and not its enemy; and,
  with military access counting, its own record's access through the other. Moved into the simulation from the Lua
  class that had it.
- **A hostile mission** is any of military, technology, boost our party, lower national unity, increase threat and
  covert ops with a weight above nought, or counter-espionage where q's target is not q's owner's friend (without
  access). So a friend's counter-espionage, boost ruling party and raise national unity at home are left alone, and a
  friend's hostile spies at home are caught on the second draw alone.
- **`espionage_bonus` is the target's, and it lowers the chance**: at home a country's own bonus makes it worse at
  catching.
- **The two are read nowhere else**, and nothing swaps them: `counter_espionage` is the catcher's own and helps at
  home only; `espionage_bonus` is the country the spies are in, and only hinders catching there. Both are written
  green when positive, which for `espionage_bonus` reads as the opposite of what it does. Settled 2026-10-14
  (`reversing/findings/FINDINGS-occupation.md`, section F) on the maintainer's account from play: it works "more as a
  'Domestic Counterespionage Difficulty' modifier", with no effect abroad and an inverted tooltip - which is
  `espionage_bonus`, BlackICE's name for it. A model of the start save's two days from the save's own spies expects
  24.9 catches (sd 5.0) as read, where the save has 33 and this project's new game 22.
- **The draws are the game's own generator**, as every roll here is (`landcombat.md`, section 8).
- **O may be the target itself**: a country's spies at home are caught by a foreign counter-espionage like anyone's.

Who is told is section 12's.

## 7. Military intelligence

    T = SPY_MILITARY_INTEL_TIME_BASE / 1000, truncated   (32)
    base = MILITARY_ESPIONAGE_DETECTION_CHANCE (0.6) x 10000 / 1000 x level / 1000 x share[1] / 1000

For every unit of the target's, in the order its units are held:

- a land unit: `c = (1000 + the actor's offmap_land_intel) x base / 1000`;
- an air unit: `c = MILITARY_ESPIONAGE_AT_BASE_CHANCE (2) x base / 1000` where it is at its own base, else `base`;
- a fleet: `c = (1000 + offmap_naval_intel) x base / 1000`, and again times the at-base figure where it is at its own
  base;

then `p = c / 1000` **rounded to the nearest** (a float made whole the way the processor does, halves to even), and
the unit is known when a draw % 1000 < p. Then

    v = min(draw % 1000 x (2000 x share[1] / 1000) / 1000, 1000)
    level = v x 7000 / 1000 / 1000 + 2                              2 to 9

and the unit is given **an intelligence grant**: the actor knows it at that level for T of the unit's days. A unit
holds one grant for each country; a new one **replaces** the old, so a 2 can replace a 9. Every unit's grants count
down by one a day and go at nought. What a country knows reads them (`intelligence.md`, sections 3 and 5).

"At its own base": an air unit or a fleet standing in a province that is its base and holds the building it needs
(`fleetsandair.md`).

## 8. Stealing technology

1. **The first roll**: `percent = level x TECH_ESPIONAGE_BASE_CHANCE (2) / 1000 x share[2] / 1000 / 1000`; nothing
   happens when a draw % 100 > percent. So the chance is (percent + 1) in 100: one spy entirely on it gives 3 in 100.
2. **The weights.** For each technology in the list of technologies - which in the original begins with a null one
   that weighs nothing - the weight is nought unless it is `stealable` (no file of Their Finest Hour's says otherwise)
   and the target's level in it is above the actor's; then `w = (10000 / gap) / max(difficulty, 1)`, both
   truncated, the gap in whole levels.
3. **The second roll**: with n the length of that list, the null one counted,

       v = sum of w x 1000 / (n x 10000)
       chance = v x TECH_ESPIONAGE_TECH_PICK_FACTOR (0.3) / 1000 x share[2] / 1000

   and nothing happens unless a draw % 1000 < chance. A large tree makes a steal rarer.
4. **The pick**: a draw % the sum of the weights; the first technology whose running total exceeds it.
5. **The theft**: the actor's progress in it is set to nought, **its level becomes the target's, however many levels
   that is**, the levels' effects apply (the technology's modifier is made again, as a level researched makes it) and
   the technology leaves the actor's research queue. No maximum level, `allow` or prerequisite is asked, and the
   category's ability is not raised.
6. **The messages** (section 12).

## 9. The two party boosts, and threat

- **Boost ruling party** (3): `x = level x SPY_PARTY_ORGANIZATION_BOOST (0.01) / 1000 x share[3] / 1000`, the party
  the **target's** ruling party; **boost our party** (4): the same with `share[4]` and the **actor's** ruling party.
  Both write the **target's** figures: `v = organisation + x`; where v is below nought nothing is written, else the
  organisation becomes v and v - the new figure, not the change - is added to the spies' tally (`politics.md`, section
  1). No cap: the month's drift brings the parties back to a hundred.
- **Increase threat** (6): `amount = level x SPY_THREAT_INCREASE_ACTION (0.07) / 1000 x share[6] / 1000 / 10`, spread
  to everybody with a scale of 1000 and **the target as the feared** (`diplomacy.md`, section 5): every other country's
  threat from the target rises. A spy on it at home raises everybody's threat from the country itself.

## 10. Lower and raise national unity

Each time a country's modifier is made again, after `war` or `peace` (`modifiers.md`, section 4, step 9): for each
country O in order that owns a province, with p = O's presence in this country and p.level above nought, where p has
a weight on mission 5 the static modifier `spy_lower_national_unity` (`national_unity = -0.1`), and where mission 7
`spy_raise_national_unity` (`+0.15`), each scaled first by the level and then by the mission's share - so
`-0.1 x spies x share`. Their Finest Hour's figures are per month (`politics.md`, section 6), and all `national_unity`
together is held at -1.0 a month: ten spies entirely on lowering reach it alone.

## 11. Covert ops, the coup and covert operations

**The points.** Covert ops (8): `x = level x SPY_COVERT_OPS_GAIN (0.5) / 1000 x share[8] / 1000`, then `x x 1000 /
30000` - the define is a month's - added to the presence's points and held at `SPY_COVERT_OPS_MAX` (50). One spy
entirely on it earns 0.016 a day, ten 0.166.

**The coup** is a command, `perform_coup`, naming its target (`from`) and its instigator (`to`) - the names the
other way round from what they suggest - which only the intelligence screen posts. It re-checks nothing. Its
execution: the instigator's presence in the target goes to nought spies and loses `COUP_COVERT_OPS_COST` (20) points,
held at nought; then

    seats = the target's cabinet seats held by parties of the instigator's ruling party's group
    chance = seats x 1000 x COUP_CHANCE_PER_POSITION (0.2) / 1000 x (1000 - unity x 1000 / 100000) / 1000
    the target leads a real faction: 0;  any other member of one: chance / 3

and it succeeds when a draw % 1000 < chance: `on_coup_success` fires for the target with FROM the instigator, else
`on_coup_fail`; the coup itself is whatever that event does (Their Finest Hour's 5057 says `coup_by = FROM`). **Who
may press it** is the screen's to ask (piece 3): one whole spy there, at least 20 points, and the two ruling groups
differing, the target's government fractured and the instigator's group holding a seat in it.

**Covert operations in a province** are `common/covert_ops.txt`'s: each a name, a `cost` in whole points, an
`is_valid` and an `is_selectable` condition and an `effect`. A command, `send_covert_ops_mission`, names the actor, the
province and the operation; it takes `cost x 1000` off the actor's points **about the province's controller - with no
floor and no test that they are there** - and, where the effect has anything in it, runs it with the province as the
scope and FROM the actor. No message. Whether one may be chosen is the province window's to ask (not built): the
province is a real one, the actor's whole points about its controller reach the cost, and `is_selectable` holds where
it says anything. Who asks `is_valid` was not found.

## 12. Messages

Only the player is told, and none of these is told to anybody else.

| type | when | told | variables |
| --- | --- | --- | --- |
| `WE_STEAL_TECH_ONCE` | a steal, the actor the player, a technology with no levels after its first | the actor | `LEVEL` the new level, `TYPE` the technology's name, `TARGET_ADJ` the target's adjective, `EFFECT` what the levels do (`technologyscreen.md`, section 9) at the new level, its first `§W` made `§b`, or `none` |
| `WE_STEAL_TECH_REPEAT` | the same, a technology with levels | the actor | the same |
| `TECH_STOLEN` | a steal, the target the player and not the actor, with more than five spies on counter-espionage at home (`level x share[0] / 1000 > 5000`) | the target | `LEVEL`, `TYPE`, `RESPONSIBLE` the actor's name |
| `TECH_STOLEN_BY_UNKNOWN` | the same, more than two and not more than five | the target | `LEVEL`, `TYPE` |
| `SPIESFOUNDUS` | a catch, the catcher the player | the catcher | `WHERE` the caught spies' country |
| `SPIESFOUNDTHEM` | a catch, the caught spies' country the player, not the catcher | it | `WHERE` the target |

The first four are logged and popped up in Their Finest Hour; the two last are neither, by its `messagetypes.txt`.
`SPIESFOUNDUS` says the spy was caught "in $COUNTRY$" - the player's own country - even when it was caught abroad.

## 13. Script

- **`spies = N`** (condition): the scope country's level in THIS's country - its own presence when THIS is the scope
  itself or names no country - at least N.
- **`spy_mission_count = { type = X value = N }`** (condition): on the same presence, `level x share[X] / 1000` at
  least N; a type nothing knows is counter-espionage.
- **`has_hostile_spy_mission`** (condition) compares in the original against a figure nothing ever sets; it stays
  reported as unread and answers false.
- **`modify_spies`** (effect): in the original it adds to the scope country's presence in the target a count it never
  reads from the file, whatever its memory held; here it adds nothing, and the reading reports it.
- **`local_intel_boost`** (effect) gives intelligence grants (section 7): `intelligence.md`, section 6.
- **`on_coup_success` and `on_coup_fail`** fire from the coup (section 11).

## 14. Against the saves

`OpenHOI3.SaveCheck espionage <install> <start-date save> [later save of that game]` makes a new game with the
install's Lua, runs its two days, and sets beside the start save, country by country and presence by presence: the
free spies, each level, each priority, each mission weight and the covert-ops points. Given a later save it then plays
on to that save's date with the save's player played and sets that country's free spies and presences beside the
later save's - the one country whose espionage share and priorities no AI moves, the AI's spying being piece 4.

What the reading predicts and the saves showed by hand (`FINDINGS-espionage.md` A.5): Germany's pool grows by its
share of leadership times 0.15 a day less the whole spies placed - 1.226 on 1 January, 2.565 on 4 January at 12:00
with its home at 7 - and a priority-1 home at 7 gets nothing more.

**On 2026-10-14** against the start save: **every home level 101 of 101** - 2 for a minor that holds land, 4 and the
second day's spy for the seven majors and China, nought for the 23 that hold none - **every priority, mission weight
and covert-ops point 10201 of 10201**, and the levels abroad 10051 of 10100. The 49 abroad that differ are each one
spy, and are catches: counter-espionage at home rolls on the two days, and the original's catches fell on other
presences than ours - Czechoslovakia's spy in Germany, given at the setup, is caught here and not there. The original
caught more, 1783 spies abroad left to our 1798: **the triggered modifier `neutrality`** of Their Finest Hour, for a
country in no faction, gives `counter_espionage` +0.1 and `espionage_bonus` +0.1, which with the base 0.1 is 1.8
times the chance at home for the ninety countries in no faction - our rate expected about 18 catches in the two days
and the save has 33. The free spies were 95 of 101, the six that differed countries whose leadership was not the
save's.

**With triggered modifiers built**, the same day: free spies **100 of 101** - Germany's alone, because the player keeps
the history's `medium_large_education_investment` where the original's AI had given it `big_education_investment`
(`docs/compatibility.md`, entry 6), 1.2 over 1.1 of the leadership, and so 1.024 spies a day to 1.113 - and the levels
abroad 10045 of 10100, **1794 spies abroad to the save's 1783**: 22 catches to its 33, where the reading's formula
gives the neutral countries 1.8 times the chance. The rest is the rolls: a model of the two days from the save's own
spies expects 24.9 catches, sd 5.0, as read, and the save's 33 is 1.6 sd above it (section 6). **On 2026-11-02**, with
every country's laws brought forward as a new game is made, the player's too (`politics.md`, section 13; entry 6
struck): free spies 101 of 101, the levels abroad 10052 of 10100 and 1798 spies abroad. **On 2026-11-18**, the stretch of
the day from the spies to the seats run a country at a time - each country's spies, then its undergrounds, its roll for
a rising, its election and its seats, before the next country's spies - as a recording of the original's draws shows it
(`reversing/findings/FINDINGS-liveread.md`, section 120): the levels abroad 10053 of 10100, the catches falling on
other rolls.

Played on as Germany to the saves of 4 January 12:00 and 8 January 07:00 of the maintainer's other game: **the home
at 7 and every priority and mission weight agree**, and the pool is short by that 0.089 a day and nothing else. The
abroad levels differ where catches did, and by the setup of the other game (A.7). The 20 April save has the player's
espionage share moved, so its pool is not compared.

## 15. The AI's spying

Read in the espionage wave's section E. The AI's spying is one program, the install's Lua
`IntelligenceMinister_Tick` - Their Finest Hour's `script/ai_intelligence_minister.lua` - which sets each country's
priority towards every other and a weight for each mission there, posting the two commands (section 3). Nothing in the
engine decides anything for it.

**The minister.** As a new game is made, after the politics minister's, every country nobody plays is given an
intelligence minister, whose first work is `draw % 10 + 1` hours away on the game's generator. From then on it works
every hour, and does anything only **in its hour of the day, its number in the original's list plus one** - Germany's,
were it the AI's, 03:00. A country that owns no land and is not in exile is passed over, as the politics minister is;
nothing works while a game is being made. Then `IntelligenceMinister_Tick(minister)` runs with the minister handed
over, and a Lua error is reported and ends that country's day.

**What the script does** (the install's own, so a mod's replaces it): one day in nine by `GetAIRand`, it sets its home
presence's missions - counter-espionage 3 and one booster by national unity and the ruling party's popularity - and its
priority 3; for each ally (an alliance or a member of its faction) priority 1 and counter-espionage 2 with a booster;
for everyone else a priority from 0 to 3 by neighbourhood, majority, continent and war, with Germany's own
`Intel_Priority` and `Intel_Mission` for Austria, and at a priority above nought two missions drawn by `math.random` -
military, boost our party or tech at peace, military, tech or lower national unity at war. It never chooses increase
threat or covert ops, nor a coup.

**What it is handed**, besides what the AI's other scripts read already (`lua.md`):

| | |
| --- | --- |
| `CAIEspionageMinister` | `GetCountryTag()`, `GetOwnerAI()` |
| `CSpyPresence` | `GetLevel()` as a fixed point, 0 to 10; `GetPriority()`; `GetMissionPriority(m)`; `GetLastMissionChangeDate()`; `GetPrimaryMission()` - the highest weight, the lower of equals, **9 where none is above nought**; the static `MissionAllowed(m, actor, target)` nothing calls; `MAX_SPY_PRIORITY` 3 and `MAX_SPY_LEVEL` 10 |
| `CCountry` | `GetSpyPresence(tag)`; `GetSpyingOnUs()` - every other country but REB owning a province whose spies here are above nought, a fraction counting; `CalculateIsAllied(tag)` - an alliance or one real faction; `GetCapitalLocation()` - the declared capital, not the acting one; `GetNationalUnity()`; `AccessIdeologyPopularity()`, a `CIdeologyData` whose `GetValue(ideology)` is the party's popularity |
| `CCurrentGameState` | `GetPlayer()`, the player's tag |
| `CEU3Date` | `GetTotalDays()` |
| `CChangeSpyPriority(actor, target, priority)`, `CChangeSpyMission(actor, target, mission, priority)` | **exactly** three and four arguments, else luabind's "No matching overload"; the mission truncated, a priority to the nearest |

**Against the saves.** `OpenHOI3.SaveCheck espionageai <install> [days] [tag] [save]` plays a new game on with the AI's
ministers at work, the trading left out, and sets every AI country's priority towards every country owning a province
beside a save of that day. On 2026-10-14, Germany played to 21 April 1936: every one of the 77 AI countries owning land
had acted, as in the save, with no Lua error, and **5921 of 5929 priorities were the save's** - where the reading's own
model of the rules, neighbours left out, had 5802. Six of the eight that differ are about Ethiopia, at war with Italy.
The missions are drawn by the C runtime's generator, per thread and never seeded in the original, and are not set beside
the save.

## What looks wrong and is built so

The original's, kept, and none put to the maintainer:

- the band cursor never wraps within a day, so a band can sit out a day with spies in the pool, and a country's only
  presence in a band gets a spy every second day at most;
- the bands are counted with no test that a country owns a province, so a presence in a country that does not exist
  thins everyone else's share;
- the pool is charged the whole of `give` even where the ten-spy cap cut it (unreachable while
  `MAX_NUMBER_OF_SPIES` is 10);
- a priority-1 presence stops at 7 spies;
- neither command checks anything, and the mission command stamps its hour whatever it did;
- a country's own presence runs every mission it has, so counter-espionage at home rolls twice a day and threat
  raised at home is everybody's threat from the country itself;
- the target's `espionage_bonus` lowers the chance of catching spies, its own at home included;
- foreign counter-espionage catches the target's own spies at home;
- a technology stolen takes the target's level whole and throws away the actor's progress in it;
- military intelligence is rounded where everything else truncates, air units ignore the offmap modifiers, and a
  later low grant replaces a higher one;
- covert operations may take the points below nought; the coup holds them at nought;
- `SPIESFOUNDUS` names the player's own country as where the spy was caught.

## Ours

- **The missions run after every country's spies are placed**, where the original places one country's spies and
  runs its missions before the next country's day: a later country's spies placed today can be caught today here and
  not there. The day's steps are run country by country across the passes as everywhere in this project (`tick.md`).
- **A spy placed does not make its target's modifier again at once**, where the original does it after each one.
  Nothing in a day reads the unity modifier between the placing and the country's own rebuild but the screen.
- **The list of technologies a steal weighs is the files' order** with a null one at its head; the original's order
  of its database was not read.
- **`modify_spies` adds nothing**, where the original adds whatever its memory held.
- **The intelligence minister's first draw comes after every politics minister's**; the order the original makes a
  country's ministers in was not read. Its hour in the tick is after the politics minister's, not placed by a reading.
- **`GetSpyingOnUs` is worked out when asked**, where the original keeps a list it makes again once a day.
- **A command the AI posts is run at the hour's end here**; in the original's saves its `spy_date` is the minister's
  hour plus one, so it runs the next hour (section E.1 of the reading, read off the saves).

## Not established

- Whether a unit's grants count down once a day: the original counts them in a unit's update the fact base takes to
  be daily.
- Who asks a covert operation's `is_valid`.
- The two `disrupt_production` and `disrupt_research` static modifiers registered beside the spies' four, and
  `spy_lower_neutrality` and `spy_support_resistance`, which no reader was found for.
