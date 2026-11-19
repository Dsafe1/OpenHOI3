# Moving and attacking

Stage 5's seventh piece, and **the military AI's third**: what moves the AI's units and makes them attack. The second
piece (`agents.md`) gave every theatre of a country the AI runs an agent, a ladder of commands and a plan; this one
places its divisions - garrisons, a line on its area of operations, a reserve behind it - sends them into battle where
the odds are good enough and calls them off where they are not, and pulls defenders out of a battle they are losing.

Read out of the original in `reversing/findings/FINDINGS-moving.md` (the moving wave of 2026-10-19, sections A to F), with
`reversing/findings/FINDINGS-aiconsumer.md` (the attack), `reversing/findings/FINDINGS-attackodds.md` and
`reversing/findings/FINDINGS-aihelpers.md` (its odds), `reversing/findings/FINDINGS-opsarea.md` (the area's scores),
`reversing/findings/FINDINGS-power.md` (committing and holding back) and `reversing/findings/FINDINGS-agents.md` (the
frame: the pass, an agent's hour, its lists).

**Mod-able or fixed.** Every count, weight and bar below is **exe-hardcoded** unless said. A mod reaches:
`BASE_STACKING_PENALTY` (the most divisions a position wants, round(0.5 / -value), 5 in Their Finest Hour), the combat
defines the odds read (`COMBAT_LEADER_IMPACT`, `COMBAT_DISSENT_IMPACT`, `COMBAT_SUPPLY_LACK_IMPACT`,
`RIVER_CROSSING_PENALTY`, `AMPHIBIOUS_LANDING_PENALTY`, `BASE_FORT_PENALTY`, `DIG_IN_FACTOR`, `ENCIRCLED_PENALTY`,
`ENVELOPMENT_PENALTY`, `STRAT_REDEP_BASE_SPEED`); the unit files - which types are `garrison_brigade`,
`militia_brigade`, `police_brigade`, `infantry_brigade`, `bergsjaeger_brigade`, `marine_brigade`, `paratrooper_brigade`
and the armour and mechanised types, by key, and every type's attacks, defences, softness, toughness, terrain figures and
speed; the map - forts, infrastructure, victory points, depots, regions; and the strategy's threat, antagonism and
`defend_prov`. Nothing reads Lua.

"An edge", "a real area", "a country's number" and the agent's hours A0 to A9 are as in `agents.md`. **Hostile** is
`agents.md`'s 7.2 test; **friendly land** a province whose controller is of the country's side, or fights beside it and
is not its enemy.

## 1. When (`FINDINGS-moving.md` A.2)

In an agent's hour (`agents.md`, section 3), after its subordinates are sorted:

1. **Every hour**: the provinces threatened from the sea (section 6).
2. **On a full pass, or at war, in an undeclared war or with provinces in revolt every third hour**: the land block -
   positions (section 7), then the fallback (section 8), then the march check (section 9).
3. **Then, on the same gate**: the attack (section 11).

The scores and lists the land block works from (sections 2 to 5) are made again on every **replan** - whenever the area
of operations is set (`agents.md`, 8.1) and whenever the plan's stance changes - and the committing (section 2) whenever
the subordinates are sorted.

## 2. Committed and held back (`ManageSubordinates`, `FINDINGS-power.md`, `FINDINGS-agents.md` C.6)

As the subordinates are sorted, each **division** of the agent's land list that the transport reservation (`IsUnitReservedForTransport`, section 11.2)
does not claim is **committed** or **held back**:

1. The bar: r = our power / their power (`agents.md`, 7.3); **factor** 0.7 above 2, 0.6 above 1.5, 0.2 under 0.25, 0.3
   under 0.5, 0.4 under 0.75, else 0.5.
2. **A theatre agent's division outside its theatre's largest area** is committed and its mark cleared. **With the frontier not at war** a division
   held back stays held back, any other is committed.
3. Otherwise one already held back stays so, whatever its figures - the bar for its coming back, at factor + (1 - factor) / 2 and 0.65, lies behind a test it has already failed and is never reached [exe, `reversing/findings/FINDINGS-liveread.md`, section 16]. And with strength and organisation each the mean of its brigades' shares - **a brigade's strength share is of its most as the game gives it, a reserve's held under its type's by the law's penalty while its country has not mobilised**, so a reserve at its ceiling counts as whole (`reversing/findings/FINDINGS-liveread.md`, section 38): held back where strength < factor
   or organisation < 0.3. The rest are committed.
4. An airborne division (role 8) is held back whenever the test runs, and passed over where the agent has air
   transports.

Then **the reserve**, only with the frontier at war: with c committed and n provinces in the area of operations, where c
> 10 or the agent is below a theatre: surplus = 0, or where c > 1.5 n, c / (10 at stance 4 or 1, 7 at stance 3, 5
otherwise); and where n > c - surplus, surplus = c - n. Otherwise surplus = -2. Then:

- **Recall**: while more than surplus + 2 are held back, the held-back division with the most strength + organisation
  (+1 for airborne), organisation at least 0.33 and strength at least 0.75, is committed again - in single precision, the
  first of equals winning, so divisions near full strength are told apart by what a float keeps of their shares (*confirmed* on the
  running game, 2026-11-11: Smolensk HQ's 23 held back entering July 1941 the original's in order, where thousandths had
  made three full divisions tie and recalled the 18. Tankovaya in place of the 115. Strelkovaya; `FINDINGS-liveread.md`,
  section 49).
- **Detach**: while fewer than surplus are held back, the committed division with the highest score is held back.
  Bound for (or standing in) a province: skipped where - for a theatre agent - it lies outside the theatre's largest
  area and its focus areas, or where a neighbour, by an edge not impassable, on land in a real area, not its country's, is
  held by a country it prepares war on; on a frontier (an enemy neighbour) skipped where a land battle is on there, or where fewer than two land
  units of more than one brigade stand there [exe, the same section]. Score = ((1 - organisation) + (1 - strength)) x 5, +1 with an attack delay running, +0.9 airborne,
  +0.2 already held back; at stance 3 or more -2 armour, -1.5 fast, and its first brigade militia
  +2, mountain troops -2; below stance 3 +2 fast, +1 armour and the first brigade not looked at; on a frontier, less the divisions already held back standing in the province it is bound for this pass, by the test or by this [exe, `reversing/findings/FINDINGS-liveread.md`, section 19].

A held-back division carries **held back**; one the land block makes a garrison (section 7.2), **garrison**. Committed
divisions standing on a friend's land within the capital's block of land are counted, for the allied-soil budget.

**The split, the allied-soil budget and the garrisons count every division that passed through this**, those held back by the test or since: the list the line itself walks is the committed one after (`reversing/findings/FINDINGS-liveread.md`, section 14; `CAgent.CommittedAtFirst`).

## 3. The area's scores (`ScoreOpsAreaProvinces`, `FINDINGS-opsarea.md` section 2)

Each province p of the area of operations, n of them, m committed divisions, in the area's order:

- Its neighbours by edges not impassable, on land: **friendly** ones counted; **front** ones (in the agent's front list)
  counted; a sticky **interesting** where one is held by a war candidate; a sticky **worthy** where one is held by a
  front-worthy enemy that is major or over 100 IC. The distinct holders of interesting or worthy neighbours (not the
  rebels): where their brigades' combat value, averaged, is at most 15% of the country's, the score is scaled by
  max(r / 0.15, 0.05).
- **Frontier not at war**: 10 x fort + 10, +10 where worthy, and +20 for m mod n of the n provinces, spread by a
  carried fraction in the area's order - **the fraction and the carry in single precision**, each step worked in double
  and rounded back, the carry compared with 1 and 1 taken off. Czechoslovakia's 25 divisions over 35 provinces give 25
  of them +20, where thousandths gave 24; with 25 the sum is 2090, above the 23-division line times the best score of 90,
  so the line fill hands no position a share, every position wants one, and the 10. and 6. divize are stopped where
  they stand (`confirmed` on the running game, 2026-11-13, `reversing/findings/FINDINGS-liveread.md`, section 69).
- **At war, stance under 3**: from 10; 10 x friendly - 10 where its area holds more than 5 provinces; + 2 x fort + 20
  unless it has at most one friendly neighbour and three front ones; +100 for its own supply depot.
- **At war, stance 3 or more**: 10, or 15 for a depot; doubled for each province of the hostile routes (section 4) it
  borders.
- Then: for a theatre agent, x3 where the province's front of its area is shorter than a fifth of the area of operations
  (the first of its area's fronts holding it; on none, 0 - so a province on no front is always tripled - and the fronts
  always made, the original making them with the area: `theatres.md`, section 2);
  + the strategy's `defend_prov` for it, / 2 at peace, / 8 at war; +100 for each of its regions marked `ai_prio` in `map/region.txt` (*mod-able*; `reversing/findings/FINDINGS-liveread.md`, section 14); x the scale
  above; and **nought** at war where no neighbour was interesting.

The records go in descending order of score, and the scores' sum is kept: the line is shared out by them (section 7.3).

## 4. The approaches (`RebuildFrontAndApproaches`, `FINDINGS-opsarea.md` section 3)

After the front list (`agents.md`, 8.2):

1. **Nearness to the objectives**: each front province's mean distance to the plan's objectives in its area; where the
   means differ, each becomes (max - mean) x 100 / (max - min) - 100 for the nearest. Read by the attack at stance 3 and 4.
2. **Target areas** - the way out: the country's own areas, not its capital's and bordering land held against them, that
   the area of operations' areas reach within two steps of area but not without crossing an enemy's - a pocket or a
   colony cut off. Every province of each goes on the **target provinces**; from the three provinces of the area of
   operations nearest each, the way there, every province on it the country does not control and is at war with (or the
   rebels') goes on the **hostile routes**.
3. **The second ring**: friendly land two steps out from the area of operations - not in it and not beside it - each step
   in an area of the supply network and held by the country's side or a country fighting beside it that is not its enemy.

## 5. The hold provinces (`RebuildHoldProvinces`, `FINDINGS-agents.md` C.7, `FINDINGS-moving.md` D.4)

Made again where none are kept, where provinces are threatened, or on a full pass on a day of the month that, **counted
from nought, is divisible by 7** - the 1st, 8th, 15th, 22nd and 29th (`confirmed` on the running game, 2026-11-13: East
Africa Command's list made again at 09:00 on 1 January with a fifth division come in, its line left at nothing and its
Tanganyikan Colonial Army where it stood; `reversing/findings/FINDINGS-liveread.md`, section 68):
each objective of the plan held by the country's side, scored by its hold score plus its priority; each priority
province the country controls, scored hold score + 200; for a theatre agent, the provinces of focus areas a puppet of the country holds, each but where one of the puppet's land
units stands or is bound - province by province, not the area as a whole (*confirmed* on the running game, 2026-11-11:
Yemen's 9768 on Britain's East Africa list of July 1941 with Yemen's army in 9750, `FINDINGS-liveread.md`, section 52)
- not for a subject - and its theatre's provinces - for a subject too - scored by their hold score where that is 0.1 or more, rounded a half to the even, and a province in both lists coming twice. In descending order, cut to the agent's
divisions (or the objectives' count, if more) [exe, `reversing/findings/FINDINGS-liveread.md`, section 15].

**The hold score** (`CAIUnit_ScoreHoldProvince`): nought below 200 infrastructure; nought outright for a province without
victory points or a depot beside a war candidate; for France each neighbour Belgium or Luxembourg holds, and for Belgium
each Luxembourg holds - the holder not the country's subject, while Germany holds land, is in another faction than the
country's and is at peace with the holder - **adds 4000 and is counted**, in place of the rest; otherwise, over its
neighbours' holders that are no friend: t = the holder's threat towards the country (+ the country's antagonism of it at
peace) - where t is 100 or more and the holder's surrender progress is under a tenth, t is added and the neighbour
counted (the record's reading, `CAIUnit_ScoreHoldProvince`; on 6 September 1939 it puts Paris before the Belgian border
among France's garrison targets, as both runs of the original send their garrisons); score = their mean / 20 + the count, + what it is worth with **hold** (`agents.md`, 7.6), + 200 where it is
threatened (section 6), + the holder's `defend_prov` for it.

## 6. Threatened from the sea (`FINDINGS-moving.md` A.1)

Every hour: a listed province stays only while it is in the agent's theatre and a sea province beside it holds a hostile
land unit aboard a fleet whose next step is it. Then each enemy fleet handed to the agent (section 12) carrying land
units whose way ends in a province the country owns adds that province. A land unit at sea given a way of one province
into enemy land adds it at once.

## 7. Positions (`CAIUnit::PositionLandUnits`, `FINDINGS-moving.md` A.2 to A.5)

### 7.1 The dispatcher

The **positions** are the area's scored records (section 3). n = the committed divisions. The **allied-soil budget** - n
/ 2 where the country leads a faction, else n / 5 where its capital's area borders land at war with it, else n / 3 - less
those on a friend's soil already. Then: **the split** (7.2); **the line** (7.3) where there is a line and positions;
**only where that moved nothing**, the garrisons (7.4); **only where neither did**, the reserve (7.5). Full pass pending
is cleared - on every pass that runs the dispatcher, full or every third hour, and nowhere else
(`CAIUnit::PositionLandUnits`' end, `FINDINGS-liveread.md`, section 134).

### 7.2 The split

1. Each committed division with a `garrison_brigade` is a **garrison**; one marked already stays so only while it stands
   in land the country controls and is not airborne with air transports about.
2. **Garrison targets**: from the hold list, best first, those scoring 20 or more that the country controls, but not a
   province of the area under 10 victory points that is not its own depot. **strong** = those scoring 50 or more.
3. With p positions: garrisons g and line l. No positions: g = n. No hold provinces: l = n less the garrison-brigade
   divisions. Otherwise: front = the front list's size; enemies = the land units on it whose controller is front-worthy and
   whose owner is no friend by the original's `IsFriend`, military access counting (section 38 of the live reads); at war on the frontier front x 2, at least 2 x enemies where their areas hold fewer than 7 provinces in
   all (else enemies), strong = 1 where front <= enemies / 3, enemies x 2; want = min(max(enemies, front), 10 p); g =
   min(hold provinces, n, n - want), raised where below it to min(min(max(targets, garrison-brigade divisions), n), n -
   front), and at least strong; l = n - g, anything over 10 p back to g.
4. Targets are topped up from the rest of the hold list until there are g of them. The list is **kept in descending score**, so one
   added here stands in its place by score, of equals the earlier first (`FINDINGS-liveread.md`, section 21). A **record** already
   taken is skipped, not a province already listed: the hold list can name a province twice, and both records then become targets,
   each wanting its garrison (section 36). Bulgaria's list on entering the save of July 1941 names 4368, 4555 and 4870 twice.
5. **Per block of land** of **the first g targets** (the shortfall to the theatre's largest area's) - **the blocks in the
   order their region groups were made**, standing in for the original's, which is that of the groups' addresses and
   differs from run to run (*Ours*; `reversing/findings/FINDINGS-liveread.md`, section 130) - the garrisons, as the
   garrison list has them, standing in the block or else moving there (the movement reader, section 10) - one standing in
   one block and bound for another counted in both: Poland's 55. reserve division, at 2447 for
   2701, is Warszawa's garrison on 6 September 1939 - are matched to its count: too many - the non-garrison-brigade one with the lowest score is unmarked, the score
   being **that of the last garrison target on the province where it stands or is bound, 1 where no target is** (+1000
   for militia, +500 for police), the first of equals going - Poland's Warszawa HQ at 10:00 on 1 January, its 9th and
   15th Dywizja at 1626 and its 55th Reserve at 2158 all on no target, drops the 9th, which then goes to the line
   (`confirmed` on the running game, 2026-11-13, `reversing/findings/FINDINGS-liveread.md`, section 70). **A dropped
   division leaves the block's list and the garrisons by the original's swap with the last entry**, so the rest do not
   close up in order: the next drop's first of equals and the order the garrisons are sent in both hang on it - China's
   Yan'an HQ at 15:00 on 1 January, marked 4., 30., 31., 27. and 28. Jun for three targets, drops the 30. and then, the
   28. now standing second, the 28., and sends 4., 27. and 31. Jun in that order (`confirmed`, section 71); too few - of the unmarked committed divisions standing or bound there, facing
   from where they are bound no neighbour the country prepares war on, carrying no armour or mechanised brigade **where the
   agent's frontier is at war** (not the country's wars) and no paratroops with air
   transports about, the one with the best 10 x garrison rating (section 11.3) + (its mean distance to the area + 10) / 5,
   x 0.75 on a front province, x (1.5 - its mean strength share) is marked. **Its strength, not its organisation**:
   `confirmed` on the running game, 2026-11-11, where every candidate's figure was the strength share
   (`reversing/findings/FINDINGS-liveread.md`, section 34); this said organisation until then, after the fact base's prose.
   A reserve's share, and its garrison rating's fill, are of its reduced most (section 38): Germany's reserve infantry in
   August 1938, at their ceiling, rate at 1.5 - 1 and not 1.5 - 0.75, and the 3. Panzer-Division is marked before them.
   The pick is in the original's precision - the rating's fill a single per brigade, the score a single with double steps,
   compared strictly - so two divisions a thousandth of a share apart do not tie (section 35).

### 7.3 The line

1. **Small fronts**: front provinces whose area has at most 3 provinces and fewer than 5 enemy land units, which a
   committed division stands in or is bound for.
2. **Share**: for each position in friendly land (others keep their old want), have = 0; one whose only front neighbour
   is a small front wants -1000; otherwise cap = **the position cap** and want = trunc(l x score / the scores' sum) up to
   the cap, the rest carried. **The position cap**: nought unless it borders enemy land or lies in the area; the stack
   cap (5) less min(6, 2/3 of its fort), at least 1; where every enemy neighbour's area has fewer than 6 provinces, at most
   the enemy land units beside it (at least 1, doubled where one of those areas is coastal).
3. **Count** [exe, `reversing/findings/FINDINGS-liveread.md`, section 13]: every committed division, garrisons too: the first position in the list that is where it stands or where it is
   bound takes it - standing there it counts as standing and heading unless it is on its way to a province no enemy holds, when it is
   counted nowhere; bound there, as heading. One falling back with under half its organisation is not counted. **For the line**, with focus areas and the frontier at
   war, every land unit of each country fighting on our side that holds a focus area's first province is counted the same way.
   **Ours: an ally's unit is counted by where its country's AI had it bound as the hour's passes began.** The original
   runs the countries' passes side by side on worker threads (`reversing/findings/FINDINGS-ai.md`), and the AI writes a
   unit's destination the moment it sends it, so whether one country's count sees another's orders of that hour is a
   race. A small country's pass is usually done before a great power's orders are given: at 09:00 on 16 July 1941
   Tannu Tuva's pass began 43rd and the Soviet Union's 98th, so Kyzyl HQ counted the three Soviet divisions in 2390
   standing and gave no order. Here the passes run one after another and the Soviet Union's comes first, so its orders of
   that hour sent them away first and Tuva marched its army into 2390. Both July 1941 recordings agree with the hour's
   start (`reversing/findings/FINDINGS-liveread.md`, section 111). **But Germany's orders of the hour are read as given**:
   the main thread runs the list's first six - `---`, REB, Germany, Britain, the Soviet Union, the United States - and
   Germany's pass begins about eighth of 102 every hour, Britain's about 81st, the Soviet Union's about 92nd and the
   United States' about last (192 hours of four recordings). At 09:00 on 16 July Germany's pull sent the 169.
   Infanterie-Division, the 6. SS 'Nord' and the 1. Gebirgsjäger to 1306, and in both runs that hour gave it the Slovak
   2. Pesi divize's count saw them, sending it to 2508 where the hour's start sent it to 1306; Slovakia's pass began
   before Germany's ended in one of the two and after it in the other (section 143). So **ours**: the countries of the
   list up to Germany are read as their orders stand, the rest as the hour began. It is a race all the same - Hungary and
   Bulgaria, whose passes begin soon after Germany's, often give their own orders before Germany's first - and a
   country whose pass begins at once on a worker thread may count before Germany's orders are given. A country's own
   units are read as they are.
4. **The rest**: nothing given out - every position wants 1; otherwise while the carry is
   over 0.1, +1 to the position with the least want (an over-cap one sorted after), one scoring under 1 left out where the scores come to 1 or more.
   The original gives 2 where a unit standing there has an entry on a list of the unit's own, **not where it is dug in**
   as this said until 2026-11-15: not one of the 2,588 units in the running game at noon on 17 July 1941 held one, and
   nothing here keeps it, so every position wants 1 (`confirmed` on the listing and the running game,
   `reversing/findings/FINDINGS-liveread.md`, section 102). Iraq's Baghdad HQ, its positions made again at 12:00 on 17 July
   as 5463, 5485 and 5552, wanted 2, 1 and 2 by dig-in and pulled the 1. Iraqi Infantry Division off its way to 5485; by
   ones none is short and it marches on, as in both runs.
5. **Supply cut**: where want > trunc(3.5 x cap) and the province's supplies are nought, or its need is five times its
   supplies, want is cut to that and the rest goes to the others under their own 3.5 x cap.
6. **Each committed division** (not a garrison, not airborne with transports), d where it is bound or stands. **Bound
   for enemy land** (held by a country at war with it or by the rebels): **passed over** with a way of fewer than 2
   provinces - an attack on a neighbour is left to run - or where d's area has 3 provinces or fewer; otherwise on as
   below. **Bound for a province that is not an enemy's**, not retreating, holding ground in a battle, and d not a depot:
   sent on to its next step (to d with a way of one), and on as below all the same (`FINDINGS-moving.md` A.4b). Then:
   **staying** where d is a position that wants divisions and whose standing are within its cap - not its have within
   its want; **then only where the may-leave test lets it go** (7.6), strictly for an agent below a theatre, the battle
   it defends not asked about (`FINDINGS-moving.md` A.4e); otherwise, over the positions in their order with a want and under their cap: one where it stands is taken at once - where it is bound for a target province away from the hostile routes, only a position beside one is considered, and scored. Else the reachable position with the lowest key, in single precision [exe, `reversing/findings/FINDINGS-liveread.md`, section 17] - have / want x 100, and the excess over 1 x 100 again;
   at war instead the distance from where it is bound x (have / want + 0.1), x 0.66 for a land battle there; then -50 x its terrain figure there (section 11.3); then x 0.66 for a depot and for a province beside a hostile route; with the allied-soil budget spent, not onto a friend's soil from the
   capital's area (**the soil test**, below). No position and d none: a second-ring province, drawn on a generator seeded with
   **the original's own number for the unit** - the serial of its save pair, which for a unit minted in play is not this
   game's one count (`savegame.md`, section 5; `CGameState.OriginalSerial`) - the first draw's signed remainder by the ring's
   size; not where the unit is bound or stands, only where the land reach test lets it, and the line counted moved whether
   or not the move posts anything. Guangxi's Duli Di'28 Shi, number 6200 of type 4713 in the war start's save, draws its own
   province, so the line moves nothing and the garrisons go, as in both of the original's runs
   (`reversing/findings/FINDINGS-liveread.md`, section 91).
7. **Each position still short** - its fill worked out once, before anybody is pulled to it - pulls the division with the least squared distance x 16 + its dig-in / 100 + its attack
   delay / 1000 - 100 x its terrain figure here (x 0.25 where it is bound for no position or one that wants none, else
   + 100 x its figure where it is bound), the first of them that can reach it and that the may-leave test lets go, as in
   step 6. **Only from a position fuller than this
   one** (`FINDINGS-moving.md` A.4d): this one's fill is (have + 1) / want, the 1 left out where it is its own depot;
   the other's is (count - k) / want, count being those standing there where the division stands in it and those it has
   otherwise, and k - the division itself - 1 unless this one is its own depot, or has nobody and the other more than
   one; a division bound for no position, or one that wants none, or standing here while nobody else does, is taken
   without asking. At war below stance 2 a division whose position is not over-filled moves only two steps. A pull
   takes one off the other position's count, and off those standing there where it stood in it. **The pull counts its
   picks on a count of its own** - the position's have as the pull begins, one more for each pick - and stops when that
   reaches the want; "has nobody" in k is that count. **A division already bound here is not passed over**: its other
   position is this one, so picking it takes one off this position and puts one back, and the pick is spent. At 10:00 on
   16 July 1941 Smolensk HQ's pull for 2390 took the 124., the 111. and the 31., then the 124. again, which left the
   176. Strelkovaya free (`reversing/findings/FINDINGS-liveread.md`, section 110). **Never pulled**: one retreating, a garrison, one
   airborne with transports about, **one bound for land of a country at war with it beside where it stands** - an
   attack under way - and **one in a battle whose own province is a position that wants divisions** (A.4b). Also never pulled: bound for its own depot under a blocking order, and with the allied-soil budget spent, onto a friend's soil from the capital's area (the soil test).
   **The soil test** (`CAIUnit_IsMoveFromHomeAreaToAlliedSoil`, `reversing/findings/FINDINGS-liveread.md`, section 37): the
   position's province held by a friend - not the country, nobody or the rebels; on its side, or fighting beside it and not
   its enemy - and the division in the capital's area **by where it is bound** (where it stands, bound nowhere) where the
   country holds that; bound for enemy ground, by where it stands, the country holding that. A division standing on a
   neighbour's land and bound home is in the capital's area; it is the send's and the pull's alone. Finding none, with the frontier at war, the nearest fit held-back division (strength 0.75, organisation 0.9, off the front,
   within 80) is sent, one at a time while the position is short, to a position with nobody, or to one with somebody only beside a hostile route or under an attack going against it, and is no
   longer held back - "short" and "nobody" by the pull's own count.

### 7.4 The garrisons

**Each target is a record**, a province listed twice being two, each counted apart, a province looked up as its last
record. Each target wants the garrisons / the targets, the first remainder ones one more; a threatened one adds the brigades of
hostile land units bound for it; at most the stack cap. Candidates: the garrisons, not holding ground nor falling back,
that the may-leave test lets go strictly (7.6), in the
target's area (anywhere for a focus area), not in a threatened province, and not taken from a fuller hold position unless
the target is threatened. The nearest **from where it is bound** by squared distance goes (x2 for a role-5 division leaving a
better-scored position); **a tie goes to the earlier garrison in the split's order**: those with a garrison brigade and those
still marked, in the committed order, less any dropped - each by moving the last into its place (section 5 above) - then
each new pick as it was picked - the vector the original's split hands its fill (`reversing/findings/FINDINGS-liveread.md`,
sections 35 and 71).

### 7.5 The reserve

The second ring's provinces, each with room for round(10 x its infrastructure). Each held-back division not falling back,
**not holding ground in a battle** (2026-11-03: ours had left both out, and moved held-back defenders out of their
battles to the rear), with no order but a plain move, not a garrison, not already in such a province within its room
(counted as a position's units are, 7.3, so one standing in a ring province and bound for another counts in neither): **stays** beside a war candidate across a river
or in a fort with strength and organisation over 0.3, or beside a war candidate's units, fit, with none of the country's
idle divisions there or fewer than half of those - **the half in whole numbers**, the units over two truncated, so two
idle divisions are enough against five (`reversing/findings/FINDINGS-liveread.md`, section 144; ours had kept it while
twice the idle were fewer, and held the German 206. Infanterie-Division in 1808 against five Soviet units where the
original sent it to the ring); otherwise goes to the ring province, never the one it is bound for, with the lowest distance + 20 x those there
- 30 x (its depot's distance in steps - that province's).

### 7.6 May it leave (`CAIUnit_UnitMayLeave`, `FINDINGS-moving.md` A.4e)

Asked by the line (step 6 and the pull, 7.3), the garrisons (7.4), the sealift (section 15), the division picker
(`agents.md`, 10.2) and the province lost (7.7) before a unit is taken
away from where it stands. **Not where it is falling back.** In a battle: not while landing from the sea on enemy ground
with its landing under way, nor in a sea battle's first four hours; and, where the caller asks, not from a battle it
defends - a land battle holds nobody else. Then **the border**: over its province's edges that may be passed, to land
held by an enemy, the **enemies** - land units of countries at war with it - and the **incoming** - land units of
countries it may operate in, bound for its province. Not strict and nobody incoming: yes. Otherwise the **idle count**:
with no neighbour held by a war candidate, 1000; else the other divisions in its province, **whoever's**, with nowhere to
go and no way left, and not on the caller's list. With somebody incoming, no where the count is under 1; strict, no where
the count less the enemies is under 1; else yes.

So a division of an agent below a theatre facing enemy units across a border stays unless more idle divisions stay in
its province than there are enemy units beside it - **a lone defender facing an attacker is not sent elsewhere**, though
the battle itself never holds it. *Hardcoded*: the 1000, the four hours, every rule.

### 7.7 A province lost (`CAIUnit::OnProvinceControllerChanged`, `FINDINGS-moving.md` A.4e)

**At the moment a province changes hands**, every agent of the country that held it is told - inside the game's own
pass, a battle's capture or a rising at midnight - and one answers where the province is beside its theatre (an edge
that may be passed to a land province stamped with its theatre) and **its area now has three provinces or fewer**:

    enemy = the brigades of land units in the area's provinces whose owners are its enemies there
    ours  = the brigades of the agent's land units bound for, or standing in, the area or a province beside it - listed
    while 2 x enemy - ours > 0:
        of the agent's land units not listed, that may leave (7.6, not strict, not from a battle it defends), not
        reserved for a transport, at a third of their strength or more and with a speed of a whole unit or more:
            idle  = the idle divisions beside it (7.6), the listed left out;  under 1: passed over
            score = map distance to the province / speed,  x 10 for a headquarters,  x max(10 - idle, 1),  x 2 fighting
        the lowest under 10,000,000 that can reach the province and may leave strictly is listed, its brigades taken
        off, and sent by the plain way (section 10); none: done

So a counter-attack into a small area - an island, an enclave, a province risen in revolt - and never a defender taken
from its battle. Seen on a recording of July 1941 (`reversing/findings/FINDINGS-liveread.md`, section 122): the rebels
take 6151 in Indochina at midnight on 18 July and Saigon HQ sends the 77th, 81st and 86th Hoheishidan in at once, as
ours now does; the march check makes them strategic redeployments the next hour, an enemy's province at their end
notwithstanding - the AI's redeployment is refused only where the unit is not a land unit (`CSetUnitOrderCommand`, the
original's `CSetOrderCommand::IsValid`). *Hardcoded*: the three provinces, the third, the 10, the 10,000,000. **Ours**:
the agents of a country the AI does not run answer only where the player has handed their headquarters to it.

## 8. Falling back (`CAIUnit_FallBackFromLosingDefences`, `FINDINGS-moving.md` A.6)

For each province of the area that is not its own depot and has a fort under 3, each land battle there at least 12 hours
old (25 at stance 3 or more) in which the country defends and **the attacker holds more than 70% of the strength**:
its neighbours are scored - one at war with the country across a river or a crossing ends it; one the country may operate
in (its side, or a co-belligerent not at war with it), infrastructure + 2 x fort, +10 across a river, +100 for the
province's depot, / 100 inside the area. With no friendly land unit standing beside the province and bound for it, every
defender with no blocking order is sent, **the last of the side first**, one to each neighbour in score order, round again
(`FINDINGS-liveread.md`, section 24) - **but where a neighbour outside the area exists, the walk ends at the first one
inside it**, and that ends the sending: the list is gone round again only when the walk reached its end
(`FINDINGS-liveread.md`, section 118). So with two neighbours outside and any inside, two defenders go and the rest stay
for the next pass: the July 1941 save's 1809 sends the 119. to 1810 and the 125. to 1929 at 15:00 on 17 July and the 64.
an hour later, where ours had sent the 64. back round to 1810 with them. The original sends an ally's defender too; here only the country's own. A sent unit
sets full pass pending, after the dispatcher has cleared it, so **the next hour's pass is full**, whatever the hour
(`agents.md`, section 3; `FINDINGS-liveread.md`, section 134).

## 9. The march check (`CAIUnit_CheckLandMarches`)

Over the agent's land units **not retreating**, with a destination and a way: a march long enough - not to a neighbour,
and the distance at least the unit's speed x 0.25 x (200 at war, 600 at peace) - becomes a **strategic redeployment**
(section 10); **with more than one province left**, a unit in a battle whose next step is enemy-held is stood down - so an
attack, whose way is the one province attacked, is left alone; a headquarters whose way crosses enemy land in an area of
more than 3 provinces is stood down, a division cut to its next step.

### 9.1 Paradrops (`CAIUnit_PlanParadrops`, `CEU3AI_RebuildWeakEnemyPorts`; `FINDINGS-liveread.md`, section 127)

Built 2026-11-18. **The targets** are made again once a day, at the country's hour of the day (`id % 24`), beside the
invasion ranking: over the countries the country is at war with, in the database's order, and then its prepared wars'
targets - not deduplicated, so a country on both is walked twice - each target's own **capital** (not where its
government sits) where no land unit of anybody's with a brigade stands, and every port of each of its areas of **at most
5 provinces** where none stands. Each is scored, in single precision, **100 a whole level of naval base built** + **10 a
level of air base working, in fractions** + **1000 where the province is its own depot**, kept above nought, highest first
and a new one after its equals. A loaded game has none until its hour, as the original's list is not saved.

**The planner** runs in the land block between falling back and the march check, only for an agent with air transports
(`agent.AirTransports`), and only where its headquarters has a theatre. Its airborne armies are its land units of the
airborne role, of every level, in the order of its land units, put on their list as they are sorted - and there **a staging
province with no air base built to level 1 is cleared**, with the set-param command (sealift target kept, staging nought).
k is the lesser of the airborne armies and the transports.

1. **Targets**: down the list while fewer than k, each takes **the nearest air base** of any area held by the country or a
   country it is not hostile to (itself first, then the database's order) - at war, only its own where the target lies on
   another continent from the headquarters - if its distance x 1000 is within **the first transport's range** (ours: the
   unit's shortest wing range). An area's air bases are its provinces with an air base built to a whole level, the
   largest first. The base is the slot's **staging**.
2. **Staging**, round the slots in turn: each base takes the nearest free airborne army not aboard a fleet and not holding
   ground - one already standing there or bound there at once - the distance in single precision, **100,000 more where
   the country cannot get from the base to the army by areas** (enemies avoided). Its staging becomes the base (set
   directly and by the command). Bound there already, nothing more; joined, **it marches there** (the AI's move, very
   safe); else it marches to the nearest air base of its own area, if it is not bound there; else, with no sealift target
   yet, its target becomes the base's area's nearest port (the command, staging the base) - a call for the sealift. A slot
   that finds no army ends the round; the rounds go on while one gave somebody and armies are left.
3. **Boarding**: each airborne army not aboard a fleet and not holding ground, whose destination (or place) has an air base
   built and a holder it may operate in, takes the nearest empty transport (measured from the transport's base to where
   the army stands), each transport once. A transport based elsewhere is sent there with `rebase_air`; one based there
   loads the army (`CLoadOnPlaneCommand`) and is sent on `paradrop_mission` **to the army's staging** - unless the army
   is at its staging, is not standing there, or may not board.
4. **Dropping**: for each slot whose target is now held by an enemy (a war, the rebels, or a war nobody declared over it),
   every transport based at its base, going nowhere and with no order (or `support_attack` or `carrier_protection`):
   empty, it loads one airborne army standing at the base and not yet loaded and flies `paradrop_mission` at the target;
   carrying, with no order, in no battle and at its base, it is sent.

**A land unit aboard an aeroplane still walks.** The 1. Fallschirmjäger Division of the July 1941 save is aboard the 14th
Air wing, based at Oslo, while it stands in Berlin - the running game's `carried_by_air` points at the plane and the
plane's carrying list holds it. At 03:00 on 18 July Oslo, Norway's capital and empty, comes on the list with Oslo itself
as its base, and the planner marches the division there by strategic redeployment through Sweden, where Germany has
military access; it walks, still aboard. So nothing here holds a plane's passenger back from a way of its own
(`transport.md`, *Ours*, struck through; `enhanced.md`).

## 10. Moving a unit (`CEU3AI::MoveUnit`)

To a province: a retreating unit is not moved; **nor one the click's test refuses there** (`movement.md`, section
1) - so a unit marching into a battle is neither halted nor sent anywhere, and the pull-in of a headquarters attacking
(6) does nothing: Poland's Armia Modlin, attacking 1749 from 1809 at the war's start, is picked 1809 and kept on its
attack, where ours had halted it (`confirmed` on the running game, `reversing/findings/FINDINGS-liveread.md`, section
88); where it is the province the unit stands in while it moves, a
**stand-down** - no order, no way, out of the battles it attacks in, where it stands (`CCancelMovementCommand`); where it
is going there already and not fighting, nothing - **in a battle it is ordered again, which takes it out of the battle**,
so a caller that does not want that skips it (11.2); otherwise the march (`CMoveUnitCommand`), or a strategic
redeployment where 9's test says so (`FINDINGS-moving.md` A.4c) - **whatever order the unit already holds**: a division
redeploying that is sent somewhere else far off is given a new redeployment, and keeps its pace. Ours had passed over a
unit already redeploying and sent it a plain march, which left it standing a day until the next pass redeployed it -
the United States' 43rd and 1st Cavalry Divisions re-aimed at 11:00 on 1 January (`confirmed` on the listing,
`reversing/findings/FINDINGS-liveread.md`, section 75).

**Where a unit is bound** is asked two ways, by two readers of the original's. **The pending destination**
(`CUnit_GetPendingDestinationProvince`, `CAgentUnits.Destination`): where the AI sent it this pass, or its way's end;
**with neither, the province of an order still under way** - anything but none or a plain move, a support attack's
among them. **Every order the AI gives binds its unit at once but a support attack**, which binds nothing until its
command runs: so a unit the attack (11) has just told to support one target is still free for a later target of the same
pass, and is sent there if it joins - the Soviet 3. Gvardejskaya Strelk. Volnovakhskaya Div. at 1750 joins the attack on
1689 at the load of July 1941 as a unit that must stay and support, and is then sent to 1749, where the original's own
run sends it (its support order itself is not in the recording) (`confirmed` on the
listing, `reversing/findings/FINDINGS-liveread.md`, section 124). A stand-down clears the first at once and posts its "no order" as a command, so a division the AI halts on a
strategic redeployment answers the redeployment's province for the rest of the pass: the second ring "halts" the Soviet
99. Strelkovaya Diviziya where it stands at 144 at 06:00 on 17 July 1941, and the pull, seeing it bound for 1589 and so
for no position, takes it to 619 without the fullness test, in all three runs (`confirmed` on the listing and the
running game, `reversing/findings/FINDINGS-liveread.md`, section 101). The attack (11) asks this one, and so does the
line's look at each committed division (7.3, step 6) (`reversing/findings/FINDINGS-aiconsumer.md`,
`reversing/findings/FINDINGS-moving.md`, A.4b): so a division waiting beside a province on a support attack counts among
its attackers - Poland's 30 Dywizja Piechoty at 1808 at the war's start keeps the odds on 1749 at 1.5 and the attack
going, as in both runs, where without it they fall to 0.04 and the 13 Dywizja Piechoty is halted. **The movement
destination** (`CUnit::GetMovementDestinationProvince`, `CAgentUnits.MovementDestination`): **nowhere under a support
attack**, whatever its way; otherwise the same. The pull and the count of a line's positions ask it (7.3), as do the
headquarters' places (`agents.md`, section 6), the detach (2), a unit's anchor and the embark tally of the sealift (15) and an
invasion's march to its port (16) (`reversing/findings/FINDINGS-liveread.md`, sections 14 and 101;
`reversing/findings/FINDINGS-power.md`; `reversing/findings/FINDINGS-subdivide.md`;
`reversing/findings/FINDINGS-theatre2.md`) - a division on a support attack counts at the province it waits in. Which
reader the rest of the original's callers use was not looked up; they ask the pending one here. Set against the July
1941 series' three runs: 102 alone where there were 109, the 1. Gebirgsjäger-Division at hour 29 among them; the war's
start, entering and 4 January unchanged.

**The way.** A move asked for safely - the line's, the garrisons', the rear's and the reserves' - takes the **very safe**
way: short of the goal, a land unit does not step into a province held by a country at war with it or the rebels, nor
into one with a land battle in it. The line's reach tests (7.3) - the send's and the pull's - ask what the original's
asks of a land unit (`FINDINGS-balancing.md`): **no way at all, but whether the areas are joined** (`CAiMoves.ReachesByLand`),
as the balancing does. They asked the safe way until 2026-11-12, when the running game's new game showed the difference: at
its second hour Italy's line sends four Eritrean divisions to 9734, already full, and none to 9960 or 9980 in Somalia, which
no land of Italy's joins to Eritrea - where the safe way, through weakly held Ethiopian land, had chosen them, the very safe
move had then found no way, and the pull had seen both positions filled (`reversing/findings/FINDINGS-liveread.md`, section
60). The marching loop the safe way once ended (`FINDINGS-moving.md` A.4c) does not come back: no area held by an enemy joins
two areas. Other reach tests - the garrisons' - still ask the safe way, not having been set beside the running game. The fallback and the march check's cut take the plain way, which only the step rules hold back
(`movement.md`). **A strategic redeployment** always takes the very safe way, whoever orders it (`movement.md`, section
3). It is an order: the unit goes at `STRAT_REDEP_BASE_SPEED` x its province's infrastructure (between a hundredth
and a whole) instead of its own, and does not fight on the way (`movement.md`); each hour of the way starts its attack
delay again (`landorders.md`, section 8). **A support attack** is an order: the unit is stopped where it stands, waits
beside the province until a land battle begins there, is given the way in then and joins it without making progress -
it attacks and does not enter - and the order ends with that battle (`landorders.md`, section 7). Its way into a
province where nobody fights yet is taken away with the order.

## 11. The attack (`CAIUnit_ManageLandUnits`, `FINDINGS-aiconsumer.md`, `FINDINGS-moving.md` B)

Nothing for an agent with no committed division or whose frontier is not at war.

### 11.1 The targets

**The ramp**: the front provinces' distances from the area's first province's depot, max and min. For each front
province p held by an enemy: weight 1, or (max - its distance) x 500 / (max - min); x2 where its area has under 3
provinces. Its land neighbours: an enemy one adds half its objective priority; one the country may operate in is an
**approach** (all approaches in the target provinces makes **all targets**), and makes it worth attacking where it is the
country's own or a country's that exists. Not worth attacking: dropped.

- **Stance 3 or 4**: + p's objective priority; + 90 on the plan's way, + 45 beside it, for each of its two ways; + 1000
  for all targets; + its nearness to the objectives (section 4).
- **Every stance**: + (approaches - 1) x (20 at stance 3, 10 at 4, else 50); for each hostile route + 1000 where it is p,
  + 500 where beside it.
- **The dead end**, off the routes: a single approach, in an area of more than 5 provinces, leading to an area of more
  than 3, from which fewer than 3 approaches go on and no sea that p touches too (a sea beside the approach alone does
  not count; *read* 2026-11-12, `reversing/findings/FINDINGS-liveread.md`, section 59: Italy's 9807, inland behind Eritrea's
  coast, dropped as the original's), while another enemy front province of p's area has an
  approach p lacks - dropped where that approach has fewer than 2 ways on or below stance 3, else x0.25 (stance 3) or
  x0.5.
- **Stance 3 and up**: for each enemy neighbour with a friendly neighbour of its own and fewer than 3 enemy ones, +125
  (+250 at 4); approaches cut off from each other, +200 (+400).
- A naval base: + 5 x its level + 100.

Kept in descending order.

### 11.2 Each target, best first

**Stance**: 4 where the weight is 500 or more, else the plan's. **May attack**: the plan's stance over 1; or the province
the rebels'; or its area under 6 provinces; or nobody standing there and stance 3 or more; or its friendly neighbours
lying in more than one block of land (**shared**).

**Candidates**: the childless land units beside p - **where bound somewhere**, not bound for enemy land, not a garrison and
this agent's; **one standing still is a candidate whichever it is**, a garrison or another agent's unit among them
(*measured*, `reversing/findings/FINDINGS-liveread.md`, section 58: Italy's Divisione Libia, picked a garrison at a new
game's second hour, joins the attack on 9987 the same hour); may attack; not in a battle - **on either side**, `IsHoldingGround` - where bound somewhere, or not shared, or not standing in a
target province; not dry of
fuel; not held back unless shared; not reserved for transport - bound by a sealift for the plan's debark and walking to or waiting in the embark area or bound into the debark area. **Cost** = 10 x the enemy land neighbours of
where it stands - 10 x its terrain figure at p (11.3) + 100 where its garrison rating is over 95; / 10 for another
agent's unit, else / 5 with a blocking order. Taken **lowest cost first**, equals in the order met - the list kept
ascending, a new candidate put before the first of a higher cost (read again 2026-11-12, section 58).

**The defenders**: p's land units at war with the country, the rebels', or of an undeclared war covering p. **The odds**
(11.4) of the units already bound for p against them.

**Committing**, each candidate: skipped with a combat cooldown and defenders; retreating; where the odds are at least the
stance's bar (5 at stance 2, 3 at 4, else 4) and its province has more than one enemy neighbour; and, where p's area has
more than 5 provinces, not shared, and p is defended or the stance under 3, strength under 0.7/0.5/0.6 or organisation
under 0.75/0.5/0.6 (stance 2/4/else). It **must stay** - and supports rather than marches - where something beside it is
**uncovered**, where it stands draws on a depot, nobody there can stand in for it, and it does not stand in a target
province. **Uncovered** neighbours of its province, by edges not impassable, in a real area and held by an enemy - **p
itself left out** - are those where anybody stands or a land unit of the holder's or a friend's beside it is **bound there - its way ending
in it, not merely its next step**, as the original's collector asks the path's last province (`confirmed` on the listing,
`reversing/findings/FINDINGS-liveread.md`, section 94: at 21:00 on 16 July 1941 the Soviet 5th Army passing through 1351
on its way to 1530 leaves 1351 covered, and the 5th H Arm Division goes into 1395 by a plain move, as in all three runs,
where ours had given it a support attack and so its way an hour later) (any, where the unit's area is
one province and the neighbour's more than five) that no land unit of the country standing in the unit's province is bound
for - at stance 4, none of the country's at all (*read* on the listing, `reversing/findings/FINDINGS-liveread.md`,
section 85: at 21:00 on 6 September 1939 the 36. Infanterie-Division (mot) beside nothing but its target goes in). A stand-in is a land unit there that is not a headquarters, in no battle, bound
nowhere, not reserved for transport and **not already joined to this attack**, of the country's own or of a co-belligerent
it is not at war with, neither of them the rebels. So of three units in one province the last to join supports, the two
before it each having had a stand-in (*measured* on the running game, `reversing/findings/FINDINGS-liveread.md`, section
54). It joins; the odds are worked out again, and where they fell it leaves.

**The decision**: attack bar 3 at stance 2, 1.1 at 4, else 1.5; cancel bar 0.8 - 0.2 and 0.01 where shared or where an
attacker draws on no depot while p has a working port. Where it may attack, the odds reach the bar and at least one
joined unit need not stay: each not bound for enemy land - **p included, so a unit already on its way in is not ordered
again** - and not retreating attacks - **a move into p**, or a **support
attack** for one that must stay. Otherwise, where the odds are under the cancel bar before and after: each joined unit
bound for p, not retreating, **stands down**.

### 11.3 A unit's figures

- **Garrison rating** (`RateUnitAsGarrison`): over its brigades, score 100 police or garrison, 90 militia, 75 paratroops or marines,
  20 infantry, 10 mountain, 1 else; r = (score / n + (2 - n) x 20) x (2 - mean strength share), between 1 and 100; nought
  with no brigades.
- **Terrain figure** (`GetUnitTerrainModifier`): its type's terrain figures for p's terrain - attacking, attack + movement; otherwise its
  defence, + the same attack + movement where a land neighbour is held by a war candidate.

### 11.4 The odds (`CAIUnit::EstimateAttackOdds`, `FINDINGS-attackodds.md`)

Each side's units u, weight = mean strength share x mean organisation share x (engagement + its modifier): A += (1 + the
country's `soft_attack` modifier) x soft attack x weight; B the same for hard; C the mean softness; D the mean
toughness (theirs: defensiveness) x its last combat's defence product x its landing's defence scale / 10^6 - **the
product of the last hour any battle worked it out, kept when the battle is over**, nothing clearing it, and nought on a
unit no battle has worked out since it was made or loaded, a save keeping none (`FINDINGS-attackodds.md`, section 7).
So the four German divisions whose attack on 2390 has ended by 10:00 on 16 July 1941 still count their toughness at
noon: the original's odds stay about 10 in all three runs and its 6th H Arm Division keeps its way to 2448, where ours,
counting toughness only while a unit fought, had fallen to 2.8 and sent it in (`confirmed` on the running game,
`reversing/findings/FINDINGS-liveread.md`, section 93). *Set beside the running game on 2026-11-10, 81 estimates of 16 July 1941: within 11%, half within 4%, Germany's single-unit estimates within 0.5% but one (`reversing/findings/FINDINGS-liveread.md`, section 6).* Ours: engagement x (1 - the attacker's
share) for each battle it defends in, again where that share is over a half. Theirs: for its first battle as an attacker,
x min(that battle's odds, 4) / 4 (0.01 for nought). Their damage = (1 - D_ours / 100)(A_theirs C_ours + B_theirs (1 -
C_ours)); ours the same the other way; **odds = ours / theirs** - nought with no unit, 100 with no defender, 50 with no
damage of theirs. The modifier (`FINDINGS-aihelpers.md`): the sum, not the product, of the combined arms, experience,
leader, dissent, supply and difficulty terms, the stacking from brigades over 3 a province, the weather, a river or a
landing, the terrain and the fort - for attackers; the fort defence, combined arms, experience, leader, dig-in, terrain,
dissent, supply, difficulty, encirclement, stacking and envelopment - for defenders. The stacking is less the country's
`unit_cooperation` and a hundredth for each point of the theatre leader's skill, as in a battle, and the country is the
lender's for a lent unit. *Set beside the running game on 2026-11-12, an hour after a save with every unit's cached half
fresh: 183 attackers' modifiers and 130 defenders' of Germany, the Soviet Union and Italy, every one the same
(`reversing/findings/FINDINGS-liveread.md`, section 55) - Germany's 0.050 apart wherever crowded until `unit_cooperation`
was taken off.* So the Soviet Union's estimates that stood about 4% high in section 6, read in the first hours after a
load, were most likely its units' cached halves still nought there (*inferred*: nothing differs once they are fresh). Nothing holds the odds above
nought: an attacker whose modifier is below -1 weighs negative, and the odds with it (`FINDINGS-liveread.md`, section 46).

The original keeps the half of each modifier that hangs on the unit alone - for attackers everything before the stacking,
for defenders everything but the stacking and envelopment - from the unit's hour to the next, and **never initialises that
cache**: after a load each unit's first look counts the real half or nought, by what memory held, a quarter of them nought
in one run of the 1941 save and others in the next (*measured*, section 46). **Ours** counts the real half always - it
cannot do otherwise and it is what most units used - and works it out at each look rather than once an hour
(`docs/compatibility.md`, entry 24). Entering a loaded game can therefore attack where the original happened not to: Japan's
11\. Hoheishidan on 9381 in August 1938, whose half was nought that run. **The savegame tool's playback hands over a
recorded run's halves** for entering alone (`CGameState.FirstLookModel`, `docs/replay.md`, section 9), memory's float
where the dirty byte was nought - nought nearly always, now and then leftover bits, and now and then not a number: a
defender's NaN makes the original's damage not a number, which its test against nought (`comiss`, `jbe`) takes as no
damage, so **the odds are 50** and the attack goes in (`CAiCombatModel.NotANumber`; *measured*, the Soviet attack on 2701
on entering July 1941, `reversing/findings/FINDINGS-liveread.md`, section 109).

### 11.6 The axis (`CAIUnit::UpdatePlanAxis`, `FINDINGS-agents.md` E.5)

At the agent's hour A0, while its plan has not been posted, and at every change of stance, for a theatre
agent of a country the AI runs: at stance 3 or 4, of every pair of a province of its area and one of its plan's
objectives the nearest, and the way between them from that province on, is the plan's **path**; at any other stance
the path is empty; a second path is always empty. Posted only where it differs. The attack weighs it (11.1).

### 11.5 Calling off (phase 3)

Each land unit of the agent in a battle, bound for an enemy province not on a hostile route: standing in a target
province, stood down where every friendly neighbour of where it is bound lies in its own area; defending where the
attacker holds more than half the strength, stood down, falling back or not; on a support attack where every attacker of its
battle has an order that is neither a plain move nor none, stood down (`FINDINGS-liveread.md`, section 20).

## 12. The sea targets (`CEU3AI_AssignSeaTargetsToAgents`, `FINDINGS-moving.md` D.1)

Once a day, at the country's hour (its number % 24), for the played country too, with **no sighting asked**:

1. **The hostile**: the countries at war with it, then those it has a war nobody declared with, then those it prepares war
   on, each once (`reversing/findings/FINDINGS-liveread.md`, section 138: the USA's 17th and 29th Navies, near Germany's
   ports, handed to Berlin HQ through the Atlantic's undeclared war in July 1941). Other AI passes that list the enemy ask
   the enemies and the prepared wars alone.
2. Every agent's lists are emptied; an agent's theatre gives its ports.
3. **Their convoys**: for each agent whose theatre has ports, over every pair of a step of the convoy's way and a port within
   3000 by sea: the ports' naval base levels summed and the distances summed. Every agent with a level or more is given the
   convoy; the one with the least `max((its want so far + the convoy's transports) / 20 / levels, 1) x distances / ports`
   adds the convoy's transports to its want.
4. **Their fleets**, wherever they are: over each agent's ports within 2000 by sea of the fleet, the distances, the levels
   and their number, a level or more needed; the least `max((the ships handed it so far + the fleet's) / levels, 1) x
   distances / number` - a fifth **where the acting capital's area is among the theatre's areas**, which need not be the
   theatre the capital province is filed under, and may be more than one (section 138: Berlin HQ's fifth, Berlin being Ob.kdo.
   des Heeres' province) - is handed the fleet.
5. **The raided waters**: the province of each finished battle that was a raid on a convoy, its defender a real country
   other than the rebels and on our side (ourselves, our overlord or its subjects, or fighting beside us and no enemy),
   ended within the month, goes to every agent with a port within 2000 by sea and an open way to it, once
   (`reversing/findings/FINDINGS-liveread.md`, section 115). A neutral whose convoys nobody raided has none, so it sends
   no patrol over another country's raided waters.
6. **Its own fleets** and its fellow fighters': each to the agent with the least mean distance from its ports - the same
   fifth - **over its ports within 2000 by sea, with a naval base level or more among them**, as the enemy's
   (and 2000 itself within reach for both; `confirmed` on the listing, `reversing/findings/FINDINGS-liveread.md`, section 78).
7. Each agent's **submarine want** is its want / 20.

The enemy fleets handed to an agent are what threatens its coast (section 6), and they and its own side's are what its
naval bomber want weighs (`agents.md`, section 9): a heavy cruiser 0.07, else a capital ship 0.15, else any ship but a
submarine 0.02 of theirs - **one of the three and never more**, so a heavy cruiser, a capital ship in the unit files, counts
0.07 alone - the same off for its side's but for a transport; so at peace the want is nought or below (section 78).

## 13. The pass's remainders (`FINDINGS-moving.md` F.8, F.9)

**Leaders**, for a country the AI runs, at hour (number + 8) % 24: each of its units with no leader, in order, is given the
first leader in service with a rank, commanding nothing and not given this pass, who may command it; a later one only
where he is fit for it by a quarter more. **Fit**: his greatest skill x 100, then for each effect of each of his traits x1.2
for surprise, defence, offence, encirclement and envelopment, x1.5 for combined arms where the unit has it, for
out-of-supply where it is short, for winter attrition where it stands in a weather picture below -5, x (half its brigades'
supply and fuel use) for supply consumption, x1.1 otherwise; half for a police or garrison division. The leader is put on
with both costs - his banked hours and the unit's organisation. **A leader in place is never changed.**

**Promotions**, for a country the AI runs, at hour (number + 16) % 24: its headquarters with no leader counted by level,
divisions left out; then from the corps up to the theatre, one each, the first free land leader in service is promoted
by (4 - his rank) - (the level - 1, or nought) ranks where that is one or more - a theatre or an army group to rank 4, an
army to 3, a corps to 2 - so that the leader pass can put him in command; fleets and air units are never promoted for.
Luxembourg's theatre, Force Publique, has Felix Bourbon-Parma raised from rank 2 to 4 on a new game's first day and given
him on the second, its organisation halved by his coming - the enemy power Paris and Strasbourg see falls with it
(`confirmed` on the running game, `reversing/findings/FINDINGS-liveread.md`, section 80; `CEU3AI_PromoteLeadersForCommands`).

**Stranded units**, for a country the AI runs, at its first pass and at hour (number + 16) % 24: each land unit standing,
not aboard, not fighting or retreating, in a province another country holds - not an enemy - in an area it may not widen
its fronts into, and not bound for its own land, an enemy's or such an area, is sent home: the nearest of its own
provinces it can reach, a quarter as far in its capital's area; else the nearest of a neighbouring area of its side that
borders no land held against it, 10000 nearer where it holds the province; else, a division, its area's nearest port;
else the capital. With nowhere, it is disbanded.

## 14. The fleets (`CAIUnit_ManageNavalUnits`, `FINDINGS-moving.md` D.2, `FINDINGS-airnaval4.md`)

For a country the AI runs, around its regrouping (`agents.md`, section 5.2): **every hour while it is at war or in a war
nobody declared, and at peace only on the agent's full pass or when its fleets are to be planned again** - and **phases 6
to 9 only on that forced pass**, at war as at peace (`FINDINGS-liveread.md`, section 25) - a foreign unit carrying troops seen
(`FINDINGS-agents.md`, A.5, row 19; `FINDINGS-reorganise.md`). **At war means at war with some country**: a war whose
other side has emptied - Ethiopia's capitulation leaves Italy alone in theirs, and the original never ends such a war -
makes nobody at war (`war.md`, section 1).

1. **A losing sea battle**: a fleet whose side holds under half of one goes home.
2. **Idle fleets home**: one not moving, with no order, away from its base, sails home - a troop carrier stays where it is
   while the plan has an embark.
3. *(the regrouping's port and repair, which leaves the fleets that are **ready**)*
4. **Orders that no longer make sense**, of each ready fleet: a join is left to run, the fleet and the one it joins
   handled; **any other order of a fleet holding a transport and carrying nothing** - a rebase under way among them, as
   the running game's French transport fleet lost its own at sea (`reversing/findings/FINDINGS-liveread.md`, section 65)
   - a raid or an escort at peace, a
   patrol at peace (a war nobody declared counting as war), and any other order of a fleet standing still at its base or an intercept standing still, are
   cancelled - the order taken away and the fleet sent to its base by a plain move, so it sails on with no order.
   **A raid at war is one of those "any other" orders** - only a patrol at war and an escort whose convoy is still there
   are kept before that test - so a submarine flotilla given a raid from its own base that found no way to its waters is
   cancelled at the next pass, still standing in port. The cancel is a command, so phase 7 of that same pass finds it
   still raiding and draws nothing for it; the pass after, it is free and drawn for again (`confirmed`, the Soviet 4th
   Navy at 782 on 17 July 1941, `reversing/findings/FINDINGS-liveread.md`, section 142). *Ours:* a reserve, a rebase, an
   invasion and a transport standing still at their base are still kept out of the test; whether the original's is
   reached by them was not looked at.
5. *(the regrouping's split and merge, whose joins - the fleet and the one it joins - are handled too)*. **On the
   handled list the fleet joined goes first and the joiner after it**, in phase 4 and in the merge alike, which the removal
   below follows: Britain's eight light cruisers split off the Home Fleet are then sent to the original's ports, 27th,
   26th, 24th, 22nd, 28th, 25th, 23rd and 21st in turn (`confirmed`, `reversing/findings/FINDINGS-liveread.md`, section 77).
6. **The handled fleets are taken off the ready list**, and nothing below touches them this pass - each in the order it
   went on the handled list, by the original's swap with the last entry, so the fleets left are not in their first order
   and the ports below are handed out in the new one: Japan's Imperial General Headquarters at the second hour of a new
   game then sends 14., 16. and 20. Kaigun to 5346, 19. to 5218 and 18. to 4986, as the original, where the first order
   sent the 18. to 5346 and the 20. to 5218 - which moved three home provinces' hold scores by the ships based there and
   the noon garrison drop with them (`confirmed`, `reversing/findings/FINDINGS-liveread.md`, section 72). **The survey**: the power of every fleet of a country at war with it within 1000 by sea of the theatre's ports, troop
   carriers listed first.
7. **Each ready fleet**, not retreating: **a wolfpack** - all submarines, not busy, at war, the naval
   stance not 0 - raids: over the enemy convoys handed to the agent, each step of a way but its ends, in its range, weighs
   the convoy's transports x |its place + 1 - the way's length / 2| - heaviest near the ends, nothing at the middle; one
   is drawn on the AI's generator by weight and the fleet given a **convoy raid** there for 504 hours. With no candidate,
   or fewer than half the steps seen **by whole division** (10 of 21 is not fewer), it **relocates**: to the port with the
   most raidable tonnage - 10 a transport whose step is strictly within its range of the port, **every step of the way
   but its ends counting, one already a candidate and one out of the fleet's reach as much as a new candidate**
   (`reversing/findings/FINDINGS-liveread.md`, section 132: the Soviet submarines of 17 July 1941, 10 at 906 and 986 and
   8 at 820 where until then 3 at each) - less 20 a ship over the port's
   level, the ships being those based there, those sent there earlier in the pass less those sent away (the tally the
   rebase below keeps, which this joins) and the fleet's own where the port is not its base; half again for its own base.
   **Where no port scores above nought or the best is its own base, it raids all the same** (`reversing/findings/
   FINDINGS-liveread.md`, section 114). Which of two equal ports wins is the original's hash map's order of addresses,
   which nothing here can repeat. **A fleet not
   busy is rebased** - **whatever it holds where its base lies outside the theatre** (the base province's theatre not the
   agent's), otherwise only with no submarine, where its base lies in another area than its commander's or is crowded past
   its level and two - to the theatre's port in its commander's area with the least distance from the commander + 10,
   +2000 a ship over the port's level, x0.75 for staying, in single precision, a fleet leaving taken off its old base's
   count (read on the listing 2026-11-13, `reversing/findings/FINDINGS-liveread.md`, section 64: the USA's 2nd USN
   Submarine Fleet, based at Manila, which the Philippines own, sails for Honolulu at the eleventh hour). **A fleet rebased
   so goes no further**: its turn ends there, neither counted nor free, though its order is still none until the command
   runs - so the patrol and the intercept cannot take it in the same pass (`reversing/findings/FINDINGS-liveread.md`,
   section 128: the British 10th Navy rebased to 2021 at 09:00 on 17 July 1941, and the 11445 patrol going to the 16th
   Navy). **So does a submarine fleet sent raiding or to a better port to raid from** (section 137: the British 41st Navy,
   sent to raid at 10590 at the same hour, is no part of the intercept on the Italian 11th Navy, which the 31st Navy is
   given alone). **A fleet holding a transport goes no further**: neither counted
   nor free, it is sent on no patrol and no intercept (`reversing/findings/FINDINGS-liveread.md`, section 42). Any other's
   power, with no rebase or raid, adds to the own power; a fleet not busy - or on patrol or escort - is **free**.
8. **Patrol**, with the naval stance not 0 and at war or with raided waters: the raided waters, or else the sea of each of
   the theatre's ports; while any are left, a centre is drawn and the four nearest to it taken with it; the nearest free
   fleet with no order, no submarine and a screen is given a **patrol** over them for 168 hours. None left: the phase ends.
   Each sea taken, and each fleet sent, leaves its list by the original's swap with the last, so which of two equally near
   comes first after it is the original's (`FINDINGS-moving.md`, D.2) - until 2026-11-16 the rest closed up in order.
   **Every order of the fleets' body goes as the AI's** - its rebases, raids, patrols and intercepts - which asks only for
   the unit, a province and a base and **no range**, as the original's `CSetOrderCommand` does (`reversing/findings/
   FINDINGS-liveread.md`, sections 122 and 128): until 2026-11-18 a patrol whose centre lay beyond the fleet's reach from
   its base was refused, and the fleet left with no order was taken again by the next pass.
9. **Intercept**, for each surveyed enemy fleet at sea: passed over where the enemy's power is over 1.5 times the own (2
   at naval stance 1) unless it carries troops; the nearest free fleets, reachable, with power, not on patrol unless the
   enemy carries troops, with a screen against all submarines, are gathered until they hold twice its power; holding its
   power - two thirds of it for a troop carrier - the first is given an **intercept** at it and the rest **join** it.
   The intercept is given on the enemy fleet, which the order names as its `target` (`CMission.OrderTarget`) and
   nothing in its hour reads: the fleet it chases is the one its own search finds (`savegame.md`, section 9). Named
   since 2026-11-05, when the save writer found all six of the original's saved intercepts naming theirs.

## 15. The sealift (`CAIUnit::AssignAreaTargetsAndSealift`, `FINDINGS-moving.md` F.1 to F.7)

At the agent's hour A6 - and at its next hour while its **reassign** flag is set, which a new agent's is, so a loaded game's
agents run it in its second entering pass (`reversing/findings/FINDINGS-liveread.md`, section 43) - for a country the AI runs - **and only where its units can carry at least one whole unit of
troops**, else nothing at all:

1. **The areas** are its theatre's, each with a **need** (`agents.md`, 7.5) relaxed where the country is at war and the area
   borders no land held against it - halved then in its capital's area - and a **have**: its divisions whose sealift
   target, else where they are bound, else where they stand, lies there.
2. **Released**: a target in a real area out of the theatre, but a headquarters' in land the country may use; while an
   area has more than its need, the division bound there from farthest (outside the embark area first); a target that
   may no longer be shipped (below). The rest with a target are **listed for the sea**, their weight summed - each unit's
   the transport weight of the brigades that count towards it, in whole units cut on their own, so a worn-out brigade of no
   width weighs nothing and a headquarters at no organisation nought (*confirmed* on the running game, 2026-11-11: Italy's
   4a Armata waiting alone at the Metropolitana's embark entering July 1941 weighs nought, and no transport is sent for it;
   `FINDINGS-liveread.md`, section 50). The boarding and sailing use the brigades' whole weight. A division
   with no target standing or bound in the country's own area outside the theatre is **stranded**.
3. **May a unit be shipped to an area** (`CAIUnit_MayShipUnitToArea`): never within its own block of land - so a target
   is never inside one piece of the side's land, and is dropped once the unit is there; never to a portless area it
   cannot walk to - **the area it is bound for or stands in** being the one asked whether it is landlocked or portless,
   and the one asked about the one it must reach on foot; not one bound for or standing in enemy land, holding ground,
   falling back, or that the may-leave test does not let go strictly with a unit defending a battle held (7.6,
   `FINDINGS-moving.md` A.4e); not into an enemy's area a headquarters or a garrison; not armour or
   fast troops to a friendly area with no neighbour but the capital's, or from an area bordering an enemy; and then only
   where the sea between them is open - **a unit standing in no area, at sea aboard, has no port to sail from, so at war
   never** (the original's null area; seen on the running game: Britain's 1st 'London' Division, aboard for Lerwick and at
   sea at 09:00 on 7 September 1939, refused and its target released, so Lerwick is filled again;
   `reversing/findings/FINDINGS-liveread.md`, section 89) (`CCountry_IsSeaRouteOpen`: at peace always; at war shut into or out of the
   Mediterranean where Gibraltar and Suez are both held by enemies, into or out of the Black Sea where its blocker is).
4. **The short areas**, the country's own in descending order of shortage - one with the head's shortage and more
   provinces than the head's area put in front - others' before them in ascending order: each
   wants its shortage, or the stranded where they outnumber it in an area bordering hostile land or the theatre's
   largest. Its **target**: a landing province for an enemy's area - near the objectives in it, a naval base and its
   holder's provinces beside it, fewer than six enemies, 1000 worse reached only over straits, 34 an enemy - or else the
   area's port nearest the objectives by sea. While its want is at least the next area's shortage - or any, for an enemy's
   - the best candidate goes: a division with no target, in a real area, that may be shipped, not airborne with air
   transports, **no enemy area beside its own touching the target area too**, from an area with more than its need, scored
   by that surplus, +10000 stranded, + its fitness for an enemy's area, + its garrison rating (or 101 less it) by which of
   the two areas border hostile land, + twice its rating for an area with no neighbour, +100 where its commander is bound
   there, **+50 for each other land unit under that commander anchored there**, x2 a garrison or x0.25 where it is
   anchored - bound, or standing - in the area of operations, where its own area borders an enemy; **all in single
   precision, the garrison rating in its own** (`CUnitRatings.GarrisonExact`, as the garrison pick's), so two divisions a
   ten-thousandth of a share apart score apart - the 2a Divisione Eritrea's 69.0068 over the 1a's 69.0045 for the
   Dodecanese, as the running game logged them (`reversing/findings/FINDINGS-liveread.md`, section 61). (The order of the areas, the enemy-area rule and the siblings were
   missing until 2026-11-12, `reversing/findings/FINDINGS-liveread.md`, section 59: in a new game's second hour Italy's
   targets were seven divisions for Somaliland, where the original's are 23 Marzo for Rodos and an Eritrean division for
   the Dodecanese, and Sardinia and Dalmatia's two divisions swapped.) It is given the target and marched where it can walk, else listed for the sea.
5. **Headquarters** whose land children stand mostly in another area are given that area's port, and not moved. **One standing in no area - aboard at sea - has no area of its own**, so any area its children stand in will do:
   Canada's 4th Corps, at sea in July 1941 with its target released by the keep-or-release (3), is given its port again in the
   same pass, and its divisions stay under it, as in the original (`reversing/findings/FINDINGS-liveread.md`, section 92).
6. **Embark and debark**: cleared where only one is set or nobody listed for debark is **bound for, or stands bound nowhere
   in,** embark's area (the original's tally asks the destination; section 43). With no
   debark, troops listed and transports: each listed unit votes for its target - 1, 4 from an area with no need, +10 for
   land not the country's; the best not tried is the **debark**, **a tie going to the first in the walk of the original's
   hash map** - eight buckets by its runtime's hash of the province, a target whose bucket is empty put at the end, any
   other into its bucket's run in ascending order, a target tried keeping its place (`CStdHashMap`;
   `reversing/findings/FINDINGS-liveread.md`, section 62) - and the **embark** the port nearest by sea to those
   bound for it in the area where most of them stand, with a way by sea to it - the area's provinces walked in their order,
   each with a naval base built, the first of equals kept (section 43). With both: those standing at embark are
   boarded, the others in its area marched there; transports - neither bound for embark nor there already, not busy (a
   fleet whose order is a busy one and which is under way or held by it; **one on a plain way, home from a change of base,
   is sent**: France's at sea for Bordeaux, sent to Alger on 2 January in both of the original's runs; section 84), fit,
   the nearest by ((1 - strength) + (1 - organisation)) x 2 + 1 times the distance by sea - are sent there until they can carry the
   troops, each by the AI's own move (section 10), so its destination is written at once and the naval body after it in
   the pass finds the fleet under way and does not split it (`reversing/findings/FINDINGS-liveread.md`, section 57: New
   Zealand's five transports sent to 7990 in a new game's second hour, kept whole).
   **The sailing** (`CAIUnit_SailTransportOrWait`): a transport at embark carrying troops - or taking them aboard in the
   same pass, counted aboard - sails them to debark when everyone bound for debark in embark's area is aboard; with one
   still to come that the transports have room for, only where the fleet may stand in debark and the nearest such is more
   than 50 hours off; with none that fits, where it may stand in debark. It sails under a transport order, an invasion
   where debark is enemy-held. *Ours*: the original asks it as each unit boards (`CAIUnit` slot 69, from
   `CNavy::AddCarried`, which also clears the unit's held-back and garrison marks) and its order runs in the boarding's
   hour; here it is asked as the AI posts the load, so the order goes out right behind it, the drain here running a command
   posted by another command only at the next.
   **On arrival** (`CAIUnit` slot 71, from `CUnit::AdvanceMovement`): as a land unit with a sealift target steps into a
   province, its agent is told at once - where it now stands in its target's area, the areas are looked at again the next
   hour; where its way is done, not aboard, at the plan's embark port, it boards the first transport of the agent's lying
   there that can take it, and the sailing is asked. Britain's 1st 'London' Division reaches Newcastle at 06:00 on 7
   September 1939 and is aboard the 4th Navy in that hour, which sails at 07:00 - both as the original's
   (`reversing/findings/FINDINGS-liveread.md`, section 89). Ours had boarded only at the agent's next daily pass.

## 16. Invasions (`FINDINGS-moving.md` E, `FINDINGS-invasion.md`)

**When**: every hour, every invasion of every country has its hour and finished ones are dropped; on the 1st of each
month at hour 1 the failed areas are forgotten; at a country's hour, for a country the AI runs, the areas are ranked and
the invasions planned.

**The ranking** - only where the country's units can carry four infantry brigades: the coastal areas of the countries at
war with it (Germany, with its capital's area bordering land held against it, only Norway's, Sweden's, Finland's and
Denmark's; in two real factions never Finland's, nor the capital areas of Romania, Bulgaria and Yugoslavia). **Failed for
the month**: an area touching the country's own coast, one it may not invade (below), one no port of which reaches a port
of the country's or - its capital's area bordering no hostile land - its fellow fighters' under 5000 by sea (2000 with
it bordering) by an open sea, **a fellow fighter's port counting only where one of the country's own ports reaches it
under that range, or it is in Britain's capital area** (`FINDINGS-moving.md`, E, step 5) - built 2026-11-16: until then
any fellow fighter's port served, and Italy, its ports and Germany's to reach from, scored 24 areas at 08:00 on 16 July
1941 where the original scores 5, Gibraltar's, Malta's, another of Britain's and two of Greece's, the same five now
(`reversing/findings/FINDINGS-liveread.md`, section 105). **Touching it** is its own side's coast beside it, and also - the candidate being its holder's capital
area - a neighbour held by a friend at war with that holder, not played, of its faction where both have one, not in its
capital's group of areas, its subject or (not Japan) nobody's subject and not Japan, Poland or Denmark: so Germany's
capital area, beside France's, is failed for Britain. Each such neighbour, and its own beside it but the capital's, is a
**staging area**, gathered before the failed list is looked at. **Score**: 100001 with a priority province; a small island (ports, under 5 provinces, no
neighbour) 10 a port level + 51, or -99 for Germany before 1944; an enemy's capital area its provinces + 1, at most 101,
halved for a subject's (the half kept); else 1; +900 for Germany's capital area with the Soviet Union owning land and past
300 of surrender or under 90 manpower and the country a great power or of more than 100 IC; for the United States against
Japan +200 unless that holds, and +600 more for an area of under 5 provinces with no neighbour; for Japan against the
United States +600 for such an area, against the Netherlands +200 and the Philippines +100 - plus, for Britain and the
United States, 120 for each of Gibraltar's and Suez's blocker provinces in it; plus, over its first 60 provinces, 8 a
whole naval base level, 40 for victory points, 30 for one yielding crude oil, metal, energy or rare materials now, 40 for a
strategic resource, 5 for one the country owns, **shared by the provinces walked in whole twenties only where those are
more than one** (an area of 21 to 39 provinces not at all, one of 60 or more by 3); plus a draw on the AI's generator below
a fifth of it rounded half to even (at least 5), the remainder signed; over its least port distance to the capital's area /
200 as a whole number; in single precision, rounded half to even at the end (`reversing/findings/FINDINGS-liveread.md`,
section 100); kept by descending score, an equal after its equals. The ten best. Then the **staging areas**: the areas of its priority provinces held by a country on its side or
fighting beside it and not its enemy added; each not failed, its sea route from the capital's area open (else failed), one
of its own only with 40 provinces of enemy-held land beside it (25 where one is a capital area), scored 500 - 10500 for a
priority area - and a draw on the AI's generator below a fifth of that, less 5 a province, doubled for its holder's capital
area, in single precision rounded half to even: France's area, 309 provinces, -1926 in the original and -1938 here at the
war's start (`reversing/findings/FINDINGS-liveread.md`, section 89). Last, from the tail while more than one is left, those
under a tenth of the head's score dropped.

**May invade** (`CEU3AI_MayInvadeArea`): its own land always; a faction member not its leader not under 50 IC nor
without transports; and a member of the faction keyed `allies` not the capital area of a great power of a real faction
other than Japan - until a neighbour of it fights it.

**Planning**, while the country has fewer than 2 invasions (Japan 8, the United States 4), prepares no war, and some
ranked area is not invaded: the **divisions** are those of two brigades or more, ashore, ready (organisation 0.8,
strength 0.75), not garrisons or airborne, in an area with a port that is the capital's or in land bordering no enemy,
and spare - more there than the area needs, the capital's need halved where it borders no hostile land and is not well
ported (`COwnerArea_IsWellPorted`: its naval bases' working levels over its provinces more than half a level a province) -
or marines; **the count and the need are kept for the area of the unit before alone**, made again by this unit's agent
whenever the area changes, and those taken from the area so far are counted off each time - so an area met again after
another is worked out again, by the agent its next division answers to (`reversing/findings/FINDINGS-liveread.md`,
section 100);
the **fleets** those carrying nothing; the **lift** their carrying. For each ranked area, best first, while the lift
holds four infantry brigades: dominions skip every area before 1942 and Germany London's before 1943; N = its **enemy
strength** (below), at least 1, tripled at a great power's capital - passed over where N brigades' weight is over twice the lift or
fewer divisions are free; the **landing** (below); the divisions nearest by sea - marines at half the distance, fast
troops at twice, no armour or garrisons, none bound there already, none past what is left of the lift - while fewer than
N and their supplies under what the landing's ports land a day and 2; with twice the lift's weight the area fails; with
fewer than N, nothing. **A friend's area** - or, for Britain and its dominions up to 1941, one holding Paris, Brugge or
Amsterdam, whoever holds it - is **reinforced**: N is one and the enemy strength of each enemy area beside it, not held to 10,
less one for each of the country's land units not a headquarters already in its group of areas, each it lent the holder
there and each of its invasions' bound there, at most 5 for those three areas and 20 otherwise, their supplies what the
force's start from; it lands at its port nearest the country's true capital, the distance times 3000 over the working
naval base level, its capacity the area's ports'; armour and garrisons may go; nothing is tripled, no area failed for
weight, one division enough. Britain at the war's start sends the 1st Royal Marines alone from Pembroke to Newcastle for
Cherbourg, as the original does (`reversing/findings/FINDINGS-liveread.md`, section 89). Then transports until they carry them and escorts until their power is half their weight - each
fit, the nearest of each a round, an escort with a submarine a hundred times as far, a carrier a third, a capital ship a
half. With a transport and room for the smallest division, the **invasion** is made with them all.

**An area's enemy strength** (`CountEnemyUnitsInArea`, `CAreaEnemies`) is brigades, not units: the brigades of the units
there at war with the country (or rebels) that can fight - a headquarters' brigade cannot - in thirds, at least 1 where
there are any; less a quarter, rounded half to even, of the brigades of the country's friends fighting the area's holder,
there and in the areas beside it held by the holder's enemies; **twice the thirds instead, with nothing taken off, in an
area of under 4 provinces and one port**; a figure above nought at least 5 where the area is its holder's capital's and
the thirds reach 5, at least 1 otherwise; nought at the least, and 10 at the most unless the caller asks otherwise. The
Soviet Union on 17 July 1941: Bornholm 2, Tromsø's area 1 and Hammerfest's 1, each holding a German division beside
headquarters (`reversing/findings/FINDINGS-liveread.md`, section 100). The same figure is an enemy-held area's need
(`agents.md`, 7.5).

**The landing** (`CEU3AI::PickInvasionLandingProvince`): of the area's provinces, the least score among those with a naval
base, beside real sea not the Black Sea's or the Baltic's, held lightly enough (or near a priority province) where the
area has more than one province, its lead transport able to enter a sea beside it, and reached by an open sea from a
port of the capital's area: the mean distance to the area's priority provinces (a tenth where it is one), less 10 a unit
of its port's day, less half the worth of the enemy land beside it on the same sea, less 1 a neighbour, less 20 x (2 -
its defence), +10000 reached only over straits, + 34 x its defence, less 2 a victory point, less 20 for a capital, +1000
where another invasion lands; in Germany's capital area, for a great power with the Soviet Union weak, doubled in its
favour, and France's provinces a quarter more. **Its defence**: the enemy's brigades there by strength, a headquarters
brigade a third, raised a fifth for each coastal fort level.

**An invasion's hour**: **over** once its fleets or troops are gone, every division can walk to the objective, its fleets
cannot carry the smallest, the objective is in nobody's hands it may enter, or its troops landing are worn under a third;
**the embarkation port** while it has none or a stranger holds it: of the side's ports reaching the objective by an open
sea within 2000 (1000 with the capital's area bordering hostile land), the least (1.5 - its level / 10) x 10 on a front x
0.3 the country's own x 0.75 the capital's area, x 2 or 4 where its area borders hostile land, x the distance, x 3 where
the troops stand nowhere in that area - none, and it is over. Then **the troops** ashore march to the rendezvous; **the
empty fleets** sail there and merge - **one under an order first has it taken away, its way kept, and sails an hour
later**, the order's taking writing the rendezvous as where the AI sends it so that the sailing passes it over that hour
(`reversing/findings/FINDINGS-liveread.md`, section 139: the Soviet Cernoje Flote, on a patrol, taken for an invasion at
04:00 on 18 July 1941, sails for 1218 at 06:00); the one fleet, there and idle, **loads** the troops standing there and waits for
those still coming; then sails to the port, and there is given the **invasion** on the landing picked again - none left,
and the area fails for the month.

**Given up** when its area is neither ranked nor beside a ranked one: its fleets sent home, troops aboard a fleet in port
put off.

## 17. The air (`CAIUnit_ManageAirUnits`, `FINDINGS-reorganise.md`, `FINDINGS-airnaval4.md`, `FINDINGS-airstance.md`)

Every hour, for every agent with air units, in four phases; the third is the regrouping (`agents.md`, section 5.3).
**Full** is the agent's full pass (`agents.md`, section 3). Every figure here is **exe-hardcoded**; a mod reaches the
unit files' types by key (`interceptor`, `cas`, `cag`, `tactical_bomber`, `naval_bomber`, `strategic_bomber`,
`transport_plane`), every wing's `range`, `sea_attack` and `strategic_attack`, the air base buildings, the weather's air
defines (`AIR*` in `defines.lua`, which make the weather penalty) and the map's latitude.

### 17.1 The air base lists (`CAIUnit::BuildAirTargetProvinceLists`)

On every full pass, and on the first pass after a replan, four lists are made of the **candidate bases** - the provinces
that hold a built air base: the country's own in the agent's theatre, and its overlord's and its puppets' wherever they lie
(`FINDINGS-liveread.md`, section 27) - each
base scored by a **mean distance**, lowest first, a base of equal score after those already there:

| list | for | the mean distance to |
| --- | --- | --- |
| default | anything else | the area of operations with the frontier at war, else the enemy's bases - the bases of each country at war with it in the area of that country's acting capital; with any invasion targets, 0.75 of the mean to them and 0.25 of that |
| strategic | a strategic bomber, or a first wing with strategic attack over 50 | the enemy's war industry handed to the agent - none here (section 20) |
| naval | a naval bomber | where the enemy's fleets handed to the agent stood at the last count of the aircraft wanted |
| transport | a transport plane | the invasion targets, and only while the agent has airborne divisions - never here |

The mean is over map distances (four to a pixel), **doubled where the base is itself one of them**; with nothing to
measure, the mean distance to the plan's objectives; with no objectives, 10000. Single precision. The **invasion
targets** are the provinces of every fleet of the country under an invasion order, made as the subordinates are sorted.

### 17.2 Triage (phase 1)

Each group, not retreating:

1. **Joining** a partner that still exists: it and the partner are **taken** (passed over by phases 3 and 4); one whose
   partner is gone has its order dropped and goes on.
2. **In no war**, declared or undeclared, **or with no base**: a **candidate**, whatever its state - the steps below are a
   country at war's (*measured* in a new game's first days, `reversing/findings/FINDINGS-liveread.md`, section 56: Japan's
   bombers, landed at half their organisation, merge at its next full pass; Italy's Aeronautica AOI, tired in the war on
   Ethiopia, rests and merges with nobody).
3. **At war, its airfield marched on** - an enemy land unit beside it whose way ends in it (section 94 of the same): phase 2 is forced; a
   busy group not rebasing has its order dropped; it is a **candidate** whatever its state.
4. **Unfit** - a wing under 0.5 of its strength or of its organisation at air stance 2, else 0.7: under any order it is
   called off - a carrier's group of only CAG put on **carrier protection**, any other's order dropped. **Wanting rest** - a
   wing under 0.75 of either at air stance 2, else 0.85: with no order, at its base, it rests. Both bars halved for a
   carrier's or an interceptor group (`FINDINGS-liveread.md`, section 27).
5. On the move, under an order or fighting: passed over. Otherwise a **candidate**.

### 17.3 Rebasing (phase 2)

On a full pass, or forced: each candidate not taken and holding no CAG takes its role's list (17.1) and starts at its
own base with a score of 1,000,000; for each listed base no enemy marches on, the score is the listed one -
**averaged with the distance from the headquarters to the base** when the group's parent is anything but a theatre -
and, but for a transport plane, **times the crowding** where it is over 1: (the wings sent there this pass + the wings of
every air unit based there, nested under a headquarters or not, **but a carrier's air group**, which is on its carrier's
list and not the airfield's - Britain's at Scapa Flow had crowded it for its first new-game pass, sending its bombers to
1521 where the running game sends them to 1127, `reversing/findings/FINDINGS-liveread.md`, section 66 - + the group's own
unless it is based there) / the base's working level. A base scoring lower
than the best so far is the best. A best that is not its base: **rebase** there, the count of wings sent there and taken
from its old base kept for the rest of the pass, and the group taken.

### 17.4 Missions (phase 4)

Nothing while a regrouping is pending after phase 3, at peace on a pass not full, or at air stance 0 (the AI keeps every
agent at 2, `agents.md`, section 7.2). Each candidate not taken, out of battle, **at its own base** and not busy goes to one
pool, by the wings it holds, the first wing's figures standing for the group's:

1. any tactical bomber, CAS or CAG: with a **naval budget** left - four for each province threatened from the sea
   (section 6) - and sea attack above nought, **naval strike** (one off the budget); else **close support** where the area
   of operations has a province, at peace, below a strategic attack of 1, or with any CAG; else **strategic**;
2. any strategic bomber, or strategic attack over 50: **strategic**;
3. any naval bomber: **naval strike**;
4. any interceptor: **intercept**;
5. else none - a group of multi-role wings alone flies nothing.

Every order is given at the **defensive** stance (`stance=1` in the save's), flying day and night. A province is **in
reach** when the group's range is at least its distance from the group's base; **the weather** over it is the air's
weather penalty (`aircombat.md`); **seen** is an intelligence of 2 or more (`intelligence.md`). **Till sunset**: the hour
the game's clock stands at, set against 12 + q, q = (latitude x the month's figure / 1000 + 12000) / 2000 in whole hours
over the group's base, the month's figure being the night's table (`landcombat.md`, section 4; south of the equator six
months on) - at least three hours left, and the order ends then.

- **Close support** (`ground_attack`), till sunset: the first of the **invasion targets** where an enemy land unit stands,
  in weather to 0.400 and in reach; else the first **battle on the front** - a province of the agent's front (section 4)
  it sees, with a land unit in a battle - in weather to 0.330, over which at most one of the agent's groups
  already flies close support, in reach; **a battle so chosen is not chosen again this pass**.
- **Intercept** (`air_intercept`), at war, for 120 hours: every province in reach, in order, that the country may enter,
  sees and that is not an enemy's air base; the first is the order's province. No weather test.
- At air stance 2 only:
  - **Logistical strike**, for 24 hours, while the plan's power ratio - its own power x 1000 / the enemy's, 1000 with the
    enemy's below nought and -1 at nought - is **below 750**: the first in reach and in weather to 0.330 of the provinces
    one step behind the front - land beside an enemy-held front province, held by an enemy, with infrastructure of 0.2
    or more, touching none of its holder's enemies by land.
  - **Strategic bombing** at the enemy's war industry: none handed here (section 20).
  - **Naval strike**, till sunset: over the seas of the enemy fleets handed to the agent at sea - and the sea each sails
    into next - in weather to 0.330, **every one in reach, in that order and repeats and all, is the mission's**, the first
    its province; with none, a **port strike** over the harbours of those in port the same way (`reversing/findings/
    FINDINGS-liveread.md`, section 129: the Italian 1st Air wing of 18 July 1941 strikes 10571 and moves on to 11456,
    where until then its mission held 10571 alone and it turned for home).

## 18. Kept as the original does them

- **Only one of line, garrisons and reserve moves anything in a pass.**
- **The attack takes the cheapest unit first**, and a garrison's +100 puts it last.
- **A called-off attack halts where it is**; nothing sends it back.
- **The terrain figure's neighbour term re-reads the province's own terrain.**
- **A garrison is rated higher the weaker it is.**
- **A unit nobody has attacked counts no toughness in the odds**, its defence products being nought until it fights;
  **and one whose battle is over counts its last hour's**, which nothing clears.
- **A province of 500 or more is attacked as at stance 4.**
- **The air's sunset ignores longitude** (17.4): the hour of the game's clock is set against a sunset worked from latitude
  alone, so the AI's daylight missions keep the same hours from Lisbon to Vladivostok.
- **A group of multi-role wings alone joins no pool** (17.4) and flies nothing.
- **The transport list is made only when the agent has airborne divisions and read only when it has none** (17.1, 17.3),
  so a transport plane is never rebased.
- **A group rebased in a pass is no partner for a merge in it** (17.3; `agents.md`, 5.3).

## 19. Ours

- **The split's blocks go in the order their region groups were made** (7.2, step 5): the original keeps them in a map
  ordered by the groups' addresses, which the heap decides - on one run 132 of 216 neighbouring groups of its list lie in
  rising order - and two of its runs of one save differ on Trondheim HQ's blocks of 17 July 1941, one sending the
  3. Infanterie-Division to Oslo and one not (`reversing/findings/FINDINGS-liveread.md`, section 130). The order of making
  agrees with the running game's addresses on that pair; nothing here can repeat them in general (`enhanced.md`).
- **The ways** (section 10): the safe and very safe finders weigh a fleet or an air unit otherwise, which is not built:
  neither holds them back.
- **The target areas' two steps** (section 4) are taken through the areas' neighbours, enemy-held after the first; the
  original's area path finder and its two cost functions are not read.
- **The transport reservation** (section 2) is asked of a unit with a sealift target only; no
  paradrop is built.
- **Committing** (section 2) leaves out the reading's "cannot reach the area" test, which was not read.
- **The regions marked to be held** (section 3, +100 each) are none: the region's mark was not read.
- **The threatened provinces** (section 6): the warning the original gives as a landing's way is set is taken in the hour.
- **The axis** (11.6): the original also asks a test of each pair that was not read; here the nearest pair is
  taken, and with no way between them the path is empty.
- **The fleets** (section 14): a patrol's centre is drawn on a C runtime `rand()` of its own, made afresh for each
  patrol phase and seeded 1 as a new thread's is - until 2026-11-16 on the AI's generator, which took a draw the
  original never takes there and moved every later AI draw of that country, the invasion ranking's among them (seen
  with the original's draws played back, `docs/replay.md`, section 9). The original draws on the C
  runtime's `rand` of whichever worker thread ran the country, which nothing here could repeat - a fresh worker's stream
  starting at 41, 18467, 6334, the main thread's carrying on from every draw since the process began (*measured* on the
  running game, 2026-11-12, entering July 1941: Germany, Britain, the Soviet Union and the United States drew on the main
  thread, Britain's Middle East Command 1229 for 10582 and Moskva HQ 20857 for 10508, behind the closed Baltic strait, so
  the Baltic Fleet stands; `FINDINGS-liveread.md`, section 53). A playback of a run recorded with the patrols' draws hands
  them over (`docs/replay.md`, section 9). An agent with no fleet stops before phase 1, as the original's does, so it
  makes no patrol draw (`FINDINGS-liveread.md`, section 112); a fleet leaving a losing
  sea battle is sent home, the reading saying only that it disengages; an empty transport's order is always cancelled,
  the flag that spares one not being read.
- **The sealift** (section 15): the original's steps after boarding - unloading, merging and sailing a transport - are named
  by their calls only, so here a fleet at embark carrying troops, or taking them aboard in the same pass, with nobody
  left waiting there unboarded, is given the transport mission to debark - boarded and sent in one pass, as the running
  game's Ceylon Command was on 3 January at 09:00, where ours waited a pass for the troops to be aboard and sailed a day
  late (`reversing/findings/FINDINGS-liveread.md`, section 63) - the mission held to no range, as the AI's orders are
  not (section 84, where the original's step is read in part: it sails at once with everyone bound for debark aboard,
  and waits while another could still fit and is near); a division's fitness for an enemy's area is its strength share, the original weighing it by its
  type as well; the embark port's way to debark is any distance by sea; a tie in the embark area, which the
  original breaks by a hash map's order of areas by their address, goes to the first found; and a unit trespassing in another country's land - a
  flag nothing here holds - is not looked for.
- **Invasions** (section 16): a country preparing any war plans none, where the original lets a great power invade a
  lesser one it prepares war on a month after the plan; an invasion is over where the objective is held by someone the
  country is neither at war with nor fighting beside, the original asking whether it may enter it; the empty fleets
  merge by the join command; and the ranges and the port radius are in the map's own units throughout.
- ~~**The raided waters** (section 12, step 5) are the sea provinces on the side's convoys' ways whose last raid was within
  a month: the list of finished battles here keeps a week and not whose convoy a raid was on.~~ Built as the original's
  on 2026-11-16, from the list of finished battles, which keeps each raid's defender (section 12, step 5).
- **The hold list** (section 5) holds a province once.
- ~~**The second ring's draw** (7.3) is the game's own generator seeded with the unit's number; the original seeds an
  MT19937 the same way, its seeding not read.~~ Read on 2026-11-14: the game's own seeding and twist, seeded with the unit's
  serial (`reversing/findings/FINDINGS-liveread.md`, section 91).
- ~~**A support attack ends** once the province is no longer held against the unit, nobody stands there to fight, or its
  way is gone: what ends the original's order was not read.~~ Read on 2026-11-04 and built as the original's
  (`landorders.md`, section 7).
- **The odds** (11.4): the technologies' free stacking (`unit_cooperation`) is taken off since 2026-11-12 (section 11.4), and
  encirclement is weighed since 2026-11-10 (`landcombat.md`, section 4). The country's difficulty is weighed since 2026-11-06 (`frontend.md`, section 4.3).
- **The air** (section 17): a "busy" group, which the original asks of its order, is one under any order; a group the original
  gives the order "none" at its base has its order dropped; the places enemy fleets were seen are made again at each
  count of the aircraft wanted, where the original pushes onto them with nothing found clearing them; every enemy's bases
  count, a flag the original asks of each not being read; a battle on the front is a land unit there
  in a battle, the original also asking that a figure of the unit's battle be nought, which was not read; a country's air bases are the
  provinces it holds with one built; and the wings based at a base are counted over every country's air units.
- **A move with no way** (section 10): the AI posts nothing for a unit that has no way to where it is sent, and the move
  counts as none - so a step that only tried an impossible move does not stand in for one that moves. The original's
  `MoveUnit` posts its command whatever; the reach tests the reading has (7.3, steps 6 and 7) are asked as well.
- **The march check** (section 9) passes over a unit the same pass has just sent somewhere else. The original's later
  steps see the AI's destination at once while its posted command has not yet run; the march check reads the old way,
  so a halt it posts would follow the new march, leave the unit where it stands and keep it marked retreating - for good,
  as only arriving clears the mark. Whether the original reaches that state was not settled (`FINDINGS-moving.md` A.4b).
- **The areas a plan keeps** - its theatre's areas and fronts, an agent's focus areas, the areas its attack failed in or
  ranked - are made again whenever the supply network is (`supply.md`; at any change of hands, and daily), and each kept
  area is carried over to the new area holding its first province under the same holder, or dropped. How the original's
  areas outlive a change of hands was not read; kept as they were, they went stale and a block's positions with them.
- ~~**Where a unit is bound** (section 10): the original's reader answers nowhere for a unit under a support attack; ours
  answers its way's end, the exception not yet set against the comparisons.~~ Built 2026-11-15 as the original's two
  readers; which one each of the original's other callers uses was not looked up, and those ask the pending one here.
- **The pull** (7.3, step 7): two of its tests were not read - a destination the original takes from another helper,
  and a test of a division bound for its own depot - and are left out.

## 20. Not built

- ~~Paradrops (`CAIUnit_PlanParadrops`)~~: built since 2026-11-18 (section 9.1).
- The Allies' Baltic ban on patrols and intercepts (`CAIUnit_IsBalticBarredToAllies`), and the four provinces the raid
  passes over.
- Of invasions: the reinforcement of a friend's area by sea (the ranking's second list and the planner's friendly branch);
  the tallies of transports and brigades short it leaves for the production minister; the carrier aircraft sent over the
  beach; a landed division put under a theatre's headquarters; and the played country's invasions over its own units.
- The enemy's war industry handed to the agents whose air bases are nearest (section 12): so no strategic bombing or nuke
  mission (17.4; `nukes.md`, *Ours*), and the strategic list (17.1) measures against the objectives.
- The scenario game's rules (nine tenths of the garrisons to the line): there is one start.
- ~~A strategic redeployment's cost in supplies (`STRAT_REDEP_SUPPLY_MOD`)~~: built since 2026-11-18, twice the
  supplies and no fuel (`upkeep.md`, section 1). Its cost in organisation, `STRAT_REDEP_ORG_LOSS`, is built since
  2026-11-18 too (`organisation.md`, section 3).
- The human's delegated AI: the objectives' areas (11.1), the agentless divisions of the played country (7.4).
- ~~**A province lost**~~: built since 2026-11-18 (section 7.7).

## 21. Checked against the saves

`OpenHOI3.SaveCheck agents` (`agents.md`, section 14) also sets each division of a country nobody plays, found by its
number in both saves, beside the later save: where it stands, whether it has left its first place or is on its way as the
save's has, and - for one the save has moving - whether it is bound for the same province. A save keeps a moving unit's
way, so "on its way" is read off it: the original had **542 of 908 divisions moving by 4 January**, many not yet out of
their first province.

On 2026-10-19, `Germany1936_01_01_00` to `_01_04_12`: **moved or not 708 of 908** (about 366 with nothing moving),
**bound where the save's is 208 of 542**, standing where the save has them 725. What differs is which position a
division is sent to - the line's shares hang on the scores, the hold list and the balancing, each with its own gaps
(`agents.md`, section 14). Italy against Ethiopia: 35 of 49 divisions move or stay as the original's, 14 of 31 bound for
the same province; the attack takes Ethiopian provinces, eight lost by July. ~~Where the original's Italy has taken none
of the 21 by 20 April~~ - that save is the outlier, by an account of the original: in the Ireland game's save of 2 February
1936 the original's Italy holds 8 of them.

With the whole piece built, the same run: **the axis 106 of 106** (Italy's way to Addis Abeba among them), **embark and
debark 93 of 106** - nine of the save's twelve sealifts have one here, five with its embark - moved or not 710, bound 212.
**To 20 April** (`_04_20_03`, 2619 hours, 132 s): moved or not 651 of 879, bound 101 of 572, standing where the save has
them 258 - what differs grows over months, every choice resting on positions already different; the plans' stances 103 of
106, areas 91, the axis 105, fleets 221 to the save's 205.

~~**The fleets move too much**: 242 against the save's 222 on 4 January (211 before the naval body). Italy, at war, keeps
28 to the save's 18 - a fleet on patrol is never merged, and 21 without the patrols - and Britain at peace 41 to 36;
which of the naval body's steps keeps fleets from merging that the original merges is not found.~~ Found on 2026-10-20:
the ports spread a fleet just ordered to join, or the one it joins, and the rebase took the place of the join - see *A
war that never ended* below.

**The air** (section 17): the check also sets each air unit of a country nobody plays, found by its number, beside the
save - where it stands, its mission and, where both have the same, the mission's province, stance, end and provinces in
order. To 4 January: **air units where the save has them 51 of 57** - before the rebasing nearly all stood on their
first fields, where the original had moved Italy's to 3630, 3692 and 10004 - and **the mission 57 of 57**: ~~the save's one
AI air order, an Italian intercept given at 05:00 on 1 January, is ours province for province, all 36, at stance 1 and
ending at 05:00 on the 6th~~ - a coincidence of the AI running twice in the first hour, which merged two of Italy's three
air groups away; see *Units that never stopped moving* below for the figures since. The count of air units per country goes from 99 to 96 of 101: Japan keeps 9 groups to the
save's 6, a group rebased being no partner for a merge in that pass (section 18), and too worn by the flight to be fit to
merge for days after - measured on 2026-10-20 with the trace's daily air line: every one of Japan's eleven land-based
single wings is rebased on its agent's first full pass, before any merge, and lands at about 0.56 of its organisation,
under the 0.75 the offensive air stance asks; the four at 4986 are fit and merge on 7 January, the five at 4691 later.
**The original merges first**: its merged 3. Nihon Koukuujieitai stands at 4691 on 4 January at about 0.62 - worn by
one flight, not by two - and 5. Nihon Koukuujieitai and the 3 Zerosen no Hikodan, which others joined, never left 5020.
Which of the first hours' passes merges before the first rebase is not found; it wants a reading of those hours. **Read on
2026-11-12** (`reversing/findings/FINDINGS-liveread.md`, section 56), a new game logged hour by hour beside its own saves:
the original too rebases first and lands its wings at half their organisation, and merges anyway - at peace the triage
lists every group, worn or not, and the orphans attached at the second hour set their agent's **regroup air pending**, so
the Zerosen merge at the third hour and the worn bombers at the full pass of the twelfth. With both built, the 4 January
check against that new game's own save of the hour gives air units 101 of 101 countries and the Italian intercepts' end
hours 3 of 3; against the reference save Italy too is the save's, and Japan's 7 to 6 and the intercepts' hour are that
game's own (the new game gives Japan's count exactly).
To 20 April: air units 98 of 101 countries, where they stand 41 of 53, the mission 52 of 53 - China flies an intercept
the save does not have.

**Four corrections a look in play found** (2026-10-19, after Germany's divisions were seen to stay in Gera and Leipzig
and an Italian division sent to Bender Beila). The check takes `player=TAG` - the Ireland game's saves have Germany run
by the AI - and `trace=TAG`, which writes each pass of one country's land block and attack; and it sets the land each
country holds of another beside the save's.

1. **The line took a position no way led to** - Germany's 11. Infanterie-Division sent from East Prussia across the
   Polish Corridor every pass, which counted as the line moving and so kept the garrisons and the reserve from ever
   running. The reading's "reachable" was not built (7.3, step 6).
2. **The split counted every garrison target**, where the record counts the first g: a block of Germany's wanted 14
   garrisons for a split of 5, leaving the line 4 divisions for 13 positions (7.2, step 5).
3. **The march check stood down every attacker**: the record has it look only at a way of more than one province, and
   not at a retreating unit (section 9).
4. **The line and the pull took divisions out of their attacks** every three hours and the attack sent them back, so no
   attack ever arrived: the listing has both pass over a division attacking a neighbour, which the reading had left out
   (7.3, steps 6 and 7; `FINDINGS-moving.md` A.4b, read for this). Divisions taken out of a battle were also left
   marked retreating for good (section 19, the march check).

With all four, to 4 January: **moved or not 763 of 908** (710 before), **bound 276 of 542** (211), standing where the save
has them 738 (723). To the Ireland game's 2 February save: Germany's divisions left standing where the original's moved
fall from 16 to 1. **Bender Beila is the original's too**: by 20 April its Italy has sent the 7a CCNN Cirene there, and the
province is in its African theatre's area; ours sent divisions there from the second ring when the line pulled them out
of an attack, which it no longer does. ~~Italy is still slower than the original: 4 Ethiopian provinces held on 2
February to its 8~~ - see the next paragraph. The figures against a save of February move by tens from one build to the
next, a few hundred divisions hanging on whether the Spanish Civil War's events fire.

**Attacks that stopped and started** (2026-10-19, a look at a game in play: Italian divisions in Ethiopia "joining
an attack, then stopping, then attacking again, over and over"). The trace (`trace=ITA`, which now also names what each
move and each halt came from) showed two causes, both departures from readings the fact base had (`FINDINGS-moving.md`
A.4c):

1. **The attack re-ordered its own attackers.** Its commit loop passes over every attacker bound for enemy land, the
   target included (11.2); ours exempted the target, and a move to a unit already attacking - which `MoveUnit` posts for a
   unit in a battle - takes it out of the battle (section 10). Every three hours each attacker left its battle and joined
   it again.
2. **The line chose positions only enemy land led to.** Its reach tests ask the safe way and its moves the very safe way
   (section 10); ours took the plain way, so a position past Ethiopian land was chosen, the march check cut the march at
   the first hostile province, and the next pass chose it again. The 26a Divisione 'Assietta' was re-ordered every three
   hours from 8 January; now it is ordered into 9846 once, takes it and goes on.

With both: halts in Italy's land block from 197 to 2 to 2 February, and **Italy holds 9 Ethiopian provinces on 2 February
to the original's 8**. To 4 January: moved or not 767 of 908, bound 273 of 542, standing where the save has them 738.

**A defender that never took its province back** (2026-10-19, seen in play: an Ethiopian unit sitting in a province
Italy held, taking it back only when another unit walked in). A unit takes the province it stands in each hour only while
it has no way, is in no battle and is not falling back (`landcombat.md`); these were marked falling back with no way left,
which nothing clears. Three ways the AI left them so, each against a reading:

1. **The pull took a division defending a position that wanted it** - the line had just sent it on, which marks a unit
   leaving a battle as falling back, and the pull sent it back to where it stood, which is a halt. The pull passes over a
   division in a battle whose position wants divisions (7.3, step 7): the "map not read" of A.4b is the line's own
   position map, read for this.
2. **`MoveUnit` moved a unit falling back** - its first gate refuses one (section 10).
3. **The attack took a defending division as a candidate** and its call-off then halted it: the original passes over a
   unit in a battle on either side (`IsHoldingGround`), ours only one attacking (11.2).

The check now counts each day the land units left marked falling back with no way: **0 to 2 February**, where there had
been four sitting in Italian-held land on that day.

**Units that never stopped moving** (2026-10-19, a reference game as Ireland: China's divisions all marching north
for some days, then south for a day, then north again; Italy's army and navy "always moving" at home). Not a rule that
lets a unit arrive before it is asked again - the original asks every pass, as here - but four faults of ours:

1. **China: the areas went stale.** Every change of hands makes the supply network again, with new areas, and a plan's
   theatre, fronts and focus areas still held the old ones - so a block's positions came and went with each network made,
   and its divisions were sent after them. The kept areas are now carried over (section 19). China's orders to 2
   February: 1006 to 46.
2. **Italy's army: the pull's fuller test was a guess.** Divisions were pulled from one position to fill another and
   pulled back the next pass; the test is read now (7.3, step 7; `FINDINGS-moving.md` A.4d). With it **the supply gate
   of a route** (`movement.md`, section 3): a division given no supplies is not routed onto an enemy's land, where ours
   had sent it and the march check halted it.
3. **Italy's navy: the AI ran twice in the first hour** - once as the new game's two days end and again as the session
   starts - and the second pass undid the first's joins and rebases. It runs once an hour now. **That moved Italy's air
   orders**: the doubled pass had merged two of its three air groups into the third, and the one left was given an
   intercept at 05:00 on 1 January, as the save's is. Now all three stand, as in the save, and each is given the save's
   intercept at the save's stance - **an hour before the save's**, 04:00 for the first two and 05:00 for the third where
   the original's are 05:00 and 06:00, and so without 10575, which the save's lists among 36. Italy's own full pass falls
   at 14:00, so these come from the hourly pass of a country at war; which of the first hours' steps the original takes
   an hour later is not found. Air units 59 of 59 by mission, where they stand 53 of 59.
4. **And a fleet's join was answered by its partner's**: the record's existing-orders phase (section 14, step 4) puts a
   fleet joining another, and the other, among those handled before the merge; ours did not, and pairs ordered each other.

Italy's fleet orders to 2 February: about 560 to 102, none over 14 a fleet; Italy still keeps 25 fleets to the save's
17 - ~~a fleet on patrol is never merged (section 18)~~ corrected in the next paragraph. To 4 January: **moved or not 774 of 908, bound 275 of 542, standing
where the save has them 740**, fleets 237 to the save's 222. To 2 February: moved or not 806 of 930, bound 249 of 601,
standing where the save has them 496, and Italy holds 7 Ethiopian provinces to the original's 8.

**A war that never ended** (2026-10-20, seen in play: Italy's navy "still looks wrong. In the original the AI puts
their navy back at base if it goes to back to peace"). Run to 20 April, Ethiopia capitulates on 30 March and Italy's
patrols went on for three weeks after. Three faults of ours, each against the record:

1. **Italy was at war with nobody and still at war.** A capitulation, like a peace, takes the loser off its side and
   leaves the war standing - the original's, which never ends a war whose side empties - and ours asked whether a country
   was on a side of any war, where the original asks its own flag, made from whether it is at war with any country. So
   Italy's patrols were never cancelled and its land block kept the three-hourly pace of war. "At war" is the flag's now,
   everywhere it is asked (`war.md`, section 1).
2. **The naval body ran every hour at peace**, where the original runs it at peace only on the agent's full pass or a
   replan of its fleets (section 14).
3. **A join was undone in the pass that ordered it.** The original takes the fleets a join was ordered for, and the fleets
   they join, off the ready list before it spreads fleets over the ports; ours spread them from every fleet, and a
   crowded base's rebase took the place of the join. Hourly passes at peace had half hidden it, merging what the next
   hour's join reached. This, not patrols, was why Italy kept 25 fleets to 17.

With all three, the day after the peace every patrol is called off and by 10 April every one of Italy's fleets lies in
its base, and nothing moves after. Fleets to 4 January **221 to the save's 222** (237 before), Italy's 20 to 18, Britain's
34 to 36 and Japan's the save's; to 2 February Italy's 18 to 17; to 20 April 211 to 205 and Italy's 17 to 14. Divisions to
4 January as before; to 2 February moved or not 810 of 930, bound 247 of 601.

**Ethiopia's surrender left it Italy's puppet**: the history's war (`history/wars/SecondItalyAbyssinianWar.txt`) gives
Italy the goal `casus_belli = puppet`, and a capitulation carries out the victor's goals (`war.md`, section 13) - the
original's rule. No event or decision of the base game annexes it afterwards.

**A headless run** (`headless.md`): to 1 July 1936 in 214 s with nothing thrown and no Lua error; the unit AI's pass
about 10 ms an hour, its hold lists and the day's balancing the dearest parts (`OpenHOI3.SaveCheck profile` lists them
under the pass).

**The Marco Polo Bridge a year late** (2026-11-02, the reference Ireland game played on to August 1938, Japan and
China both the original's AI). The original's Japan took `marco_polo_bridge_incident` between 1 July 01:00 and 2 July
16:00 1937 - the autosave of 1 July has no flag and no war, the save of 5 July both - and ours in July 1938. The decision
is posted when the prepared war is ready (`foreignminister.md`, section 7): at most 30% of the front facing China empty.
On 1 July 1937 the original's Japan held 5 of its 6 China front provinces with the divisions it started with there; ours
counted 2. `OpenHOI3.SaveCheck agents ... ready=JAP:CHI,CSX` set the two side by side, and three faults came out:

1. **A province's front was read off a cache.** The scores (section 3) triple a province on a narrow front, and ours read
   the front's length off the area, where the fronts are made only on the first asking - so an area nothing else had
   asked about since the network was made again looked to have no front and was tripled. Which provinces were tripled
   changed from day to day, the scores flipped between 10 and 30 and between 30 and 90, and each flip sent a division
   off a position that now wanted none: Japan's 15th Division left Jixian on 9 July 1936, and the 17th and 6th Miyun and
   Huailai after it. Read in full since, ours kept the China front manned to April 1937.
2. **That rotation cut organisation.** A division sent to a far corps (`agents.md`, section 4.2) lost its army's commander
   for a day, and its organisation fell to the bare ceiling (`organisation.md`, section 2) and took five days to climb back:
   on any day most of the front stood under the 99% the readiness test asks. With the front settled, every division on
   it is at its ceiling on 1 July 1937; whether a corps still rotates its divisions elsewhere was not looked at again.
3. **Japan built eight divisions too many**, and each that came in moved the `m mod n` bonus (section 3) and with it
   which positions wanted nobody: from April 1937 the front emptied again. The cause was the consumer goods demand, which
   left out the standing army (`production.md`, section 3).

With all three, on 1 July 1937 Japan holds **all six China front provinces**, five with the save's own divisions,
**74 divisions and 12 corps to the save's 74 and 12**, and is ready; divisions where the save has them 41 of 65, from 33.
To 4 January nothing changed - moved or not 774 of 908, bound 275 of 542, standing 740, fleets 221 to 222; to 2 February
moved or not 810 of 930 as before, bound 245 of 601 (247), standing 491 (496).

The check gained three switches for this: `ready=TAG:TARGET,...` - the readiness count, the front day by day for the
last 45 days, the country's corps and its brigades' organisation against the save's; `unit=NAME` - that land unit hour
by hour for the last eight days, with its ceiling, its commander's bonus and the chain above it; and `tracedays=N`, the
trace for the last N days only. The land block's trace now writes each position's score.

**A defender ordered out** (2026-11-03). China's army fell from 90 units to 54 by December 1937 where the original's kept
80 to 88, and 113 of its 395 retreats to the end of the war were the AI ordering a division out of a battle it was
defending rather than a battle lost. The question for the fact base was whether the original's AI does that at all. Its
answer (`FINDINGS-moving.md` A.4e): **it does** - no battle holds a land unit the line wants elsewhere - but the line,
the pull and the garrisons ask the may-leave test first (7.6), which ours did not have anywhere, and the line kept a
division where its position's standing were within the cap, not its have within its want. The sealift's test was read
again with it and had two misreadings, and the division picker gained the idle count it weighs candidates by. To 4
January: moved or not **777 of 908** (774), bound **298 of 542** (275), standing 747, fleets 221 to 222, the rest as
before. The rear (7.5) had left out its own two tests, falling back and holding ground, which the record had, and was
the largest single mover of China's defenders out of their battles - 80 of 194 such moves in 1937.

**And China lasts less, not more.** One run against another says little here: every country draws on one generator, so
a rule that moves one Italian division in January 1936 changes Ethiopia's fall, the Spanish war's start and China's own
production, and the two runs enter July 1937 with 86 and 77 Chinese units. So the rules were tried on a probe that
switched them on only on 1 July 1937 - the runs alike to the hour until then - against the build without them, to
September 1938, nobody playing: China's land units on the first of each month, 1937 July to December, 86 85 84 84 85 83
without and 86 85 82 78 73 68 with, and its surrender on 19 March 1938 without and **28 January 1938 with**; the
original's China keeps 80 to 88 and surrenders in mid-August. The AI moved China's defenders out of their battles 36
times from July 1937 with the rules - 30 of them the line's re-order of a division passing through to its next step
(A.4b) and 6 the fallback from losing defences - where the same rules with the rear's two tests still left out had moved
them 194 times (the build without was not traced). So **taking defenders out of their battles was what kept ours alive
longer, and is not the gap**: what is left is how its battles go. The trace
is `OpenHOI3.SaveCheck headless ... inspect=TAG`, which now also writes each move the AI posts for one of the country's units in a
battle, with the step that posted it.
