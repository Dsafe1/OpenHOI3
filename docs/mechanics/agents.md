# The unit agents

Stage 5's sixth piece, and **the military AI's second**: what the AI does to its order of battle and to each theatre's
battle plan. A country the AI runs has **one agent for each of its theatres** (`theatres.md`), bound to the theatre's
headquarters. Once a day each agent builds a ladder of commands under the headquarters - corps over divisions, armies over
corps, army groups over armies - and puts every unit under the nearest command with room; walks its headquarters to where
they are wanted; splits fleets and air groups that are too large and merges ones too small; and keeps the theatre's plan:
a stance, the AI's power beside the enemy's, objectives, an area of operations and the aircraft it wants. **Nothing here
moves a division towards an enemy or attacks**: that, the movers between theatres and invasions are the third piece.

Read out of the original in `reversing/findings/FINDINGS-agents.md` (sections A to E below follow its A to E), with
`reversing/findings/FINDINGS-power.md` for the powers and `reversing/findings/FINDINGS-reorganise.md` for the air caps.

**Mod-able or fixed.** Every count, threshold and weight below is **exe-hardcoded**. A mod reaches: the
`RADIO_*_LEADER_DISTANCE` defines and each type's `radio_strength`; the `hq_brigade` and `garrison_brigade` types, by key;
the unit files' figures that make a power (attacks, defences, toughness, softness, armour), `capital`, `transport`,
`is_sub`, `carrier_size`, `range`, `strategic_attack`, `transport_capability`; the technologies that make an aircraft
available; victory points, forts and infrastructure; the strategy's threat, antagonism and `conquer_prov`; and the texts
`ARMYGROUP_NAME`, `ARMY_NAME`, `CORPS_NAME`, `NAVY_NAME`, `AIR_NAME`, `REGIMENT_NAME` and the ordinals. Nothing reads Lua.

Distances are the map's (`theatres.md`, the head), and "an edge" one of a province's edges in their order, **an
impassable edge** one of kind 3. **A real area** is a supply area (`supply.md`, section 2): a province with one level of
infrastructure or less is in none. **A country's number** is its index + 1, as everywhere the AI staggers by it.

## 1. The agents (section A)

1. **One agent a theatre, and none below.** It is made when the theatre is made (`theatres.md`, section 5) - for every
   country, the played one among them - with **no unit**, its area the theatre, and **area dirty**; it is removed when
   the theatre is dropped, or left with no provinces by a refresh.
2. **Bound** to the theatre's headquarters by the country's AI pass (section 2), at the first hour it runs past the gate:
   every unbound agent whose theatre's headquarters exists takes it; an agent made by the pass for a theatre with none
   (2.5) is bound at once. Binding sets the headquarters' plan **active**, with `allow_hq_reorg` as it was, and runs the
   agent's subordinates (section 5) and its powers (7.3). **The played country's agents are never bound.**
3. **Unbound** - the plan switched off and reset (stance 2, air and naval stance 1, area, objectives and powers emptied) -
   when an agent is removed, or its unit stops being a theatre's headquarters (`theatres.md`, section 9.3).
4. **The orphans.** Once every agent of the country has posted its plan once (**plan posted**, 3.2), every hour the pass
   walks the country's units in their order: a theatre-level unit that is no theatre's headquarters loses its agent and
   is disbanded where nobody plays the country; **any other unit with no parent and no agent** is attached under the
   headquarters of the theatre its province is stamped with, where that theatre is the country's and its headquarters is
   above the unit; failing that, under the unit of the agent nearest it - the first of equals. Where the theatre is the
   country's and its headquarters is not above it, the unit is disbanded where nobody plays the country.

## 2. The pass (section A.2)

The AI's pass for a country runs **once as a game is entered - the original's first frame, before an hour is played -
and every hour after the hour's other work** - not during a new game's two days and first hour (`tick.md`, section 6).
What the entering pass posts is run before the first frame, so a new game's first screen has its units sorted (2.1). It
runs for every country with land or in exile:

1. Nothing for a country whose theatres are **marked** (`theatres.md`, section 1).
2. **The gate.** A country starts **waiting**. While waiting, its theatres are looked at: when every one has a
   headquarters (or it has none), it stops waiting and **its pass ends for that hour**; otherwise the pass goes on.
3. Every agent of the country ticks (section 3).
4. **The played country**: nothing more.
5. **Every other country**: agents are made for theatres that have none and bound at once (1.2); the unbound agents are
   bound and the orphans attached (1.2, 1.4); then at the country's hours:

   | hour | what |
   | --- | --- |
   | its number % 24 | once the pass has run once, the powers of every agent (7.3) and the divisions balanced (10.2); the allied-objective provinces (7.4); the objectives (7.4) |
   | (number + 16) % 24 | the air units moved between theatres (10.3) |
   | every hour | the stances (7.2) |

   And the pass has run once.

### 2.1 The sort (section G)

At the gate, as the country stops waiting - its first pass, and again a pass after a theatre is made or dropped for a
country nobody plays - **every unit is put under the theatre it stands in**:

1. Its units are walked with each one's subordinates before it, the history's in the order they were read, and each but a
   theatre's is placed: a land unit aboard a fleet where the fleet is going on the country's land, else at the fleet's base;
   a fleet carrying land units where it is going on the country's land; a unit with a base at its base, whoever holds it; a
   land unit where it is going or where it is, on the country's land; otherwise at the headquarters a search finds - the
   first of the country's theatres covering the province's area (its own country's area, or one of its focus areas) where
   the country may operate there, else the nearest headquarters, **each distance measured from the last nearest found**.
2. A unit placed in one of the country's theatres climbs its chain while each commander stands in the same theatre; where
   one stands elsewhere, the unit below it is listed; a unit at the top is listed itself; a climb ending at the top or at a
   theatre lists nothing.
3. Each listed unit, once, in the order found - but an air group holding a carrier's wing - is put under the headquarters
   of the theatre it stands in. Nothing marches, nothing is disbanded, and a command it empties stays.

## 3. An agent's hour (section A.5)

hour = the hour of the day; A0 = the country's number % 24, A3 = (number + 3) % 24, A6 = (number + 6) % 24, A9 = (number +
9) % 24. **The full pass** is hour A6, or **full pass pending** - set as an agent is made and whenever its area is set
(section 8), cleared when a full pass has run.

1. **Area dirty**, or hour 1 on a day of the year divisible by 7 (from 0, 1 January; 365 days): the area is set
   (section 8).
2. Hour A0, or **plan posted** clear: the plan is posted - for a bound agent of a country the AI runs, **plan posted** is
   set. (The original's axis, a path to the objectives at stance 3 or 4, is not built: section 12.)
3. Every hour: the command province (6.3); at hour A3, the commands (section 4).
4. **No unit: nothing more.**
5. Hour A9: the focus areas (8.3).
6. Every hour: the front coverage (8.4).
7. **Replan subordinates**, or hour A9, or the full pass: the subordinates (section 5) - which clears replan
   subordinates.
8. The full pass: the plan's aircraft (section 9); the headquarters pulled in (6.4); **full pass pending** cleared.
9. When this agent is the nearest agent at or above its unit - always, an agent being a theatre's: the air units
   regrouped (section 7.2 of the regrouping, here 5.3) on the full pass or while **regroup air pending**; the fleets
   regrouped (5.2) on the full pass or while **replan naval**, which is set as an agent is made and cleared by it.
10. Every hour: the headquarters marched to its command province (6.3).

## 4. Commands made and filled (section B)

Run at hour A3 by an agent with a unit and a theatre, of a country the AI runs.

### 4.1 Making commands

1. Nothing unless the country's manpower is at least the `hq_brigade` type's `build_cost_manpower`.
2. counts[k] = the agent's land units (5.1) of each kind, **leaving out a unit whose first brigade is a
   `garrison_brigade`**.
3. For the divisions, then the corps, then the armies, need = their count / 4 + 1, in whole numbers; while the count of
   the kind above is below the need:
   - **borrow**: over the country's other agents in order, and each one's non-division land units, a unit of the kind
     above with no children, no subordinates promised to it (4.2), standing in a real area, not taken this round, and
     joined to the headquarters' province by areas no enemy holds (6.5), counts one and is attached under this
     headquarters. The need is asked again only between agents.
   - **make** otherwise: of the agent's land units of the kind (not garrison-led, not chosen yet for this kind, on land)
     the one with **the fewest units above it**, the first of equals, gets a new command over it (4.4), and the count of
     the kind above goes up by one. With none, the next kind.
4. **The ladder stops at army groups**, and a command made today cannot be chosen until it exists, so the ladder grows a
   level a day: corps, then armies, then army groups.

### 4.2 Filling commands

Each unit's **promised** count starts at nought each time. The agent's land units, but its own, any aboard a fleet, are
sorted: army groups, armies, corps, and the rest - **garrisons** where the first brigade is a `garrison_brigade`, else
**divisions**. Each garrison whose parent is not the headquarters is attached under it (its old parent's promised count
down one, the headquarters' up one); so is each of the agent's fleets (5.1) whose parent is not. Then divisions to corps,
corps to armies, armies to army groups, each unit U with parent P in order:

- reach(H) = H's first brigade's `radio_strength` x the `RADIO_<level>_LEADER_DISTANCE` of H's level / 1000, nought for a
  unit with no brigade; over = -1000, or with a parent, `distance(U's destination or place, P's destination or place) -
  reach(P)`, in thousandths.
- With P exactly one level above U: U is **kept** where P's march order blocks it or over <= 0, while P's promised count
  plus its children of U's kind is at most 4; otherwise, and with P elsewhere or none, U looks for a command:
- each candidate K of the level above, in order: n = K's promised count + 1 + its children of U's kind; K may take U at n
  <= 4, and at n = 5 only if K is P or U is beyond P's reach; score = the distance from U's place to K's, plus K's own
  over where positive, **x 0.85 where K is P**; the lowest below 1,000,000 wins, the first of equals.
- A winner other than P: P's promised count down one, the winner's up one, and U attached under the winner.

### 4.3 What attaching does

U leaves its parent and goes last under the new one - the same owner, the new one ranking above it - with no cap on how
many a command holds. A unit that changes parent removes its own agent, if it has one, and marks its old and new agents'
**replan subordinates**.

### 4.4 A new command

Over unit T (a corps, an army or a division):

1. A new id - the game's last unit id + 1 for the headquarters, + 2 for its brigade, **+ 3 reserved and unused**.
2. Where T stands; a T aboard a fleet at sea uses the fleet's base; not on land, nothing is made.
3. A headquarters **one level above T**, with **one `hq_brigade`** at **a twentieth of its strength**, no experience, its
   technologies the country's, named by `REGIMENT_NAME`; the country is charged a twentieth of the type's manpower (by
   the technologies' `build_cost_mp`, at least 50 thousandths, floored at nought).
4. Under T's parent - or its grandparent, where the parent is already of the new level - if that ranks above it; T leaves
   its parent and goes under the new headquarters.
5. Named `ARMYGROUP_NAME`, `ARMY_NAME` or `CORPS_NAME` numbered, the first number free in the country; its leader chosen
   where the country assigns its own; a battle checked for where it stands.

## 5. Subordinates and regrouping (section C)

### 5.1 The subordinates

Slot 78's sorting, as far as this piece needs it: over **the whole country's units** in their order, a unit belongs to
the agent when the nearest agent up its chain is this one - in practice, every unit under the theatre's headquarters - and
a carrier's air group to the agent of the fleet its carrier is in. Belonging units go onto the agent's lists:

- **land**: every land unit; **non-divisions** those above a division, but for one a sea lift or a drop has claimed
  (`moving.md`, section 2), which goes on no list but land - so a headquarters bound for its embark is not pulled in, and
  still counts as a division where the hold list is cut (`confirmed` on the running game, 2026-11-11: Italy's III Corpo
  d'Armata dell'A.O.I. entering April 1936 and Britain's 7th Army entering July 1941 are on neither original list,
  `reversing/findings/FINDINGS-liveread.md`, section 48);
- **fleets**: every fleet; **transport fleets** those with a transport that carry something or do not need port;
- **air**: every air unit, kept in order by range; **air transports** those that carry troops.

Which divisions are committed and which held back, the reserve, and slots 79 and 80 are the third piece's.

### 5.2 Fleets (`ManageNavalUnits`'s regroup)

Run for a country the AI runs (or a plan allowing headquarters reorganisation) when the agent's naval pass is forced.

1. **To port.** For each of the agent's fleets with ships, a base, and not retreating: **needs port** = a transport below
   0.5 of its strength, or another ship below 0.75 of its strength or 0.5 of its organisation. A fleet that needs port,
   away from its base and carrying nothing, is sent to the nearest of the country's ports - each area's ports largest
   first, the distance at sea / 1000 in whole numbers, the current base's x 0.75, the least by strict `<` - rebased
   there, or, the base being nearest, sent home.
2. **Worn ships out.** Otherwise a fleet with a **damaged** ship (a transport below 0.5 of its strength, another below
   0.9) or a **resting** one (a non-transport below 0.9 of its organisation), with no order, at its own base: where 0 <
   damaged < ships and it is not fighting, one new fleet takes every damaged ship. Either way it sits out the rest.
3. **Ready** fleets, in order, each not retreating or fighting, with ships, not handled this pass, with no order, at its
   own base. Its ships are counted by role - **capital** (`capital = yes`), **transport** (`transport = yes`), **sub**
   (`is_sub`), **carrier** (`carrier_size` > 0, not capital), **screen** (the rest).
4. **Split**, walking its ships in order: a capital goes when capitals > 4; a transport when transports > 4 or the fleet
   holds a capital or carrier; a sub when subs > 3 or the fleet holds any other ship (that count taken once); a screen or
   carrier when screens > 4. **Each ship that goes becomes a fleet of its own** in the fleet's province based at its base,
   and its role's count goes down one.
5. **Merge**: a fleet with no transport and fewer than 6 ships takes the first other ready fleet not handled whose
   combined counts pass: no subs beside other ships, subs of one range (the first ships'), at most 3 subs; capitals +
   carriers at most 3 where this fleet has any; not screens alone; not transports beside capitals or carriers; at most 3
   screens; reachable by sea. It is ordered to **join** the other (5.4), and both are handled.

### 5.3 Air groups (`ManageAirUnits`' regroup)

Run when the agent's air pass is forced or **regroup air pending** is set, over the agent's air groups that are fit - no
wing under half its strength or half its organisation at air stance 2, 0.7 of either otherwise, halved for a group with a
carrier's wing or an interceptor (the triage's lower pair, `moving.md`, section 17.2) - not busy and in no battle, at their own base. A group holding
a carrier's wing, a group on loan and a group with no base are passed over.

- **Split**, a group of more than one wing, walking its wings in order: a strategic bomber where there are more than 3; a
  tactical bomber where more than 2 or beside a strategic, naval, transport or CAS; a naval bomber where more than 2 or
  beside a strategic, tactical, transport or CAS; a transport plane where more than 1 or beside a strategic, tactical,
  naval or CAS; a CAS where more than 2 or beside a strategic, tactical, naval or transport; **an interceptor where the
  group holds anything but interceptors and multi-role fighters**; anything else where it has a strategic attack above 50,
  or more than 1 multi-role, or beside a strategic, an interceptor or a CAS. Each goes into an air unit of its own in the
  group's province, based at its base, and its type's count goes down one.
- **Merge**: a group of fewer than 3 wings takes the first other group by range not handled, neither with a
  `strategic_attack` above 50, whose combined wings pass: no carrier wing; strategic at most 3 and alone; tactical, naval
  and transport together at most 2 and of one kind; transports at most 1; multi-role at most 1 and without strategic or
  interceptors; interceptors at most 3 and alone with multi-role; CAS alone and at most 2. It is ordered to join the
  other, and **regroup air pending** is set.

### 5.4 The commands

- **A new fleet or air group** (`CCreateUnitCommand`): empty, the id given, in the province, based at its naval base or
  air base; it is attached under the agent's headquarters as it is made.
- **A ship or wing moved** (`CTransferSubUnitCommand`): refused where the receiving unit is fighting. The ship goes; a
  receiving unit with no name is named `NAVY_NAME` or `AIR_NAME` numbered, the first number free; its leader is chosen where
  the country assigns its own and the unit has none; an emptied unit loses its leader; a carrier brings the air groups
  based on it.
- **Join** (`join_fleet`, `join_air`): the unit sails or flies to the other's province; arrived, it is merged into the
  other as the merge button merges (`selection.md`, section 5.4) - its ships or wings and what it carries go over, the
  more skilled leader stays - and it is removed. With no other, it goes back to its base. **A join_air keeps its partner
  twice** - as the order's target and as the join's own - and a load restores only the second: so the merge still happens,
  but the air body's triage and regrouping, which ask the first, find a loaded join_air dead, call it off and regroup
  afresh (`confirmed` on the running game, 2026-11-11: Italy's 2nd Squadra Aerea, joining the 5th Comando Aerosiluranti
  'Eolo' in the save of August 1938, read with no target on entering, and the Eolo sent to join it instead;
  `reversing/findings/FINDINGS-liveread.md`, section 51). A join_fleet's target is restored. The original saving a loaded
  join_air again would write its target once; ours writes it twice, as a join given in play is written.

## 6. Headquarters (sections B.6, B.7)

### 6.1 The level wanted

n = the plan's area count where it is active, else the theatre's province count: 3 at n <= 5, 2 at 6 to 10, 1 at 11 to 29,
0 at 30 and above. It would make a parentless agent's unit a higher command; a theatre's headquarters being level 0
already, **for the AI it never acts**.

### 6.2 The command province

Every hour, where the agent's unit is a land unit with children: the agent's command province is chosen again where it
has none, or on the day's forced hour (A0 on a day divisible by 9), or where the unit stands on a front province (section 8,
step 5's test).

### 6.3 Where a theatre's headquarters stands

The theatre's headquarters' province is `theatres.md` section 10's choice; for any other level, each of the theatre's
provinces scored, where the agent's area of operations is not empty: skipped at a mean distance to the area of nought;
100000 / mean; halved beside the area (for an army or a corps, halved **not** beside it); halved again bordering another
holder's land; x 0.01 in the area; x (1 - a/15) for a = the friendly land units in it or making for it; x 0.01 with
infrastructure below 200; the highest wins, strictly. Every hour the headquarters is marched to its command province.

### 6.4 Headquarters pulled in

On the full pass, every non-division headquarters under the agent - one that is moving always, one standing still only
where a child is under its full reach, it trespasses, or it stands on a front province while the frontier is at war; one in
a battle too, a land battle always letting it go - is sent, where the land reach test (`moving.md`, section 10) says it can
get there, to the province among its children: each land child's destination or place (reachable by areas no enemy holds) and
every land neighbour of it in the same area, whether or not the child's own province could be reached; scored by the mean distance to the children - the
garrisons left out where any child is none - skipped at nought, -6 for where the headquarters is going, +1000 bordering foreign land, +100000 with infrastructure built below 200 (the
most built, not what the province has the use of); the lowest
below 100000, single precision throughout, and its own province where none is. Corps are walked before armies, each seeing where the one below has just been sent. **A headquarters trespassing** - standing where its owner may not enter, never the rebels' - is looked at standing still, and **on the move answers where it is bound at once**, so it is left on its way: Guangxi's 2nd Corps in Chinese-held 5836 entering April 1936, which the original left marching (`confirmed` on the running game, 2026-11-11, `reversing/findings/FINDINGS-liveread.md`, section 47). The mark is asked as it stands, where the original makes it again each hour and at any change of pact.
A child's "destination" is where the AI sent it in this pass where it did, else its way's end: so the children the land block has just
moved count at their new goals. `confirmed` on the running game, 2026-11-11: every one of 72 choices made on the same children - the
same places, goals and garrison marks - was the original's, and every choice that differed had children that differed
(`reversing/findings/FINDINGS-liveread.md`, section 33).

### 6.5 Joined by areas

Two provinces are joined when a path of areas runs between them - the supply network's areas, each beside the next -
none held by an enemy of the country.

## 7. The plan (section D)

A unit's plan: **active**, **stance** (0 withdraw, 1 prepare, 2 defensive, 3 attacking, 4 blitzing), **air** and **naval
stance** (0 prepare, 1 defensive, 2 offensive), **our** and **their power** (thousandths), **embark** and **debark**, its
**area** of operations, **min** (one count for each unit type, the null type first, then the unit files' types), and
**objectives** (a province and a priority each) and `allow_hq_reorg`. A plan starts inactive, stance 2, air and naval 1.

### 7.1 Active

Set by binding (1.2) and by every change to the plan's stances or powers; cleared, with the plan reset, by unbinding.

### 7.2 The stances (`UpdateUnitStances`)

Every hour while the country's stances are unsettled, then once a day at hour (number + 16) % 24:

1. Over the country's agents in order: **an agent with no unit ends the pass** for the hour, unsettled. An agent whose
   frontier is at peace (section 8.2) has a sum of nought; otherwise its theatre's hot fronts, each new enemy once, add
   the plan's their power / 1000 where that enemy is at war with the country or either is `REB`. The least threatened
   agent is the one with the smallest sum.
2. Over the agents: air or naval stance not 2 gives both 2. With no area the stance is left - and a frontier at war leaves
   the pass unsettled. At peace, stance **1**. At war: r = our / their power, or 10 with their power at nought, halved
   where more than one hot enemy exists over all agents and this one is not the least threatened; g = the friends' summed
   `army_combat_value` / the enemies', or 10; v = the front coverage (8.4) x r: **under 0.99 stance 2, under 1.98 stance 3,
   else 4**, and a 3 or 4 with g under 0.9 one less.
3. A stance that differs is set, pushed down to the agent's children, and **plan posted** set - and **the reserve released**: every
   held-back division's mark cleared and the list emptied, so the next sort decides it again at the new stance
   (`reversing/findings/FINDINGS-liveread.md`, section 45).
4. A pass that ran whole with no agent at war without an area **settles** the country's stances; nothing unsettles them.

### 7.3 The powers (`UpdatePlanPower`)

At binding and at hour A0: our = **the sum of every division's power** on the agent's land list, a division getting x (1000
+ 80 x its province's fort level) / 1000 unless it is moving or the stance is 3 or more; their = **the enemy's power the
area faces**, gathered as the division need is worked out (7.5): each counted front province's hostile units' power by
the province's weight. **They are written only when either changes in its whole number.** A unit's power is the sum over its
brigades of `(strength/100) x value / 1000 x (organisation/100) / 1000`, a land brigade's value `(soft + hard + piercing
attack + (defensiveness + toughness)/10 + armour) x (2000 - softness) / 1000`, an air wing's `air_attack + air_defence +
500`, a ship's `(500 + sea_attack + sea_defence) x hull / 1000`, all in thousandths.

### 7.4 Objectives

**Priority provinces** (at war only): the allied objectives (`CAlliedObjectives`) that the country's faction members at war,
and the countries it is at peace with, set for it. **Objectives**, for every agent, only on a **hot** theatre: the areas
beyond its fronts (a front hot or facing a front-worthy enemy) whose first province's controller is front-worthy, once each
in the order met; each of their provinces at priority `victory points x 20 + conquer_prov + 100 for a priority province`,
kept at 1 or more, `hold` and `invasion` no. Posted where they differ; a theatre not hot clears them.

### 7.5 The division need (`EstimateTheatreNeed`, `EstimateAreaNeed`)

Read whole on 2026-10-19 (`FINDINGS-moving.md`, section C). A theatre's need is the sum over its areas, in order, of
each area's need, **relaxed**, one list of visited areas shared by them all. One area's need, for the agent's country C,
all division toward nought:

0. **An area the enemy holds** (its first province's controller C's enemy): its enemy divisions, and nothing else.
1. **Italy and Ethiopia**: C Italy and the area's first province held by Ethiopia - nought.
2. **Objectives**: the plan's objectives lying in the area and in this theatre (the province's theatre stamp; with no
   theatre, in the plan's area).
3. **The fronts**: each of the agent's front provinces (8.2) beside the area by an edge not impassable, the province there
   with 200 infrastructure or more - a pact with its controller younger than two thirds of `NAP_UNBREAKABLE_MONTHS`, in
   whole months, undoes it - is **counted** where its controller is front-worthy to C; **halved** where it is also no war
   candidate and its surrender progress is past a tenth, and a halved one is no **live front**. Its weight: relaxed and no
   pact, 1000 for a war candidate, else 500; otherwise 2000 and 1000; halved, half. Its floor: 500 where halved or with
   infrastructure 300 or less, else 1000. Its hostile land units (7.2's hostility, headquarters too): n of them and P their
   power. **score = max(floor, n x weight)**, **power = P x weight / 1000**. In its controller's acting capital's area, its
   controller the first member of a faction: score + weight, power + power x weight / 1000. A score over 1000, C's capital
   area being another area that borders this province's area: power x 1000 / score, and score 1000. The power goes to
   their power and the score to the sum; the province's area is one **faced**.
4. **N1 = the sum / 1000, rounded half to even.**
5. **N2**, only for an area whose first province is C's: the theatre's provinces in the area, each worth `CProvinceHoldValue`
   (7.6), summed, / min(the area's provinces, 40), cut; at most a quarter of its provinces (at least 1 when not relaxed).
   An area with no neighbour, not C's capital area: over 1 with under 3 provinces is 1, over 2 with under 15 is 2.
   Otherwise nought with a sum over 2 is 1.
6. **need = objectives + (a live front and N2 > N1 ? N2 : N1).**
7. **Focus areas**: where the area's block of one side's land holds more than one province, each focus area of the same
   block not visited is visited and its need added - before what follows, which then applies to the sum.
8. The area C's, not its capital area, bordering an area held against it (`COwnerArea_TouchesHostileArea`: a neighbour
   whose controller is at war with its own, or either the rebels) and the faced areas holding more than 3 provinces: **+4**.
9. C with more than one theatre and need over twice the brigades the faced areas' controllers have standing in them:
   **need = max(need / 2, twice those brigades)**, at least 1.
10. **The capital floor**: the area C's capital area, C major or over 100 IC - need at least, relaxed, the greater of its
    ports' naval base levels / 4 and its ports / 2; otherwise 3 x its ports.
11. **At least 1.**
12. An area neither C's nor its subject's: over 1, halved, at least 1.
13. C Italy and Addis Abeba (province 9943) in the area, or in an area beside it held by Italy's subject: **a third**.

The capital floor is what makes **every European Soviet theatre need 18**: the Soviet Union's land is one area of 2694
provinces holding Moscow and 73 levels of naval base. The balancing between theatres (10.2) weighs the need, and the
aircraft wanted (section 9).

### 7.6 What a province is worth holding

`CProvinceHoldValue` (the second term of the original's `EstimateAreaNeed`), in thousandths; asked for N2 and, with **hold**, for a hold position:

- **Nought** below 200 infrastructure.
- **Near**: a land neighbour by an edge not impassable whose controller is no friend of C's (with access) and either has a
  threat towards C of 100 or more - with C's antagonism of it added where C is at peace, the AI runs its units and hold is
  clear - or is front-worthy to C. **Full**: always, but for the United States at war while Britain owns land and is not in
  exile, its owned core victory points less those it holds under 2.
- 3 x the fort level where near; full: 5 x (the coastal fort level + 1) where there is one; the air base's built level and
  the naval base's - for Germany and Italy on a curve, x = 10 x level + 30, above 100 (x - 100) / 5 + 90 - doubled outside
  its holder's capital area in an area of under 15 provinces.
- Victory points: owned by C, the greater of 2 x points + 5 and the points' share in per cent of all C owns; held but owned
  by a country not in exile, 2 x points + 5.
- 100 for C's acting capital; full, 60 for a strategic resource.
- Half the province's IC - or, with hold: 100 for its own supply depot, half its IC x its revolt risk (the shown figure)
  / 1000, and, full with a naval base and a port, 30 + 2 x the ships of the fleets based there, at sea or not.
- A quarter of the plan's objective's priority on it.

## 8. The area of operations (section E)

### 8.1 When

Whenever **area dirty** - set as an agent is made and whenever its theatres are made or refreshed - and on the weekly hour
(3, step 1). Setting it clears area dirty, sets **full pass pending**, and clears the hold provinces.

### 8.2 The rule (`SetArea`)

Nothing unless the agent has a theatre's headquarters.

1. The focus areas are made again (8.3).
2. **Candidate fronts**: the theatre's own, in order; then, for each focus area **the front search may widen into**, every
   front of that controller's theatres starting in the area and facing anyone but the country, without repeats - a subject
   taking only those against an enemy. The search may widen (`AreaMayWidenFrontSearch`, `CFrontSearch`;
   `FINDINGS-subdivide.md`, section 6) into an area with provinces whose controller is not the rebels and is on the
   country's side, or fighting beside it and not its enemy; then yes for a country in exile; yes, for a country the AI
   runs, where one of its priority provinces lies in the area or in an area beside it held by an enemy; no where a person
   plays the controller; yes where the controller is the country's subject or a member of the faction it leads; and
   otherwise yes where an area beside it of **more than five provinces** is held by a country it is at war with, the side
   it fights on led by a country nobody plays - which is how **Slovakia's theatre, facing only friends, takes Germany's
   front against the Soviet Union** in July 1941 (`reversing/findings/FINDINGS-liveread.md`, section 37). The
   expeditions ask the same with any unplayed controller enough, and the stranded units' recall as the area does.
3. **Taken**: a front that is hot, or faces a war candidate (`theatres.md`, 4.1), or - with **wide open** - a front-worthy
   enemy. Wide open: the theatre is not hot, or the country prepares war on the enemy, or its threat from the enemy is at
   least 40 and the enemy's neutrality at most 20.
4. **Provinces** of a taken front, in its order: infrastructure at least 200, in a real area, the country's own or a front
   province or its subject's, and with an edge, not impassable, to a land province with a hostile controller (nobody,
   `REB`, or not of the same side and either not a co-belligerent or an enemy) **that is itself in a real area**.
5. **Second chance**, where nothing was taken: each of the theatre's own fronts whose enemy's threat towards the country
   plus the country's antagonism of it is above 100 gives **every** province with infrastructure at least 200 in a real
   area.
6. The area is set - empty too, which clears it - and the plan's min padded to the types. **The frontier is at war**
   where a province of the area has a neighbour, by an edge not impassable, on land, whose controller the country is at
   war with (`CAIUnit::ClassifyOpsAreaFrontier`, one of the replan calls; `FINDINGS-opsarea.md`).

### 8.3 The focus areas

For each block of one side's connected land among the theatre's areas holding more than one province: the block's areas
not the theatre's country's, not of another real faction, added where this theatre has strictly the most edges into the
country's land along the area's fronts (the first of equals by theatre number), repeated while one is added.

### 8.4 The front coverage

Every hour: of the hold positions - the area's provinces at stance 1 to 4 - those **exposed** (an enemy-held land neighbour
in an area of more than 5 provinces) and **covered** (one of the country's childless land units stands there):
covered / min(exposed, committed divisions); 1 with nothing exposed, 0 with no unit or no division.

## 9. The aircraft wanted (`min`, section E.7)

On the full pass, the plan's min is zeroed and, each where its type is available and usable: **interceptor** and
**tactical bomber** = (the agent's brigades + 3 x its division need) / 40, the need being `EstimateAreaNeed`'s count over the
theatre's areas (CAS for the tactical bomber where that is not available); **multi-role** = round(tactical / 2 + naval / 2 +
transport / 2), each a half rounded to even; **naval bomber** = the naval score rounded to even (`moving.md`, section 12).
The transport plane and strategic bomber wants are not built (section 13). Posted where it differs.

## 10. Between theatres (section F)

Units move from one of a country's agents to another by being **put under the receiving theatre's headquarters - nothing
marches**; what then moves them is the receiving theatre's own work.

### 10.1 When

At the country's hour (its number % 24), **once its pass has run once**: the powers of every agent (7.3) are worked out
again, and the divisions balanced (10.2). At (number + 16) % 24: the air units (10.3).

### 10.2 Divisions

1. The agents with a unit and a theatre, sorted by need - count, less 1000 where the frontier is at peace, the greatest
   first, equals in their order.
2. **Takers** in that order, but one with fewer needed than it has where its frontier faces no war candidate. **Donors**
   for each: every other agent, walked from the end of the order, those **connected** to the taker (an area shared, or
   the donor's headquarters can reach the taker's) put first.
3. **A donor is passed over** where the taker faces no war candidate while the donor's frontier is at war at a stance of 2
   or less; where 3 x the taker's need - its count < 1; where the taker faces no war candidate, the donor is at war and the
   taker's count is at least two thirds of its area's provinces.
4. **The mobile grab**: connected, the taker facing a war candidate, the donor not, and the taker needing more than the
   donor - every division of the donor holding a motorized, mechanized, armor, light armor or heavy armor brigade goes.
5. **How many**: ratio = count / need for each, 1 with no need. (t1, t2) = (0.4, 0.15), or (0.3, 0.1) with the taker
   facing a war candidate, or (0.35, 0.125) with both. Where the donor's ratio - t1 > the taker's, n counts up while n <
   the taker's need - count and the taker's ratio with n more is below the donor's with n fewer, less t2. The least given is
   the donor's count - need where the taker faces a war candidate, the donor not, and they are connected - and then only
   divisions of speed 2 or more; half that where both face war candidates, connected, and the donor has more than twice
   its need. n is at least that and at most 3 x the taker's need - count; under 1, nothing.
6. **Which** (the donor's pick): for each of the donor theatre's areas a keep = its need (7.3) x f - f = 1 - n / (count -
   n - least given), at most 1 - rounded half to even, at least 1 where there is a need. A division is a candidate where it
   is the donor's, not airborne, not a garrison on or bound for a province of more than 9 victory points, one the
   may-leave test lets go - not strictly, a unit defending a battle held, the candidates so far left out of its count
   (`moving.md`, 7.6) - not bound for enemy land, fast enough where asked, and in an area that has more than its keep.
   **In the choosing**, a candidate with no idle division beside it on a war candidate's border (7.6, the count, those
   taken so far left out) is passed over. Each is scored (1 - its distance to the taker's fronts / the greatest) x 10, x
   15 where its commander has given one already, x 0.25 unless its area is one of the taker's or beside one, x the idle
   divisions beside it / 100 where they are fewer than 100; the best above nought is taken, the first of equals, until n
   are (`FINDINGS-moving.md` A.4e). **The distance stops at 1000** (250 of the map's pixels): the original keeps the
   least square from a first best of 1,000,000 and takes the root at the end, so a theatre with no fronts and anything
   further off answer 1000 (`CMapDistance.LeastFromAMillion`). The greatest among the candidates is then 1000, and every
   candidate at it scores nothing, so **a donor gives only what stands within 1000 of the taker's fronts**. Measured in the
   running game (`FINDINGS-balancing.md`). It reads as an accident and is kept (`docs/enhanced.md`). The ratios, the scale
   and the scores are single precision, as the original's, the 0.25 is not taken off a unit at sea, a garrison on its own
   depot stays, the agent's target provinces give nobody, and a mountaineer division at rest in the mountains of the donor's
   area scores a third. An area's "have" counts by where a division is bound.
   A donor facing a war candidate gives no mobile division unless only fast ones were asked.
7. **Whole commands**: a commander, not fighting, retreating or a theatre, all of whose subordinates were taken, goes in
   their place - and its own commander the same, once.
8. Each goes under the taker's headquarters, its power and divisions moved from the donor's figures to the taker's.

### 10.3 Air

Every agent in order is offered every unit of the country not moved this pass and not its own: **one holding a transport
plane or a paratrooper brigade goes to the theatre of the capital**; and an air unit holding a tactical bomber, and no
interceptor, multi-role or CAS, goes to an agent whose frontier faces a war candidate from one whose does not.

### 10.4 Fleets and transports (`FINDINGS-agents.md` F.3, F.5)

**Fleets**, at (number + 16) % 24, every fleet with no agent of its own, in order, its current agent the topmost up its
chain: **one holding a submarine** goes to the agent with the most (submarine want - submarines it holds) - (the current
agent's want - what it holds + the fleet's ships), only agents with a want counting and the current one's term -10000 with
none, above nought; **a surface fleet**, where the agents with ports face enemy ships at all (their wants: the ships of the
enemy fleets handed to them, `moving.md` section 12), goes to the agent with the most want / all the wants - (what it
holds, with the fleet's ships where it is another's) / the country's ships, from -10000. **At peace nothing moves**: no
enemy ship is handed to anyone.

**Transports**, at (number + 8) % 24, for each agent with troops listed for the sea (`moving.md`, section 15) and a sealift
port - its theatre's largest area's largest naval base: while their weight is above what its transport fleets can carry,
the fleet with no agent of its own, no order, room for a whole unit, not its own and not the sealift's of another agent,
with the most 10 - min(distance by sea to the port / 400, 10) + its room, is taken.

Each moves by being put under the agent's headquarters, and both agents' subordinates are sorted again.

### 10.5 Units on a friend's land (`CAIUnit::ManageExpeditions`, `FINDINGS-agents.md` C.7)

Each agent of a country the AI runs, on its full pass, after its subordinates and the threatened provinces, goes through its
land units standing in a real area. **A unit on loan** from a country not in exile goes home when it has no blocking order,
its lender is nobody's to play and the lender may no longer operate in the area: what is under it is detached and the unit sent.
**Anything else** - its own, or on loan from a government in exile - is lent to whoever holds the province, when neither
its place nor where it is bound is the country's, the holder is no enemy either way round, **the two share a faction or both
have none**, the unit has nothing under it and is no theatre, the country may operate in the area, the area's holder is
not played or holds one of the country's priority provinces, and the area is outside the country's theatre. It is
`CSendExpeditionCommand`, with nothing asked of the other country and no influence spent, and the lent unit's date is the
game's end. It is how the USA's Phillippine Force is the Philippines' by the first balancing. An area "may be operated in"
by `AreaMayWidenFrontSearch` as these ask it: the holder on the country's side or fighting beside it, and then in exile, a
priority province in the area or beside it against an enemy, or the holder unplayed.

## 11. Kept as the original does them

- **The played country's agents exist and are never bound**, but for a headquarters the player hands over by the unit panel's box (`mapunits.md`, section 12); its other theatres carry no plan.
- **The ladder grows a level a day**; three ids are taken for a command and two used.
- **A new command starts at a twentieth of its strength** with no experience, where a theatre's headquarters starts at a
  fifth with the starting experience.
- **An agent with no unit ends the country's stance pass** for the hour; the stances settle once and are never unsettled.
- **The powers are written only when a whole number changes**, so a small figure keeps its first hour's value.
- **A war stance is never below 2**; the AI never withdraws; a peaceful agent with no area keeps stance 2.
- **The second chance takes whole fronts**; an empty area is set and clears the old.
- **A sub in a mixed fleet always ends alone**, the count of other ships never going down.
- **Objectives include provinces of front-worthy neighbours not at war.**

## 12. Ours

- **The pass runs as one step of the hour, after the ministers, for every country in turn**, where the original runs each
  country's ministers and agents together on a worker thread from the frame after the hour.
- **The first of equals by the order things were made**, as in `theatres.md`.
- **A fleet's way to port at war** is any port, the original's route test at war not read.
- **A group of one wing is passed over for the split only**, and may still look for another to join: the reading has it
  passed over for both, and the saves show single wings joining others.
- **An air group's fitness takes one pair of thresholds**, where the original's triage has two, a strength below one
  sending a wing to rest and below the other unfitting it.
- **A blocking order** is a unit on the move or under any order but a march; the original asks each order kind.
- **What a province is worth (7.6)** is worked out in thousandths, where the original works it in single precision; the
  share of victory points is cut, not rounded.
- **A port's "naval station"** (7.6, with hold) is a province with a sea of its own and a naval base; the original asks a
  pointer the reading did not name.
- **The days of the year** are counted from 1 January in a year of 365 days, as the game's calendar has them.

## 13. Not built

- **Moving and attacking** - the land block, `ManageLandUnits`, `IsUnitReservedForTransport`, the reserve and what is committed,
  and `RebuildHoldProvinces` - the third piece; and with it the axis (the path to the objectives at stance 3 and 4), embark and debark, the
  movers between theatres, invasions, the pride, the leaders promoted and the units sent abroad by a landless country.
- **The transport plane and strategic bomber wants**, and a term of the
  division demand whose source was not read, taken as nought.
- **The front coverage's hold positions at stance 0**, a stance the AI never chooses.
- **The friends' and enemies' combat value in the war stance** (`g`, the country's `army_combat_value`): taken as 10, so a
  war stance is never lowered for it.
- **The prune of an area by the priority provinces** (`SetArea`, step 6), which needs a war and a friend's request, and
  the second list of countries whose requests count (`non_hostile_countries`).
- ~~**The air groups' rebasing** (`ManageAirUnits`' second phase) and the busy groups the first phase sends to rest.~~
  Built with the third piece, 2026-10-19 (`moving.md`, section 17).
- **Of the movers between theatres** (section 10): the air units' move by the plan's group difference; a transport fleet
  taken from an agent with troops of its own for the sea, which the original allows by weights not read; and in the
  donor's pick, a division's own supply depot keeping a garrison, the idle divisions beside a war candidate's border, a
  mountain division standing in the mountains, and the target areas.
- **The objectives of other areas' borders** in the need (7.5, step 2): only the human's delegated AI has them.
- **What holds the balancing back** (10.2): with the need as read, ours moves far more than the original - section 14.

## 14. Checked against the saves

`OpenHOI3.SaveCheck agents <install> <start-date savegame> <later savegame> [tag] [examples]` makes a new game as a player
gets it, runs it to the later save's hour, and sets beside the save, for every theatre of a country nobody plays by its
headquarters' name: the army groups, armies, corps, divisions, fleets and air units under it, and its plan - active, the
three stances, the two powers, the area in order, the objectives and what it wants; and each country's fleets and air units
(those aboard carriers left out, as the save keeps them in their ships). Given a tag, that country's fleets and air units
both ways.

**The first screen**, given the start save as both saves: army groups, armies, corps, divisions and fleets under each
theatre 106 of 106, air units 104 - the sort of section 2.1. On 2026-10-17, from `Germany1936_01_01_00` to `_01_04_12` (84
hours): **the plan's active, stance, air and naval stance, area
in order and objectives agree for all 106 theatres**; the ladder - army groups 99, armies 96, corps 89 and divisions 87 of
106 theatres - and the army groups, armies and corps of every country 115, 171 and 328 against the save's 116, 173 and 328;
air units under a theatre 104 of 106 and each country's air units 99 of 101; fleets 95 of 101. What differs: the
divisions the original balances between theatres (section 10.2, its need not settled), the fleets' merging partners
(the order of the ready fleets), and the powers - our power 21 of 106 and their power 78 - where the units under a
theatre or the enemy's on its front differ, the enemy's units not moving here yet. The aircraft wanted agree for 67, the
naval bomber's want not being built.

**With the division need read (7.5), 2026-10-19**, the same run: the aircraft wanted **90 of 106** - every one that
differs now the naval bomber's want or a theatre whose divisions differ - divisions 91, corps 91, army groups 99, armies
91, their power 80. The Soviet theatres come within one division of the save (Leningrad 18 against 17, Brjansk 14
against 15), as the reading's own model predicts. **Britain and France move too much**: the original gave Home Command,
whose need of about 31 against 18 divisions its aircraft want of 4 confirms, nothing by 4 January, nor Port Harcourt or
St. John's, and moved one division each from the Army of India to the Far East and East Africa; ours sends India's
divisions to all of them. With the old need nothing of Britain's moved.

**With the distance's cap (10.2), the rest of the pick and the lending of units (10.5), 2026-11-09**, the same run: divisions
**106 of 106**, army groups 106, armies 105, corps 104, fleets 102. The running game showed the cause: the first pass's needs
the same as here in 104 of the 105 theatres that can be compared (one of Japan's is 34 against 55), India asked for 8 for Home
and returning none. Of the running game's 102 picks in four days, 101 are ours in the same place with the same divisions, since the reach test of
a theatre's headquarters asks whether areas the country may enter join them, seeking no way. The other is the Soviet Union's: its 42nd Corps has four divisions there on 2 January and
five here, so it moves as divisions and not whole.

**At war** (`Ireland1941_07_16_07`, run 51 hours both ways, 2026-11-09; `FINDINGS-balancing.md`): 27 of the 28 theatres
compared agree in need and count on the first pass, the Soviet Union's 75 among them, and Vichy's ten picks and Portugal's three
all agree. Germany's theatre 6 is 73 and 118 against the running game's 72 and 119: its 214. Infanterie-Division, waiting in the
save, is placed by both but in other provinces (1626 there, 1861 here) and is under the theatre there by the next day, not here. Of
the running game's 57 picks 47 are the same in place; one Soviet donor gives 10 of 15 where the original gives 12, its units having
moved on their own for 21 hours by then. Italy's second day balances where the running game's does not.
