# The worklist

The project's working notes: the staged plan, what is next, what was left, and a detailed account of what
is built. `readme.md` is the overview for players and contributors; this file is what a working session
starts from. Keep *Next up* and *Loose ends* current: when a piece of the plan lands, move it out of
*Next up* and into *Where things stand*; when something is deferred, it goes under *Loose ends*.

## The plan

Agreed 2026-10-07. Each stage ends with something that runs.

| Stage | What gets built | Done when |
| --- | --- | --- |
| 1. The world on screen | Provinces, adjacency, terrain, countries and start-date ownership read from an install; a political map with pan, zoom and province picking | The map of the start date is on screen. **Reached 2026-10-07** |
| 2. The clock and the economy | The hourly tick with its daily and monthly passes in the original's order; every state change as a command; IC, resources, manpower, production, research. The interface runtime arrives here, with the top bar as its first screen | A 1936-1948 run completes headless with totals that are plausible against a real savegame. **The clock, the command queue, fixed-point numbers, buildings, laws, ministers, technology levels, each country's IC, its home production, what its stockpile starts with, its six shares of IC and what consumer goods and supplies do with theirs, its leadership with what its four shares make, research and manpower are in (2026-10-08) and checked against savegames, and so is the interface runtime with the top bar and the production and technology screens; of what needs units, reinforcement, upgrades and the build queue are in (stage 3), with the four windows a unit is ordered from** |
| 3. Units and war | Orders of battle, units on the map, movement, land combat, supply; then air and naval | Two countries can fight a land war to a result. **The unit types and the orders of battle are in (2026-10-08): every brigade, ship and wing the base game starts with, holding what the original gives it on the first hour, checked one by one against a savegame. So is what a unit draws each day - supplies and fuel, the men who go home, reinforcement and upgrades - checked brigade by brigade two days on; and the officer ratio with what it does to organisation, hour by hour, checked the same way. And the build queue: what a unit costs and takes, the day's work on it, and the unit that comes out, checked against five saves of one game over four months - with the division designer and the ship, air and brigade builders that order one, read out of the original and not yet looked at beside it. And the units are on the map (2026-10-09): a counter for every unit that is not in port or at its base, stacked by province, and the menu a finished unit is placed from. And land units move (2026-10-09): an order by a right click, a route found as the original finds it, and an hourly march at the original's speed over the original's distances - every one of the map's 83,596 edges checked against the cache the game keeps of its own. And a selected unit has its panel (2026-10-09). And fleets and air units are sent about (2026-10-09): the port and the airfield on the map a docked fleet and a grounded wing are picked by, the window of orders a right click brings up, a move and a change of base, the range, and the sea's own distances - all 17,621,016 of them checked against the cache the game keeps of them, and the sailing and the routes against savegames. And land units fight (2026-10-09): a unit walking at an enemy starts a battle and fights it from where it stands, the front and the reserves, the hourly round with its damage, the side that gives way and falls back, the province taken, and the delay an attack leaves - read out of the original and not watched, there being no savegame with a battle in it. What a leader does to a battle is not in yet. And units are supplied (2026-10-10): a network of depots, the day's carrying province by province, and convoys across the sea - the network set against three savegames province for province, the carrying against a fourth in 184 networks of 234, and all 261 of that save's convoys made again way for way. And fleets fight at sea (2026-10-10), the first of five pieces of air and naval combat: the sighting, each ship's own distance, who it shoots at, sinking, breaking away and falling back - read out of the original and not watched, like the land battle** |
| 4. Politics and script | Events and decisions with their triggers and effects; diplomacy, factions, politics, espionage, occupation, revolt | The base game's events load with no unknown key and fire on schedule. **The first half is reached (2026-10-12): every event and decision of the base game is read, with no unknown key** |
| 5. AI | The planning and scoring layers, last because they consume everything else | A whole game runs with no human player. **The tech and production ministers are in (2026-10-16): every country nobody plays researches, sets its sliders, builds and places its units by the install's Lua** |

Two notes on stage 1. The province bitmap turned out not to need a cache: it is read in
about 120 ms. And `docs/mechanics/map.md` lists the things about the map that are not
established - among them that the terrain rule comes from modding experience rather than
from the executable.

Four choices sit under all of it:

- **The base game first, mods second.** Their Finest Hour on its own is the target until
  it works; BlackICE then becomes a third layer over it, and a much harder test.
- **Fixed-point numbers.** Values are held in thousandths, as the original stores them,
  so that the simulation is deterministic and rounds the way the original does.
- **Every change to the game state is a command.** Nothing mutates the simulation
  directly, which is what lets a run be replayed and, later, shared between players.
- **A written mechanics spec comes before each system.** The reverse engineering
  write-ups mix formulas with addresses, so each system gets a spec of its own here -
  formulas, ordering, rounding, caps, no addresses - citing the write-up it came from.

## Next up

**The BlackICE layer, a stage of its own** (`docs/mechanics/mods.md`). **Stage 1 - built 2026-11-16**: a mod laid over the
install as the original lays it - its descriptor found and matched as the launcher names it, its folder in front of
`tfh/` and the install, `replace_path` on a folder's listing alone, the player's folder its `user_dir`, several mods in
the order of their dependencies - by `--mod=<name>`, by the original launcher's `-mod=mod/<file>.mod`, by VS Code's *Play
OpenHOI3 (mod)* and by the savegame tool's `mod=`; the file checksum is the original's for Their Finest Hour and for two
BlackICE builds; and `OpenHOI3.SaveCheck modsurvey`, which on BlackICE GitHub makes a new game and finds what the mod asks
that is not answered - LuaFileSystem first, without which none of BlackICE's AI loads, then `remove_brigade`, wxLua,
BiceLib's 42 functions and the original's country variables in Lua. **Next: stage 2**, a BlackICE new game set beside a
BlackICE save, and **stage 3**, playing it; the survey's list is where stage 2 starts. Awaiting the user's decision:
`docs/compatibility.md`, entry 28.

**Next, set 2026-11-09: checking the original's running game against ours, through Cheat Engine.** The balancing was settled this
way: the original is read live, its AI's picks logged, and ours run from the same start and set beside them
(`docs/mechanics/agents.md`, section 14; the fact base's `findings/FINDINGS-balancing.md`). The bridge is the workspace's
`.mcp.json`. The user starts the game and Cheat Engine; a session needs nothing else. What each session logs is in the fact
base's `CANDIDATES.md`, *Live probes queued*. In order:

1. ~~**Short reads.**~~ Done 2026-11-10 (`reversing/findings/FINDINGS-liveread.md`): the generator is seeded once and runs on, and
   the hourly reseeding is gone from here; the AI comes after all of the hour's passes; the attack odds' toughness was mended; a unit's
   totals were right; Italy's 5% was `veteran_army`, which ours gives. ~~The Soviet Union's odds stand about 4% high by a term
   not found~~ - set beside the running game again on 2026-11-12 with the units' cached halves fresh, every attacker's and
   defender's modifier of three countries is the original's, 313 of 313, once `unit_cooperation` was taken off the crowding as
   the original does, in the AI's estimate and in a battle (section 55; `moving.md`, section 11.4). ~~Three AI steps draw on
   the game's generator~~ - since 2026-11-12 the AI has a generator of its own, as the original keeps its game state's beside
   the static one battles draw on, and every draw the record puts on it goes there (section 59; `landcombat.md`, section 8):
   the invasion planner and the convoy raid as the original's; the patrol's centre, which the original draws on the C
   runtime's per-thread `rand`, there too. The savegame tool's trials that roll (the raid, the trade, the foreign minister,
   the events) moved with it, and their figures in `CLAUDE.md` predate it; every recording made before no longer replays.
2. ~~**The land battle**~~: *done 2026-11-10* (`reversing/findings/FINDINGS-liveread.md`, sections 7 and 28 to 31). Over four series of
   hourly saves of the original's, about 250 hours, with the generator restored before each battle, a battle's every hour after its first
   is the original's shot for shot, its tactic picks, new fronts and reserves coming up among them - once the tactic's aggressiveness was
   given to the attacker alone, combined arms counted every brigade, a broken support brigade dropped out of its unit's terrain and the
   other adjusters, and encirclement was built. Left: two single hours where one unit's one shot falls the other way, cause not found;
   and a battle's or bombing's first hour, which the rolls made before the battles decide and a save does not carry.
3. ~~**Air, bombing and the sea**~~: *done 2026-11-10 as far as the logs reach* (the same, section 30). Every bombing of land units, of
   the ground and of ships logged is the original's after its first hour, since the province's fire at bombers was given the original's
   friend test - at sea every unit of a country that is nobody's puppet fires, the bombers' own side too - and a carrier's air group was
   counted standing with its carrier; the air battles of the last series are the original's. Left: one bombing at sea three shots short,
   six hours of air battles in the first series over the sea south of Italy, and a sea battle at 10538 that drifts after its fourth
   hour, none of them logged closely enough to say why; and, as for land, each battle's first hour. A sea battle's breaking off was not looked at closely.
4. ~~**Republican Spain surrendering on 1 October 1936**~~: dropped 2026-11-10 by decision, the original's run varying too much for one war's date to say anything.
5. **The AI's land movement**: after four days 782 of 908 divisions stand where the save has them, 789 move or stay as it does, and
   317 of 542 are bound for its province. Load from a save rather than a new game, since differences compound from day one.
   *Begun 2026-11-10.* The original can now be started, loaded, stepped by the hour and saved with nobody at the machine (the fact base's
   `probes/hoi.lua`), and `OpenHOI3.SaveCheck load` sets the ways a stretch of hours gave beside the original's. **A load's two AI passes before
   its first hour are built** (`savegame.md`, section 2), which moved a burst of about 180 orders out of the first hour. Each of the six hours after the save of 16 July 1941 beside
   the original's (`skipentry`, the generator not restored): 5 of the original's 72 land ways the same, and 235 given that the original did not - but that compares fresh agents here with the original's running ones, so **entering is the like-for-like**: both sides' agents new, off one save.
   Two things found so: a loaded game's list of units is the save's tree walked children first (`CUnit.ListOrder`), and the garrison step was mended as read (`CPositionCount`, `CGarrisonFill`; `reversing/findings/FINDINGS-liveread.md`, section 13).
   Then, the same way: where a headquarters goes among its subordinates, the region mark `ai_prio`, a country's value without what is lent to it, and the line split from every division first committed, those since held back too (`CAgent.CommittedAtFirst`; section 14 of the same).
   And the hold list: 87 of the 96 agents' the running game's, province, score and order (section 15 of the same).
   Entering now: of the original's 167 land ways ours has 133 the same, 14 more to the same province by another road, 8 elsewhere, 12 none, and 11 the original did not give (on the saves of 3 August 1938 and 20 April 1936, 92 of 110 and 59 of 73; almost none of the three saves' orders on entering are attacks, so the attack has still to be set beside the original's, over hours), the reserve's size agreeing for the Soviet Union and Germany (section 16 of the same); the Soviet agents' position scores all agree with the running game's. Germany's and the Soviet Union's every position agrees in score, order and want, 95 of the 96 agents' committed lists in order, 89 of the hold lists, 92 of the garrison marks and 1,324 of 1,327 divisions' roles. **Hour by hour after entering** (`load`'s `series=`): two runs of the original from one save agree on 54 of 69 land orders over six hours; ours gives 53 of 73 the same, and over 48 hours 147 of 299, with 188 of its own - two runs of the original differ there too: a fall-back from 2390 one run made at the second hour and the other did not, which ours now makes as the first, the same three divisions to the same three provinces (section 24) - since the reserve's ring and the garrison step's records were mended as read (`reversing/findings/FINDINGS-liveread.md`, section 23), which also made entering 136 of 173. Germany's attacks of the hour its theatre goes to stance 3 are the original's, eight divisions of ten. Every strategy is made again as a save is entered, as the running game showed, which took out a burst of 67 orders at the first midnight. **The twelve long Soviet redeployments are the original's road since 2026-11-10**: a land unit may not stand in, or pass through, land with one level of infrastructure or none, which is in no area, and that keeps a long march on the railways (`movement.md`, section 3; `reversing/findings/FINDINGS-liveread.md`, section 32) - on entering 150 of 167 the same, 8 by another road, 9 none (97 of 110 and 65 of 73 on the other two saves), and over 48 hours 155 of 299 the same and 2 by another road where there were 11. Left: what ours' own orders are - about 130 in the first 45 hours, the Soviet Union's 37, Germany's 23, Italy's 17 and Slovakia's 10, many of them headquarters moved - and those follow their divisions' orders of the same hour, the pull-in being the original's on the same inputs, 72 of 72 (`agents.md`, section 6.4; `reversing/findings/FINDINGS-liveread.md`, section 33), so the divisions' own orders are what is left to explain; of them, the garrison pick weighs a division by its strength share and not its organisation, as the running game showed (`moving.md`, section 7.2; `reversing/findings/FINDINGS-liveread.md`, section 34), and the garrisons go out in the split's order and are picked in the original's precision (section 35), Japan's at 5315 as the original's, and a province the hold list names twice is two garrison targets (section 36), Bulgaria's as the original's, and an ally's front taken where an area beside it is held by an enemy at war and the allied-soil test asked by where a division is bound (section 37; `agents.md`, section 8.2), Slovakia's as the original's, and a reserve's strength share of its reduced most (section 38), Germany's garrisons of August 1938 as the original's, and a puppet's, an ally's or an access-giver's ground entered as the original's (`movement.md`, section 3), Japan's divisions of April 1936 marching through Manchukuo, and a load's agents bound in its first pass and running their land block in the second (section 39), Italy's garrisons of August 1938 as the original's - entering 165 of 167, 106 of 110 and 73 of 73, with 5, 4 and 2 of ours alone, and over 48 hours 211 of 299 with 154 of ours alone, 147 and 188 before - **the original's own attacks on entering vary from run to run**: the same save entered twice gave 1750's odds 1.890 and 1.395 and sent the 81. Infanterie-Division there in one run only, its weights and bars OpenHOI3's in both (section 40), so the attacks on 1750 the series has in its first hour are that run's - and a fleet holding a transport sent on no patrol or intercept (section 42), the fleet orders ours gave on entering and the original did not 1 and none on the saves of 1941 and 1938, 7 and 2 before - and the sealift run on entering by its agents' flag, its embark port by the area's order and its embark kept by where the troops are bound (section 43), Australia's cavalry and transports as the original's, and the theatres refreshed on entering, the provinces a country holds placed in them (section 44), China's, Mongolia's and Britain's headquarters standing as the original's - entering 166 of 167, 106 of 110 and 73 of 73, with 1, 4 and 2 of ours alone, and over 48 hours 212 of 299, and a change of stance releasing the reserve (section 45), Japan's held-back divisions of August 1938 the original's - entering 107 of 110 with 2 of ours alone, and over 48 hours 215 of 299 - and **why the original's attacks vary**: the part of each unit's modifier the AI keeps between the unit's hours is never initialised, so a loaded unit's first estimate counts it or nought by what memory held (section 46; `moving.md`, section 11.4; `docs/compatibility.md`, entry 24), which is the Japanese attack on 9381 and the odds on 5596 that August 1938 differs on - ours counts it always, and nought for every loaded unit, tried, gave 110 of 110 there and 16 of ours alone in July 1941 - and a headquarters trespassing left on its way and one the sealift has claimed kept off the list of those pulled in (section 48; `agents.md`, sections 5.1 and 6.4), Guangxi's and Italy's corps of April 1936 the original's - entering 165 of 167, 107 of 110 and 73 of 73, with 1, 2 and none of ours alone; the one lost, Britain's 3rd Corps, was a hold list one province longer, as the original's, whose eighth is the original's Yemeni 9768 where ours had 9664 - a puppet's unguarded provinces are taken province by province since (section 52), and the 3rd Corps is the original's - and the recall from the reserve in single precision (section 49; `moving.md`, section 2), Smolensk HQ's 23 held back the original's in order - entering 165 of 167, 107 of 110 and 73 of 73 with none, 2 and none of ours alone, the two 1941 misses the 3rd Corps and the 61. Infanterie-Division's attack, which the original's second run kept on its way as ours does - and the sea lift weighing a unit by the brigades that count towards it (section 50; `moving.md`, the sealift), Italy's 2nd Navy left in port as the original's, the fleets ours gave alone entering July 1941 1 - and a loaded join_air dead to the AI as the original's, its target not restored where the AI looks (section 51; `agents.md`, section 5.4), the air orders entering August 1938 2 of 2 - and the two fleet orders still apart entering July 1941, Britain's 23rd Navy and the Soviet Baltic Fleet, both a patrol's centre the original draws on the C runtime's `rand` of the thread that ran the country, the main thread's stream for both (section 53; `moving.md`, the fleets), which nothing here repeats - and **the attacks over the 48 hours**, the attack's every target, odds and joining unit logged on the running game (section 54; `moving.md`, section 11.2): a joining unit supports rather than marches where nobody else there can stand in for it - one already joined to the attack counting for nobody - where its province draws on any depot, not only where it is one, and where it does not stand in a target area, so the third of three German divisions at 1576 supports the attack on 1577 and a lone British division beside broken Italian defenders holds back, as the original's, the 12th at 5506 and the 4th at 5484 - over 48 hours 221 of 299 the same and 110 of ours alone, where 214 and about 142, the attacks of ours alone 10 where 16 - the 1750 and 2390 the original's run varies on, the burst near the 46th hour, and the 197. Infanterie-Division on 2102, an attack both make of the units on their way there, which the two runs differ on; entering unchanged, 1938's two of ours alone gone - and the crowding less `unit_cooperation` (section 55), which brings ours' attack on 2390, the one this run of the original stopped at entering, forward from the 11th hour to the 5th, so the 48 hours give 220 the same and 146 of ours alone, every one of the rise after that hour - and a burst at the 46th hour after a battle ours ended an hour before the original (the series does not restore the generator) - 190 before the fleets' war nobody declared and their forced pass were mended as read, which also took the fleet orders ours gave on entering that the original did not from 24 to 7 (section 25), and the air rebase's crowding counted over every air unit based at a base, which took the air orders ours gave on entering that the original did not from 18 to 4, the original's 5 all the same (section 26), and then the air base lists' overlord and puppets and the triage's two pairs of thresholds, which took them to none and the 48 hours' air orders of ours alone from 20 to 11 (section 27) - the Soviet front's one division of the reserve, and the fleets' and air units' orders. **A pair of countries' military access is read from a save and written since** (`savegame.md`, section 2 lists what a load reads).
   **From a new game** (since 2026-11-12, `probes/hoi.lua`'s `hoiNewGame` and hourly saves): the line's reach tests ask whether the
   areas are joined, as the original's (`moving.md`, section 10; `reversing/findings/FINDINGS-liveread.md`, section 60), and Italy's line
   of the second hour is the original's move for move, Tevere pulled to 9960; on 4 January 857 of 908 divisions where the save has
   them and 450 of 542 bound for its province (854 and 447 before), entering and the 48 hours unchanged, 217 of 299 and 135 of ours alone.
   The sealift's pick is the original's score for score (section 61) and its vote walks the original's hash map (section 62).
   Since 2026-11-13, with a join_air ending at its range (section 67), the hold list's weekly rebuild on the 1st, 8th,
   15th, 22nd and 29th (section 68), the peace-time +20 spread in single precision (section 69) and the garrison dropped
   scored by the garrison targets (section 70): on 4 January 892 of 908 divisions where the hourly saves have them, 902
   move or stay as theirs, 499 of 541 bound for their province, and every air figure theirs. With a dropped garrison
   taken out by the original's swap with the last entry (section 71): 896, 905 and 529 of 541; with the naval body's
   handled fleets taken off its ready list the same way (section 72), 906 and 530; with every province's terrain the
   running game's (section 74 - counted round the default text point two pixels south of the centre, which also leaves
   the weather check agreeing in every province), 897, 906 and 533; with a unit already redeploying given a new
   redeployment when sent far off (section 75), 901 placed; and with an inactive plan's powers made again from its order
   of battle (section 76), the plans' own power 93 of 106 where it was 44 - what still differs is shared by both of the original's
   runs, and the 1 Moukogo Kiheishidan's place is the new game's weather, a front over it that the original has not. **Britain's fleets are the hourly saves' since section 77** - a joining fleet aimed again only at midnight, and the
   fleet joined put on the handled list before its joiner - every country's fleets 101 of 101 and every theatre's 106 of 106.
   **Three more differences are the original's own run-to-run spread**, read on the running game (sections 75 and 77): its
   new game's weather differs between its runs in half the provinces, so the 1 Moukogo Kiheishidan and the Superga, slowed
   by a front ours has, are not faults here; and the Dutch East Indies' second sealift target came out 7780, 7744 and 6434
   in three runs. With a ship weighed once in the naval score and a country's own fleets handed out within the same reach
   as the enemy's (section 78), the plans' `min` 105 of 106 and 106 of 106 against the played save - the one left, Japan's
   Kwantung Army, is the hourly run's own: run B and a third run have ours. With a unit lent out counted by neither country
   for its officers (section 79), the plans' own power 96 of 106; with the AI's promotions for its leaderless headquarters
   (section 80), the enemy power 99 of 106. **What is left of the 4 January check is accounted for** (section 81): the
   sealift plans apart are the original's run-to-run order of areas, Italy's intercepts an hour early are the played run's
   own, Japan's air groups' fuel is the province's pool (about 0.05 fuel less at 4986 before the midnight draws, the daily
   pass's), and the last own and enemy power figures are organisation a fraction of a percent apart.
   **The plans' embark and debark that still differ vary with the original's own run**: two runs of the same new game gave
   Australia, Republican Spain and the Dutch East Indies other targets or another order of votes, and Japan another port;
   a third run, watched live, gave Australia a third order of its sealift's areas and a third debark (section 71) - so its
   1st and 4th Australian Divisions, apart from both saves, are the original's spread and not a fault here.
   **The 48 hours after July 1941 are set beside three runs of the original since 2026-11-14** (section 82): they give
   each other the same new way for 228 land units, another for 18, and one run alone gives one 105 times, so a difference
   is ours only where all three agree. The first found was the reorganisation's: a unit with a sealift `target`, or under
   one, keeps its commander, and a command is scored by how far the unit is beyond *its* reach - Italy's army group no
   longer walks to two armies the original never gives it, nor do Canada's, Spain's, China's, Brazil's, Argentina's or the
   United States' headquarters move; ways no run of the original gives 84, mostly the Soviet Union's and Germany's.
   With the friends told when a country's theatres are made or refreshed, and an agent's full pass asked after its area
   (section 83), a province changing hands sets off a full pass that hour for every friend near - Barce's capture moves
   Bulgaria's divisions in Serbia as all three runs do - and the ways all three give that ours did not fall 12 to 5. What
   is left is mostly downstream of a battle at 2390 that ours wins at hour 45 (an attack at hour 5 at odds just over the
   bar) and of Sinkiang joining the Comintern at 18:00 on the second day, which is the install's Lua drawing its
   invitation (`math.random(10)`) where none of the original's three runs drew it.
   **A new game's first month is set beside two runs of the original saved daily since 2026-11-14** (section 84): they
   agree on 902 of 908 divisions' places on day 7, 889 on day 14 and 856 of 892 on day 30. The first shared difference
   was France's two sealifts, which waited at their ports: a fleet on a plain way is sent to embark, and the AI's moves
   and fleet missions are held to no range - the original asks range only where the player orders. Divisions placed as
   the runs' on day 14 went 857 to 869, on day 30 825 to 831 and 834 to 850; Italy's divisions in Italy are now the most
   of what both runs share and ours misses. The agents check's `places=FILE` writes every division's place and way for
   setting two runs beside ours.
   **A war's start is set beside two runs of the original too** (section 85): 48 hours from `Ireland1939_09_06_18`, Germany
   six days into Poland, where the runs agree on 122 new ways and one alone gives 34. With the uncovered count leaving out
   the province attacked, ours gives 77 of the runs' shared ways where it gave 54, misses 33 where it missed 54, and gives
   46 of its own alone where 52 - still the furthest of the three tests from the original, and the next place to look.
   With a change of stance scoring the area at the stance it leaves (section 86), as a load's entering does it, ours'
   ways alone fall to 37 there and to 77 in the July 1941 series, where they were 102.
   **Its march speeds were never the difference** (section 88): the `march` check's `loaded` switch takes each division
   as the save loads it, and 57 to 60 of about 60 an hour agree where a new game's brigades gave 45. Two things were:
   a battle whose defenders have all gone picks no tactic, so its attacker walks at the plain combat pace; and the AI's
   move asks the click's test first, so a unit marching into a battle is neither halted nor pulled back - Poland's Armia
   Modlin kept on its attack. With both, ours gives 105 of the runs' shared ways, misses 14 and gives 26 of its own
   alone, where it was 94, 24 and 36; entering, 4 January and the July series are unchanged by them.
   **Britain's Royal Marines are the original's since** (section 89): the invasion ranking's staging areas - Germany's
   capital area failed for touching France's, France's ranked at -1926 - and the planner's friendly branch send the 1st to
   Newcastle for Cherbourg; a division boards at the embark port the hour it arrives and the transport sails behind the
   load; a unit at sea may not be shipped at war, so the 2nd fills Lerwick. The July 1941 series' shared ways went 186 to
   215 and its own alone 114 to 78. At the war's start the staging draw shifts the AI's generator, a German diplomacy
   change remakes Germany's theatres and a western one neither run makes moves its divisions: Germany's ways alone 17 to
   46. **That remake is the original's** (section 90): its own build from nothing, called on the running game at the same
   hour, makes the same four theatres province for province; what neither run had is the trigger - Britain's
   invitation to Australia, which the install's Lua makes only on a one-in-seven draw of the AI's generator.
   **A finished battle's toughness stays in the odds** (section 93; `moving.md`, section 11.4): the defence product its
   last hour made is never cleared, so four German divisions whose attack on 2390 is over still give odds of about 10 at
   noon on 16 July 1941 and the 6th H Arm Division keeps its way to 2448, as in all three runs; ours, counting toughness
   only while a unit fought, had fallen to 2.8 and sent it in. Against the July series' three runs ours' ways alone went
   75 to 58; at the war's start 110 to 113 the same, 9 to 6 missed and 45 to 42 alone. The same save's first odds on a
   loaded battle differ from load to load in the original - section 46's uninitialised cache - so 2390's attack is
   called off at entering in one recorded run of three.
   **A province is covered against a unit merely passing through** (section 94; `moving.md`, section 11): the original's
   count of units moving into a province asks where each one's way ends, not its next step, so the Soviet 5th Army
   crossing 1351 for 1530 leaves it covered and the 5th H Arm Division goes into 1395 at 21:00 by a plain move, as in all
   three runs. July: 216 the same, 57 alone, 4 none; the war's start 114, 5 missed, 43 alone.
   **The hour walks a country's units in the order of its own list** (section 95; `movement.md`, section 6): the save's
   tree children first, as ours already kept for reinforcement, where the hour had gone by number - so the 181.
   Infanterie-Division, 31st, looks at its support attack on 1750 before the divisions that start that battle and gets
   its way an hour later, as in both runs. Every battle's rolls fall otherwise after it, and the series moved with them:
   July 216 the same and 59 alone, the war's start 109, 12 missed and 45 alone - the misses a battle at 2213 that ours
   still fights at its 28th hour where the original's was over before its 18th, so 2213 falls later and Poland's
   agents redo their area six hours late.
   **An invasion planned for an hour takes its division out of its theatre's lists** (section 100; `moving.md`, section
   16): the Soviet Union lands the 195. Strelkovaya Diviziya at Trondheim at 04:00 on 17 July, which keeps it out of
   Smolensk HQ's split at 06:00, so the 173. is not dropped and sent to 1577. Ours planned nothing, for three reasons now
   mended: a candidate's spare is worked out again whenever its area differs from the unit before's, by that unit's
   agent; an area's enemy strength is brigades in thirds weighed against our friends' (`CAreaEnemies`, also an
   enemy-held area's need), where ours counted units, headquarters too; and the ranking's score is the original's in
   single precision, with its 30 for a resource and its twenties. July 219 the same, 1 missed, 53 alone.
   **A halted division still answers its order's province** (section 101; `moving.md`, section 10): the AI's halt posts
   its "no order" as a command, so until it runs the original's destination reader answers the old order's province -
   the Soviet 99. Strelkovaya Diviziya, halted at 144 at 06:00 on 17 July on its way to 1589, is seen bound for no
   position and pulled to 619, as in all three runs. The 78. and 117. Strelkovaya Diviziya at 10:00, next on the list,
   follow from 06:00, where the original's own runs part over the 176. Strelkovaya's way (1395, 2636 or 2390); no rule of
   ours was found wrong there.
   **The later-starts commit (`ba1b07b2`) moved the July series by a draw, not a rule**: from 219 the same and 53 alone
   to 189 and 104, through Italy's Cdo. Sup. Metropolitana sending its reserves to the Soviet front from 17:00 on 17
   July. At 15:00 Germany's foreign minister calls Italy into its war - against Mongolia, Their Finest Hour's `GER.lua`
   calling Italy against anyone but the Soviet Union once Paris is the Axis's - and Italy, of its faction, joins; its
   strategy made again takes Germany's eastern front into its home theatre's area. That step runs on the script's
   `math.random(6)`, which every script here draws on in turn (`lua.md`, section 3); the commit changed how often the
   scripts draw before that hour, and the roll that missed now hits. The original's three runs miss it, as two days'
   rolls at one in six will two times in three, and its rolls cannot be set beside ours. Nothing to mend; the series is
   set beside main from here: July 190 the same, 25 other end, 109 alone; the war's start 109, 12 missed, 45 alone.
   **Where a unit is bound is two readers** (`moving.md`, section 10): the pending one, which the attack and the line's
   look at its committed divisions ask, counts a division waiting on a support attack as bound for the province it
   supports; the movement one, which the pull, the count of positions, the headquarters, the detach, the sealift and an
   invasion's march ask, answers nowhere for it. Ours had one reader; given the support attack's nowhere alone, Poland's
   attack on 1749 lost its 30 Dywizja Piechoty, fell to odds of 0.04 and was called off at the war's start, which neither
   run does. July 102 alone, the 1. Gebirgsjäger-Division at hour 29 gone; the war's start, entering and 4 January
   unchanged.
   **The usage commit (`993908c0`) moved the series** to July 212 the same, 5 other end, 125 alone, 4 none; the war's
   start, entering and 4 January unmoved.
   **The line's rest gives every position one** (section 102; `moving.md`, section 7.3): where the share gives nothing
   out, the original gives two only where a unit there holds an entry on a list of its own, which no unit in the running
   game held - not where it is dug in, as ours had it. So Iraq's 1. Infantry Division is no longer pulled to 5463 at noon
   on 17 July: July 124 alone, nothing else moved.
   **The home production commit (`b68289c0`) moved the series by the draw again** - Germany's call of Italy came up at its
   first tick instead of its second, July 143 the same and 118 alone - and so **a run of the original's is now played back
   draw for draw** (`docs/replay.md`, section 9; section 103): recorded on the running game with the fact base's
   `probes/hoidraws.lua`, and `OpenHOI3.SaveCheck load ... series=... draws=FOLDER` puts the original's generator back each
   hour and answers every country's AI draws, on the AI's generator and in `math.random`, with the original's for it.
   Against that one run: 267 the same, 59 alone, 21 none, 6 other end, where without the draws 186, 128, 27 and 81 -
   Italy's join gone with the roll. The kit's series against the earlier runs still drifts with every commit; measure
   against the recorded run with its draws from here.
   **Each country trades once a day** (section 104; `trade.md`, section 9): the original hands the hour's trading a 24th of
   its list of countries by the hour of the day, so each trades at one hour, where ours rolled for every country every hour
   - about 180 draws an hour where the original makes 4 to 8, and the scripts' trading some 25 times an hour. Built: the
   draws line up with the recording's, the units' orders unmoved, and the trading's share of an ordinary hour 11.3 ms to
   2.5 (`CLAUDE.md`, the profile).
   **Where ours drew beyond the original** (section 105), the playback naming each extra draw's caller: a patrol's centre,
   drawn on the AI's generator where the original draws on a thread's C runtime `rand()` - now a `rand()` of its own,
   its lists swap-removed as the original's - and Italy's invasion ranking, 24 areas scored where the original scores 5,
   a fellow fighter's port counting only where an own port reaches it (`moving.md`, sections 14 and 16). **The war's
   start is recorded too** (`wz`): with its draws, 128 the same, 10 alone, 6 none, before and after; without them the
   patrol's change had moved it by a roll.
   **The ranking's staging step was right**: the original draws it inline, past the recorder, which now logs that one too
   (section 105); a July recording with it (`wr4`) has every draw of the AI's generator, and ours's staging draws match.
   **From 2026-11-15 to 2026-11-18** each difference against a recording was read and built, liveread sections 106 to
   126: what a load draws before its first hour, a lent unit's officers, the scripts' first day after a load, the line's
   pull counting its picks on a count of its own, the countries' passes racing, the patrols' end and `rand()`, a way round
   a known enemy stack, the submarines' raid, a neutral's raided waters, a loaded game's first midnight, a redeployed
   brigade's organisation, falling back's stop, a raid's convoy ship, the country's day one country at a time, a province
   lost and a redeployment into an enemy's province, a support attack binding nothing for the rest of the pass, a fleet
   under bombs seen with no roll, and a battle started by the asker's order (`moving.md`, section 10; `navalcombat.md`,
   section 1). The recordings grew probes as they went (`wr5` to `wr19`: draws, the game generator's callers, every AI
   move with its caller, the reserve's picks, the sighting calls, the caches).
   **Then sections 127 and 128** (2026-11-18): the paradrop planner and its daily list of weak enemy ports, an airborne
   army staged at its target's air base and a plane's passenger free to walk (`moving.md`, section 9.1), and a fleet the
   naval body rebases free for nothing else that pass, its orders refused for no range (section 14). Hour 41's weather was
   a name the comparison had not mapped, and is mapped in the kit's `runcmp.py`. **And section 129**: the AI's naval strike
   covers every sea in reach (`moving.md`, section 17.4). **And section 130**: the split's blocks taken in the order their
   region groups were made, standing in for the original's, which follows the heap and differs from run to run
   (`moving.md`, sections 7.2 and 19).
   **Then sections 131 and 132** (2026-11-18): a strategic redeployment's supplies (`upkeep.md`, section 1), which had
   pushed Moscow's stockpile over the line the production script shuts supplies off at, and the submarines' raidable
   tonnage counted over every step of a convoy's way (`moving.md`, section 14), which had made the Soviet raids one draw out.
   **Open, in order**, measured against `wr19` (48 hours from `Ireland1941_07_16_07`, every probe): 468 of 469 land orders
   the same, none of ours alone, fleets 163 of 165, air units 25 of 26; `wr15` 467 of 467 (one of ours alone, that run's
   original keeping the 3. Infanterie-Division off to 38) and fleets 162 of 164, `wr14` 466 of 484 and 159 of 163, `wr11`
   453 of 488 and 146 of 167. **Every hour of `wr19` agrees draw for draw before its first battle, and no battle's dice
   part in its 47 hours**, so what is left of it is orders no draw shows - and the AI's own draws, which the playback
   counts by country. **Every `math.random` draw of `wr19` is the original's since 2026-11-19**: Cuba's and Haiti's two
   beyond at hours 27 and 31 were a puppet's debt revoke ours let through, which cost Britain the influence that pays
   its upkeep on the Dominican Republic, whose influence modifier then went and with it the friend Cuba's and Haiti's
   intelligence ministers saw (`aid.md`, section 1). Open: (a) New Zealand's and France's patrol centres at hours 6 and
   29, and the Soviet one left at 34 - a hook naming each `math.random` caller is in the session scratchpad as
   `dbg7-luarand.patch` (`OHDBG_LUARAND=TAG`); (b) the fleets and air units ours orders otherwise -
   the British 31st and 41st Navies, the German 33. Unterseebootsflottille, 5th and 6th Navy, the Soviet Cernoje Flote,
   the German 26th Air wing and the Omani dive bombers; (c) `wr14`, whose dice part at hour 13, and `wr11`.

**Noticed in testing, 2026-11-12, and all mended the same day**: held keys, the number pad's Enter, the
right button, the opening view, the alert icons, the counter distance, a held arrow key's lag, the names of provinces
`positions.txt` does not place (`mapdraw.md`, section 4), the zoom's ease (`mapunits.md`, section 7) and the lend-lease
slider held to its limit (`sliders.md`, section 2).

As of 2026-10-19, stage 5's military AI built through its third piece, moving and attacking (the last paragraphs before *Loose ends*); before that, in stage 3 with stage 4 built as far as items 6 to 10 cut it - events and decisions, diplomacy through factions, politics and espionage, each in four pieces, and triggered modifiers, with occupation and revolt, the last of stage 4, built in its four pieces - and the economy of stage 2 next, buildings in the build queue first. Kept
current as work lands, so that picking this up cold needs nothing but this file and
`docs/mechanics/`.

The units exist (`docs/mechanics/units.md`) and are kept: they draw supplies, lose and
regain men, and are re-equipped (`docs/mechanics/upkeep.md`), and their organisation is
held and returned by how many officers their country has
(`docs/mechanics/organisation.md`). More are built: the queue is in, with the thirty units
the orders of battle start under construction (`docs/mechanics/buildqueue.md`), and the
four windows a unit is ordered from (`docs/mechanics/builders.md`). And they are on the
map: a counter each, and a menu that puts a finished one there
(`docs/mechanics/mapunits.md`). And the land units move (`docs/mechanics/movement.md`),
a selected unit has its panel (`docs/mechanics/unitpanel.md`), and fleets and air units
are given their orders (`docs/mechanics/fleetsandair.md`). And land units fight
(`docs/mechanics/landcombat.md`), with the battle on screen
(`docs/mechanics/combatview.md`). And they are supplied along a network
(`docs/mechanics/supply.md`): every province draws on a depot, supplies and fuel go out to
it province by province, and a unit draws on the province it stands in. And across the sea
by convoy (`docs/mechanics/convoys.md`), with a map mode that shows how it all stands
(`docs/mechanics/supplymap.md`). And fleets fight one another at sea
(`docs/mechanics/navalcombat.md`), and have their missions, the raid on convoys among
them (`docs/mechanics/navalmissions.md`).
In order:

1. **Air and naval combat**, which is what stage 3 has left. Cut in five on 2026-10-10 -
   **the cut is this project's: the go-ahead was given, and the cut has not been reviewed**:

   - **the battle at sea - built** (2026-10-10, `navalcombat.md`): fleet against fleet;
   - **a fleet's missions - built** (2026-10-10, `navalmissions.md`): reserve, patrol,
     intercept, sortie, the raid on convoys and their escort, and the battle a raid fights.
     **Transport and invasion are built since 2026-10-15**, with carrying (`transport.md`);
   - **the battle at sea on screen - built** (2026-10-10, `combatview.md`, section 10): its
     row in a fleet's panel and its window. **It was built with the land battle's marker on
     the map too, and that was taken away the same day**: the reading for the air side
     found that the original's marker is a land battle's alone. **The marker was there
     when it was reviewed in play, and the change has not been raised for a decision.** This piece was put here on
     2026-10-10 - fleet missions first, then the combat screen, then the new game's two
     days - ahead of the two pieces below. **Reviewed in play that day: "The sea
     battle window looked perfect"**; no picture of the original's was measured against it;
   - **the battle in the air - built** (2026-10-10, `aircombat.md`): air superiority and
     the intercept, a mission's cycle from its base and back, what a country knows of a
     province, and the four hours an air battle lasts. **Read out of the original and set
     against nothing**, and it has not been reviewed in play;
   - **bombing - built** (2026-10-10, `bombing.md`): ground attack and interdiction, the
     four strategic kinds, the strikes at ships and at ports, anti-aircraft fire, what a
     bomb does to a province and what comes back each day. **Read out of the original and
     set against nothing**, and it has not been reviewed in play. **A carrier's aircraft
     came later**, with carrying by air (2026-10-16, `transport.md`, section 13);
   - **the battle in the air and the bombing on screen - built** (2026-10-10,
     `combatview.md`, section 11): the row in a unit's panel and the window, whose list for
     the bombed side of a bombing is the province's buildings, stockpiles and production
     that the bombers' mission bombs, each with a bar of what is left of it. **Neither has
     a marker on the map: the original gives none**, to them or to a battle at sea. **Read
     out of the original, tried with real clicks, and it has not been reviewed in
     play**;
   - **the rest of the air orders window - built** (2026-10-11, `fleetsandair.md`, section 4,
     and `aircombat.md`, section 2): the four shapes of a mission's area - point, region,
     circle, cone - with the three sliders, the right click that aims a circle or a cone and
     the shift that adds to one, the boxes of the hours, the two dates and "continuous", the
     readout of the day, the ladder that ticks the first row, what it remembers for the
     next, and the map's colours while it is up. **Read whole out of the original, tried with
     real clicks, and it has not been reviewed in play.** Nine things in it are the
     original's and look odd, and are built so (`fleetsandair.md`, section 4, and below),
     none raised for a decision: the priority is shown and never sent; a slider must move a twentieth
     before it counts; the figures show the sliders and not what a right click aimed; a cone
     loses its part west of north when it starts below north; clearing the ticked box of the
     hours leaves none ticked; the hint under the sliders is empty until accept is first
     greyed; the second rung of the ladder replaces the first; a month on keeps the day and
     runs past a short month; and the title stays when a right click moves the window;
   - **the air map mode - built** (2026-10-11, `airmap.md`), the last of the air side on
     screen: grey, green where the player's intercepts reach and red where its air
     superiority does; a ring round each of its air bases for each range of aircraft there,
     red to green by their strength; the air bases and anti-aircraft guns as large pictures
     by their level, the large air base clicked before any counter - which is how an air unit
     at its base is picked in this mode; the three icons of the week's air battles and
     bombings; and the province's two lines. Under it, **a list of finished battles that
     keeps a week**, every battle of every kind going on it as it ends (`CCombatHistory`).
     **Read whole out of the original, tried with real clicks, and it has not been
     reviewed in play.** Four things in it are the original's and look odd, and are built so
     (`airmap.md`), none raised for a decision: its colours are not made again each day, only when the
     player's fighter orders start or go; a count of battles is a signed byte, so 128 in a
     week hide the icon; a beaten side is named by nobody; and an air base the player knows
     little of is drawn at the size of a level 1. Reading for it corrected the fact base
     twice. `Tab` reaches it after supply, and `--mode=air` starts in it.

   With that, **stage 3 has what it set out to have on the air side**; transport and
   invasion, and a carrier's aircraft, came with carrying (2026-10-15 and 2026-10-16).

   **Bombing** (`bombing.md`): a unit on a bombing mission flies out to a province of its
   list, bombs it for four hours and is told to return the hour after; home, it flies again.
   **A bombing is a battle of its own kind**, one to a province - of the ground, of land
   units or of ships - and a unit in one is not "fighting": a division under bombs marches
   and attacks as before. Each hour every wing fires at one target its mission goes for and
   then **bombs the province**: whole runs of its strategic attack, never fewer than one,
   each a die's worth off the buildings, the stockpile and the day's production its mission
   names. The province fires back with its guns and with one and a half times every friendly
   unit's air attack. **A building now has two levels** - what was built and what works of
   it - and a province's IC, roads, guns and radar are made from the second; what was bombed
   comes back a little every day, faster on good roads (`CProvinceRepair`).
   `OpenHOI3.SaveCheck bombing` flies one with the install's own units: four German wings
   over Poznan leave a fifth of its one factory after the first sortie and a tenth after
   three days. **Nine things in it are the original's and look odd, and are built so**
   (`bombing.md`, section 12), and **none has been raised for a decision**: a bomber with no
   leader finds a ship one time in a hundred, so strikes at ships wear the port and hardly
   touch the fleet - ten in a hundred more for each point of its leader's skill; a fleet in its port fires its air
   attack three times over; two units cannot bomb one city's ground together; interdiction
   flies only at ground its country sees into - since 2026-10-14 the land along its border and its units'. In the
   client `--mission=GER:strategicbombing:1928` gives a country's air unit a mission, and
   the orders window gives all eight for the province clicked.
   **Reading for it corrected the air battle**: the hour in which a unit out of a battle
   starts no other ends with its first move, so one that flies on may fight at once where it
   arrives; and it named the radio's and the radar's technology keys, which are now in an
   air battle too.

   **The battle in the air** (`aircombat.md`): an air unit under air superiority goes round
   the provinces of its mission and flies at the strongest enemy air unit it knows of in
   them, one province ahead of it; one under an intercept waits at its base, watches its
   provinces and two steps around them, flies to meet the strongest enemy on its way in,
   and turns for home when it has caught it. A battle begins when a unit under either order
   finds an enemy air unit where it has arrived or stands - **anything in the air may be
   fought, and a unit sitting at its base only by a fighter and only while it is itself fit
   to fly**. Each hour every wing fires its `air_attack` in shots at one enemy wing, about
   half hit, and a hit costs a little organisation and less strength; after four hours it
   is over, a side worn to nothing falls back, and the units have an hour in which they
   cannot be fought again. A mission outlives its sorties: home under "return to base" a
   unit goes out again until its end hour. `OpenHOI3.SaveCheck airbattle` flies one with the
   install's own units: three wings of German bombers over Warszawa fight Poland's fighters
   ten times in two days and turn for home at two thirds of their strength, and the Poles
   catch them eight times more on the way.
   **Eight things in it are the original's and look odd, and are built so** (`aircombat.md`,
   section 9) - a beaten unit beside the sea flies out to sea, one too worn to fly cannot be
   attacked at its base and yet takes off every third hour, a wing's defence and its
   `air_defence` do nothing. Of that last, settled on 2026-10-10: "Yes thats a bug in
   the original"; the others, not known offhand, were to be noted - so
   they are noted there and nothing is changed. In the client `--mission=GER:airsuperiority:1928` gives a
   country's air unit a mission, and the orders window gives both for the province clicked.

   **The new game's two days - built** (2026-10-10, `tick.md`, section 6), by the same
   decision. A new game no longer starts at its history files: as it is made it runs what the
   original runs before a game's first hour - a day, the filling of the provinces, a day, a
   month's pass and one hour, the clock standing still and the game not yet "running", which
   is when a brigade may have its own figure of organisation and nothing else, nothing
   decays and a convoy is manned for nothing. So two days of production, research, building,
   upgrades and rotation are behind the first screen. **Checked like for like, it is the
   start save's Germany**: given the laws that country's days were run under, every one of
   its 172 brigades, ships and wings has the save's strength and organisation, its manpower,
   officers, influence, abilities, technology and build queue are the save's, and six of its
   seven goods to the thousandth - supplies are 0.030 over in 8410, and were 3.3 under
   before what leaders do to a unit's use was built.
   **What a player gets is still not what the original's first screen shows**, and is not
   meant to be: there its AI has changed laws by then, the player's among them, and here
   the player's country keeps its history's (`docs/compatibility.md`, entry 6).
   `OpenHOI3.SaveCheck firstdays` sets the two side by side.

   **The battle at sea** (`navalcombat.md`): a fleet of a country at war looks each hour
   at the first enemy fleet in its sea province and rolls to see it, ship by ship; a
   sighting starts a battle and every other fleet there joins the side whose enemies are
   its own. **Each ship stands at a distance of its own** - two ships reach one another
   when their two distances together are within the shooter's reach - and each hour it
   picks a target by its side's positioning, fires, and closes the distance or opens it.
   Damage is two dice over the target's hull; a ship has no defence but that. A side
   whose ships average under one organisation has lost and its fleets run for the port
   they lie off; a battle nobody loses ends when the hourly roll to break off, which
   gets likelier by the hour and much likelier at night, has set the fleets counting and
   then passes again; and a fleet told to leave counts four hours before it goes.
   **Read out of the original and set against nothing**, like the land battle: no save
   has one. `OpenHOI3.SaveCheck seabattle` fights one with the install's fleets:
   Germany's battle fleet gives way to the Mediterranean Fleet after 17 hours and falls
   back to Wilhelmshaven, one British destroyer sunk; a Japanese and an American
   squadron break off after ten hours, two cruisers down. **Reading it found three things
   nobody had written down**: every ship has its own distance and closes it at a quarter
   of its speed; a ship that has lost the enemy shoots at its own side seven times in a
   hundred; and a fleet cannot be taken out of a sea battle in its first four hours.
   **Two things about it are the original's and look wrong, and are built so**: an idle
   fleet of a country nobody plays starts no battle, and a crowded fleet's stacking
   penalty leaves its ships hardly ever finding a target. **There is no AI**, so no
   enemy fleet leaves port: `--sail=ENG:10531` on the command line sends one to sea, and
   `--war=GER:ENG` with it gives the player's own fleet something to find. **On screen**
   (`combatview.md`, section 10) a battle at sea has the land battle's row and window - and
   no marker on the map, which is a land battle's alone - the window with halves of its
   own: each side's positioning, and a list of its
   ships whose pictures stand along their rows by each ship's distance, so that the two
   fleets are seen closing across the gap between the lists. Built from a reading and
   tried with real clicks; nothing of it was set against the original.

   **A fleet's missions** (`navalmissions.md`): an order is asked every hour whether it is
   still going, and that is where a mission does what it does. A **patrol** goes round
   the sea provinces beside its own, shadows a foreign fleet it sights - which makes that
   fleet twice as easily seen and, from the day's turn, the water known without a roll -
   and every twelve hours tries the convoys where it is. A **convoy raid** is the same
   round with no shadowing, and a raider starts no battle between fleets. An **intercept**
   waits in port, sees an enemy in its province or beside it without rolling, and goes
   for one no stronger than itself. A **sortie** sails out and turns for home on
   arriving. An **escort** sails up and down the stretch of a convoy's route within its
   range. A **reserve** lies in port and once a day gives its sound ships for the worn
   ones of the fleets beside it. A fleet of the player's too worn for its stance makes
   for the nearest harbour. **A raid on a convoy is a battle**: the convoy's escorts and
   transports become a throwaway fleet of destroyers and transport ships, five rounds of
   the battle at sea are fought on the spot, and what that fleet lost comes off the
   convoy - which then wants escorts, is given them, and keeps out of that water.
   **Read out of the original and set against nothing.** `OpenHOI3.SaveCheck raid` sends
   the install's raiders at the install's convoys: Germany's battle fleet in the Channel
   Approach sinks 37 British transports in a month. **Six things in it are
   the original's and look wrong, and are built so** (`navalmissions.md`, section 12) -
   the plainest that **an interceptor goes out once** and then lies in port under its
   order, and that **a patrol sinks convoys**. Settled on 2026-10-10: "All those
   things are correct, and they may be bugs, but thats ok." In the client `--mission=ENG:patrol:10531` gives a country's fleet a
   mission, there being no AI to.

   Supply came before it, cut in three on 2026-10-09, and **all three
   pieces are in** (2026-10-10). **It was reviewed in the game the same day** and nothing
   was raised against it.

   **The network, built**: the land cut into areas - what one country controls, touching,
   each province with more than one level of infrastructure - areas on one side grouped, a
   depot picked for each group, a depot found for land its group gives none, and every
   province's distance from its own; with what a province can pass in a day and what a load
   loses crossing it. **It agrees with three savegames province for province**: the same
   4,400 provinces out of supply and the same 402 that pass without limit
   (`OpenHOI3.SaveCheck supply`, `supply.md`, section 9). **And the original was watched picking
   a depot**: Manchukuo on Hamhung as the game starts, on Pusan with North Korea released,
   on P'yongyang with both - and the score gives all three. In the original a released
   puppet draws on its own capital for some days first; here it is in its master's group at
   once, and stays so on purpose (`docs/compatibility.md`, entry 9). An account given beforehand said that
   a puppet is in its master's network unless the two do not touch, and that allies keep
   their own; both were read so.

   **The daily pass, built** (`supply.md`, sections 11 to 17): once a day, before any
   country's own day, the provinces are gone through furthest from their depots first; each
   works out what it is short of fifteen days of what its units ask for, takes what the
   provinces nearer the depot held the day before, and passes the rest of its want inward;
   then what any province holds spare goes one province inward. **A unit draws on the
   province it stands in** - its share of what lies there - and on a friend's depot its
   country pays the friend. **A marching unit takes its fifteen days with it.** **A day's
   supplies and fuel are made where the factories are** and come to the stockpile a province a
   day. And **a new game fills its provinces first**: the pass run 251 times over, with the
   stockpiles put back each time, in about two seconds, between the two days it runs before
   its first hour. The readout of a selected
   province says what lies there, what its units ask for and what the day asked of it, and
   the unit panel's supply and fuel bars are real.

   **It reproduces the original's own figures**: with the units, laws and production of the
   reference start-date save, a day, the filling and a day give **184 of 234 networks the
   same in every province and every figure the save keeps** - Germany's 245 provinces,
   Sweden's 210, Turkey's 195 (`OpenHOI3.SaveCheck supplypass`, `supply.md`, section 17). It
   was 124 before the convoys and 171 before what leaders do to a unit's use.
   The others differ by what goes in: the original's AI has units on the move and has moved
   shares of IC. **Reading it found four
   things nobody had written down**: what a load loses is an amount and not a share; a
   marching unit carries its supplies; a puppet's stockpile drains into its master's
   network; and the original runs a day *before* it fills the provinces, so what that day
   made away from the capitals is lost.

   **The sea and the screen, built** (`convoys.md`, `supplymap.md`): a country's supply
   convoys are made for it - at peace one to every harbour of each block of its land across
   the sea, the depot's first - each finds its way by the original's search, wants so many
   transports for the length of its way and the size of the harbour it ends in, and lands
   each day what those harbours have room for; resource convoys bring home what cut-off land
   yields, which waits in the block's largest harbour; a country's transports and escorts are
   shared out among its convoys each day, supply convoys first; and a new game gives every
   convoy its ships for nothing and every country a free transport for each two of its base
   IC. **All 261 convoys of the reference start-date save come out of it**: the same runs
   with the same goods, the same way province for province, the same transports and escorts
   on each, and the same free ones left over for all 20 countries that have any. **Reading
   it found three things nobody had written down**: an area's harbours are kept largest
   first, so "the first harbour" is the largest; a convoy lands in harbours and the daily
   pass carries it to the depot; and the yield of land that draws on a depot in the capital's
   block counts as produced at home. **The supply map mode** colours the player's land green
   where it holds what was asked of it, yellow where it is behind, red where it is cut off,
   with red stripes on a road that is full - `Tab` goes round the political map, the terrain
   and supply - and the tooltip of the province under the mouse says what the original's
   does, line for line and in its colours. Nobody attacks a convoy yet, none is traded for or lent - building them came on 2026-10-15, `convoylines.md` - and there is no
   list of them on the production screen.

   **The areas were wanted by three older rules**, each of which had guessed at them: home
   production, the `overseas` modifier and where a finished unit may be placed. All three
   now use the area as read, and the save's home production of metal is right for 78
   countries of 78 where it was 76 (`supply.md`, section 10).

   Land combat came before it, in three pieces, and all three are in (2026-10-09).
   **A battle exists**: a land unit walking at a province an enemy stands in
   starts a battle for it and fights from where it stands; each side's units go on the front
   until the battle's width is used and the rest wait in reserve; each hour both sides
   shoot and the damage is settled; a front unit with no organisation left falls back; a
   side with nobody on its front has lost; the winner walks in and the province changes
   hands; and the attack leaves a delay of a week or so (`landcombat.md`).

   **And the hour's circumstances count**: fifteen things multiply into a unit's attack and
   defence each round - a river, a strait, forts, the ground, the night, the weather,
   experience, combined arms, dissent, lack of supplies, being dug in, crowding,
   envelopment, several battles at once, its country's cores - with day and night by each
   province's own clock and digging in a level a day (`landcombat.md`, section 4) - and since
   then the leader and his traits, the difficulty setting and, since 2026-11-10, encirclement.
   What still waits - shore bombardment and the rest - is listed there. **The weather's part is built, and the weather since 2026-10-15** (`weather.md`).

   **And it is on screen** (`combatview.md`): a battle the player has a part in has a round
   marker on the map, green, yellow or red by how it goes for him, with the attacker's share
   of the two sides' strength on it; the unit that attacks has the red arrow; its panel ends
   in a row for the battle; and a click on the row or on the marker brings up the battle's
   window, out of the original's `combat.gui` - the two flags, the picture of the ground,
   an icon for each thing in play, each side's tactic, and its units on the front and in
   reserve. **With the window came the tactics** (`landcombat.md`, section 11): each side
   picks one out of `common/combat_tactics.txt` by weight when the battle starts and again
   every day of it, and a tactic the other side's counters does nothing. Most of what tells
   one tactic from another asks for a leader, and there are none.

   **The fight was read and not watched.** None of the reference saves has a battle in it,
   and the original's rolls cannot be made again here (`landcombat.md`, section 8), so
   nothing sets a round against the original's hit for hit. **Five things about it were
   settled, all on 2026-10-09**: a battle of two like divisions lasting about a
   week "seems right"; an attack delay stops all movement but the walk into the province
   attacked, where a unit may arrive with it still running, which is what is built; occupied
   ground is striped; units that attack together stand on the front until the width is used
   and the rest are reserves; and one that joins later waits in reserve and comes up by its
   chance - which is how it was read. `landcombat.md` ends with what is not established, and
   `OpenHOI3.SaveCheck battle <install> [attacker] [defender]` fights one with the install's
   own units and says how it went: Italy fails across a river against Ethiopia at Gurbo in
   181 hours, Japan takes the town of Tianjin in 347, and a German division sent across the
   Rhine at the Maginot Line attacks at four hundredths of its strength.

   **What is on screen was set against the original**: three screenshots of a battle in the original,
   of the same day - the marker, the panel's row and the window. They corrected four things
   about how the interface draws, none of them about battles: a bar's picture is drawn
   upside down, a turned flag turns about its middle, a round shield cuts its flag to a
   disc, and a name too long for its box ends in three dots (`interface.md`, sections 5 and
   7). With those the window's upper half lies on the original's to within a pixel. **Ours was
   reviewed in play on 2026-10-09** - "Looks very good" - with the request that opening the window should not
   move the map, which the original's row in a unit's panel does (`docs/compatibility.md`,
   entry 8).

   **There is no diplomacy, so a war has to be given**: `--war=GER:POL` on the command line,
   which is `CStartWarCommand` and nothing like a declaration. On the start date only Italy
   and Ethiopia are at war. **And there is no AI**: the enemy stands where its order of
   battle put it.

   Movement, cut in three on 2026-10-09, was done before it as far as
   it goes without an enemy: the order, the route and the march for land units; the unit's
   own panel; and fleets and air units, with the window of orders that sends them, their
   range, the sea's own distances, and the port and the airfield a docked fleet and a
   grounded wing are picked by (`fleetsandair.md`). Every order of a fleet or an air unit
   but a move and a change of base is a mission against somebody, and waits for the battles
   at sea and in the air.

   **Fleets and air units have not been looked at beside the original.** They were built
   from the reading and checked with real clicks - a port clicked, a sea province right
   clicked, the window's row and `accept`, hours let go by - and their sailing, their
   route and the whole table of distances by sea agree with the original's saves and its
   own cache. What has nothing to check it but the reading and the art: how big a
   building's picture is on the map and from what zoom, the orders window beside the
   original's, and which unit of several a click on a port brings up first.

   **The unit panel was set beside the original on 2026-10-09**, one idle
   infantry division in both. It was built from the reading and checked with real
   clicks - a row of a unit above, an order and the button that halts it, the three
   checkboxes. A screenshot of the original mended three things, each now measured on it and none
   read: where a turned bar stands, that the panel's entries touch, and where the name
   sits in its box. `unitpanel.md` ends with what is still unseen - a headquarters, a
   unit on the march.

   **The march's speed is checked** against four saves of one reference game made
   on 2026-10-09 with units on the move: `OpenHOI3.SaveCheck march`, 301 of 301
   divisions to the thousandth over four hours (`movement.md`, section 5). The weather's
   part in the speed was built for it; the weather itself is not. The hour of an arrival
   and the route are not what that check covers for a division; for a fleet and an air
   unit it covers both (`fleetsandair.md`, section 6).
2. **Leaders** (`leaders.md`), taken up next on 2026-10-11. Cut in four
   the same day - **the cut is this project's**:

   - **who they are and who commands what - built** (2026-10-11): `history/leaders` and
     `common/traits.txt` read, a leader's skill from his experience, who is in service as a
     game starts and from the day his history first gives him a rank, the rank each level of
     a unit takes, and **the choice of a leader for every unit that has none** - his skill
     and a set amount for each of his traits that suits the unit, the first of equals
     winning - as a game is made and as a unit is put on the map or joined. **It is the
     original's unit for unit**: the start save's 1555 unit leaders and 101 countries'
     leaders in service all agree (`OpenHOI3.SaveCheck`). The panel shows a unit's leader,
     in red where he has not the rank, and his portrait. **Eight things in it are the
     original's and look odd, and are built so** (`leaders.md`), none raised for a
     decision: an order of battle's leader goes on whoever he is; the day's putting in
     service gives rank 1 and nothing later in a history is applied; a division of one
     brigade is never given one; the paratroop trait never counts towards choosing; the
     defensive one suits only a unit with a garrison brigade; the rank that counts is
     looser than the rank that is chosen; a trait's figure plays no part; a dated skill
     does nothing;
   - **how far a command reaches - built** (2026-10-11, `leaders.md`, section 8): a
     headquarters reaches in full within its radio's radius - 200 km for a corps, 400 for an
     army, 600 for an army group, 2000 for a theatre - less out to twice it and not at all
     beyond, the links multiplied up the chain; a leader's trait comes down the
     chain halved at every step; the army's commander adds his skill by his reach to every
     brigade's organisation ceiling, and the army group's takes a twentieth a point off what
     its units use, with the `supply_consumption` traits. The radio icon of the unit panel
     and the broken dark red line on the map show a unit out of range. **Read whole out of
     the original and checked against the start save**: the organisation of all 764
     brigades under a commander agrees (142 before), 62 of 65 capitals' use of supplies
     (38), and 184 of 234 supply networks in every province and figure (171). Each rank's
     skill has its own part, as set out on 2026-10-11 - the division's,
     corps' and theatre's are in battle and wait for the next piece. **Two more things in
     it are the original's and look odd, and are built so**: the reach drops from half to
     nothing at twice the radius, and a new game's stockpile is reckoned before its leaders
     are chosen. The first was raised for a decision that day; the other nine have not been;
   - **what a leader does in a battle**, cut again the same day, the cut this project's:
     - **in a land battle - built** (2026-10-11, `leaders.md`, section 9; `landcombat.md`):
       a unit's leader's bonus - 5% a point of skill, with his offence or defence trait and
       his trait for the ground - every other trait in the figure it names, the theatre
       commander off crowding, the corps commander bringing reserves up, a defender's leader
       in the order of its front, a leader in shattering, a side's highest ranked leader in
       its tactic and the `trait` condition read for it, and the chain's speed traits in a
       march. **Read out of the original and set against nothing**, as the land battle is;
       reading it confirmed the defender's front order off the instructions and found a
       corps in reserve counting its own commander. **Three more things look odd and are
       built so**: a unit whose own leader lacks the rank or the skill gets none of the
       leader's bonus, its commanders' offence and defence traits with it; `fort_defence`
       and `surprise_chance` do nothing; the `trait` condition asks the leader's own traits
       only. The aggressiveness a tactic is picked with is the leader's unit's since 2026-11-09 (`unitpanel.md`, section 9); then plans were the AI's;
     - **at sea and in the air - built** (2026-10-11, `leaders.md`, section 10;
       `navalcombat.md`, `aircombat.md`, `bombing.md`): a unit's leader's bonus on every ship
       and wing - his skill alone, no trait - where he has the rank; his skill, whatever his
       rank, in drawing a fleet up, in a ship's or a wing's choice of target and in a bomber
       finding ships; a side's leader in its positioning at sea and against friendly fire;
       and eleven traits where they name, spotting to strategic attack. Two technology
       figures went in beside the admiral, `targeting_chance` and `targeting_choice`.
       **Read out of the original and set against nothing**; most of it had been read
       before, and the reading named those two figures' keys in the record, and found the
       fact base's "an admiral of skill 5 draws his ships up at exactly half their reach"
       missing the floor that leaves one further. **Two more things look odd and are built
       so**: an admiral too junior for his fleet still draws it up and picks its targets,
       and `dissent_impact` does nothing at sea or in the air;
     - **the experience and the traits a battle gives - built** (2026-10-11, `leaders.md`,
       section 11): every unit's hour in a battle or a bombing gives experience - a die, the
       size of the enemy, the defines and the `xp_gain` traits - to each brigade, ship and
       wing, to its leader and, at a share each step, to every commander above; a leader's
       skill rises from it, faster the further below his best. Each side's leaders bank their
       hours in battle and have them counted, when a unit leaves the side and at the end,
       towards the traits of `common/gainable_traits.txt` whose condition holds - six
       conditions read for it, the tactics' condition made a battle's. **Read out of the
       original and set against nothing**; the start check still agrees leader for leader.
       **Four more things look odd and are built so**: a raid never counts its raider's
       hours, so `seawolf` cannot be earned by raiding; `has_combined_arms = no` holds like
       `= yes`; a battle broken off is won by both; a ship with no target learns nothing;
   - **the window a leader is chosen in - built** (2026-10-11, `leaders.md`, section 12), with
     his place in a battle's window: opened from a unit's leader, it lists the owner's leaders
     of the unit's kind who are free, through a filter of traits, sorted by name, skill, traits
     or rank **with the original runtime's own sort** - copied step for step and checked against
     the original's code run under an emulator on 1,360 lists, since which of equals comes
     first is the sort's - and posts four commands: a leader given, a rank up or down, every
     leader taken off after a confirmation, and the country's own choosing turned off. A
     battle's window shows each side's highest ranked leader, his portrait and the arrow of
     initiative. **Read out of the original and tried with real clicks; it has not been
     reviewed in play.** Reading for it corrected the fact base's rule for when the unit panel's
     leader button may be pressed. **Six more things look odd and are built so**, none raised
     for a decision: the portrait opens the window where the button cannot be pressed; nothing asks whose
     unit it is; an expeditionary force's refusal is undone by the original's own last test; a
     general promoted is taken off his unit while the window's head keeps him; a filter with
     everything off is forgotten; and "remove all" closes the window before it is answered.

   **With that, leaders are built** as the cut set out; the AI's own reshuffle, leaders events
   make, and the window's tooltips wait (`leaders.md`, *Not built*).

3. **Tooltips, for everything built so far** - decided on 2026-10-11, after
   leaders and not before: the leaders to be finished as planned, then tooltips for
   everything already built. **The mechanism is built**
   (2026-10-11, `docs/mechanics/tooltips.md`): read out of the original, tried with `--click`,
   and it has not been reviewed in play - the one box from `core.gui`, a tooltip's delayed
   half under a line of dashes once the mouse has been still, an element's tooltip hanging 64
   below its corner, the box kept inside the right and bottom of the screen, and a button's
   shortcut, which is all that shows yet. **No `.gui` file sets a tooltip of its own**, so
   every tooltip is a screen's code; what each says is read and built a screen at a time, in
   this order, the cut this project's:
   1. **the map's - built** (2026-10-11, `tooltips.md` section 7): the province under the
      mouse, `ProvinceTooltip_Build` read whole - a selected army's warning, the name and
      owner, the flags (`@TAG` in text draws a flag), the terrain, political, supply and air
      modes' parts, and the distance from what is selected. Tried with `--click`; it
      was set beside the original - "the style looks good" - and on a screenshot of the original
      the tooltip stands over the minimap, which waits for the minimap. The readout no longer
      carries the supply and air lines;
   2. **the top bar's - built** (2026-10-11, `topbar.md` section 14): by the element's name -
      each picture a heading with its description to come, each resource's number its day's
      ledger, IC, dissent, diplomacy, national unity, officers and manpower with the figures
      behind them, and **where a modifier comes from**, source by source with its name, which
      four of them end in (`modifiers.md`, section 8). Tried with `--click`; reviewed in
      play on 2026-10-11: "Looks good". Three oddities are kept as read (`topbar.md`,
      section 14);
   3. **the production and technology screens' - built** (2026-10-11, `productionscreen.md`
      section 8, `technologyscreen.md` section 9, `sliders.md` section 6): by the element's
      name - the resources' ledgers, a strategic resource with each province that gives it, the
      bombs, the buttons, a build queue line by its element with its brigades drawn as their
      counters (`£` in text, `tooltips.md` section 7); **what each of the ten sliders' shares
      would do at the slider's position**, with the lock line on the knob; where leadership
      comes from, a tree entry, a queue line with research efficiency, a category icon's speed
      and **the start button's days at full funding**. Tried with `--click`; reviewed in
      play on 2026-10-11: "Looks good". Two parts wait for readings of their own:
      **what a level of a technology does**, which a tree entry's tooltip and the panel's
      `effects` list are made of, and **why a technology may not be researched**, which its
      `allow` block says;
   4. **the four builder windows' - built** (2026-10-11, `builders.md` section 11): each
      window's own routine read whole - **only the column headers speak**, a name and then what
      the column means, the four step buttons say "Ctrl + LMB to add or remove 10", and the
      designer's combined arms icons say their group; **the designer's terrain summary**, its
      brigades' ground, river, night, fort and landing figures averaged; and **the practical
      icon**, how much cheaper the country builds what it teaches. Tried with `--click`; they
      have not been reviewed in play. The step of ten is Ctrl now, on the tooltip's word,
      where it was shift. Reading for it corrected the fact base: the routine it had as the
      builders' headers is the ledger's;
   5. **a unit's panel - built** (2026-10-12, `unitpanel.md` section 10): every entry's own
      routine read whole - the header, the status, the order with what each kind of order
      calls itself, the brigades with **a brigade's summary** (its model by name, its
      strength, organisation and experience, a ship's speed and range, and the level of each
      technology it carries, a level named by its text or by **a model year**), the buttons, a
      headquarters' row, and **a leader's own tooltip** - his rank, command, skill, how far to
      the next and his traits. A text box over a button now answers first where the mouse is
      over it and leaves the button its clicks (`CGuiObject.PassiveTooltip`). Tried with
      `--click`; it has not been reviewed in play. Supply and fuel came later, and the
      attrition breakdown on 2026-11-14 (section 10.2, tried with `--click` at Edd and
      Vercelli); a base's supply, the speed's terms and the hourly regain wait for what they
      describe (*Loose ends*). Reading for it corrected the fact base: the order of the radio lines' text and
      line break, and that a headquarters out of reach is yellow;
   6. **a unit's other windows - built** (2026-10-12, `mapunits.md` section 10,
      `fleetsandair.md` section 8): read whole - **the deploy menu's lines** list their unit's
      brigades by their counters, a finished fleet's or air unit's line nothing; **the orders
      windows** say why `accept` cannot be pressed, the stance and the priority, the air
      window's shapes, continuous box and day readout, and each row its order's description,
      the fleet's with how far out of reach it is. **Both windows' priority buttons say
      nothing**, the original asking twice for a name its file does not have. A list's rows are
      now searched for a text box that answers (`CTooltips`). Tried with `--click`; they
      have not been reviewed in play;
   7. **a battle's window - built** (2026-10-12, `combatview.md` section 12): the window's
      routine and every entry's read whole - the terrain, the width, the retreat, the arrow
      between the tactics, **a tactic's own text** with what counters it and what it counters,
      each side's leader with his initiative; **a land unit's whole** - its leader, attack and
      defence, what each combat modifier is worth to it, its side's tactic as the side feels it,
      piercing and armour; **an attacker's reserve its chance of coming up**, where the
      defender's say a unit's whole; a ship's target, positioning and reach; a wing's attack
      and defence; the icons of the modifiers in play; the close button `CLOSE`; and **the row
      in a unit's panel**, each side's units by name and what they come to, with `VERSUS`
      between - where **one fleet's ships run together**, the original's. Tried with
      `--click` on a battle at Chrzanow; they have not been reviewed in play. A landing's
      lines, the bombed side's rows and a bombing's addition to the row wait (*Loose ends*);
   8. **the leader window's - built** (2026-10-12, `leaders.md` section 13): the window's
      routine read whole - the trait filter first and final, then a row, then by name, and what
      is left to the unit panel header's names, so **a row's button says the header's "Assign
      new leader."**; a portrait the leader's own tooltip, `gaintrait_info` the traits he is on
      his way to, **a trait's icon its name and a line for each effect**, a filter row what its
      trait does, the filter button how it works. The leader's own tooltip now says how many he
      may command only where he commands a fleet or an air unit, which corrected the unit
      panel's reading. Tried with `--click`; a screenshot of the original shows
      a trait's icon answering with its name, which settled that the original shows them;
   9. **the technology screen's last two - built** (2026-10-12, `technologyscreen.md`
      section 9; `CRequirementText`, since 2026-11-14 `CConditionText`, and `CTechnologyEffectsText`): read whole out of the original,
      tried with `--click`, and they have not been reviewed in play. **Why not** is the
      `allow` block's account of itself, every condition with a red or green asterisk and the
      words turned round inside a "none of" - **indented as the mod's DLL corrects the
      original**, by decision (`docs/compatibility.md`, entry 10). **What levels do** is the
      units and building a first level allows, each usable unit type's figures folded onto one
      line, and 46 country-wide figures, the tactics, groups, missions and decay - every
      figure times the level the entry speaks of. `activate_building` and `listening_station`
      are read for it.

   **With that, every screen built has its tooltips**; from now on a new screen comes with its
   own.

4. **A look at the screens beside the original**, figure by figure. It needs nothing
   built, only the game open, so it can happen whenever suits. The specs each end with
   what a look would settle. The production screen has four needs to look at and the
   lines of its build queue. **The four builder windows have been tried twice by hand**,
   on 2026-10-08 and the day after; what was wrong is mended and the second time they
   were found right (`builders.md`, section 10). Nothing in them has been measured
   against a screenshot, and `builders.md` ends with what that would settle: where each
   figure sits, the checkbox's and the combined arms strip's frames, the spare template
   buttons, the panel with nothing picked. **The near counter is measured against a
   screenshot of the original's** (2026-10-09), which put its figures on their line and
   its name down its right edge; `mapunits.md` ends with what a further look would
   settle - a counter's size at a given zoom, the far counter's texts, which way a stack
   fans.
5. **What the builder windows leave unread**, none of it in the way of ordering a unit:
   the name and the picture of the historical model a type is shown by, the name of each
   level of a ship's technology and the text of what it does, the description of a wing
   or a brigade, and what saving a template does
   (`reversing/findings/FINDINGS-builders.md`, the end).
6. **Stage 4: events and decisions**, taken up on 2026-10-12 by decision ("Ok
   lets go") after the cut was raised for a decision: reading the script; what a trigger answers; what
   an effect does; and an event firing and a decision taken, with the event's window and the
   decision screen. **All four are built** (2026-10-12); what waits for other systems is in
   *Loose ends*. **Then, by decision ("Lets finish the event and decision stuff
   first"), three more**: the words of conditions and effects, the on_actions, and messages -
   read out of the original together by a wave of seven - **all three built** (2026-10-12).
   1. **reading the script - built** (2026-10-12, `docs/mechanics/script.md`;
      `Content/Script`, `Sim/Script`): read out of the original by a wave of three
      (`reversing/findings/FINDINGS-eventfiles.md`, `FINDINGS-triggergrammar.md`,
      `FINDINGS-effectgrammar.md`) - the words a file is cut into, an event's, an option's and
      a decision's keys, the mean time to happen, all 153 trigger keywords and 91 effect
      keywords with the shape of each one's value, and the names the original looks a key up
      in when it is no keyword, in its order. **Every problem is reported where the original
      is silent** - an unknown key, a name nothing has, a value of the wrong shape, and what
      the original does that a file cannot have meant, `limit` inside a scope the plainest.
      **The base game's 232 events and 74 decisions load with no unknown key**, and with four
      problems, all the base game's own: a `controls` with no `=`, `units_in_province = GER`,
      and two events sent that no file has (`script.md`, section 10).
      `OpenHOI3.SaveCheck script <install>` lists them. Nothing fires yet;
   2. **what a trigger answers - built** (2026-10-12, `docs/mechanics/triggers.md`;
      `Sim/Script`, with `CConditions` the way in): the scope and its eleven forms, the four
      blocks and the five sets, and every leaf the simulation can answer, as the original
      answers it - `month` from nought, `war_with = REB` true for everyone, `neighbour` asked
      of the other country's list, a province's number throwing the country away. The 78
      leaves nothing had read were read for it by a wave of three
      (`reversing/findings/FINDINGS-triggerleaves.md`). **A condition about something the
      simulation does not hold yet answers false and is reported as unbuilt** - diplomacy,
      the government, parties, flags, revolts, espionage, the AI's missions, a war's record
      (`triggers.md`, section 8) - by the decision of 2026-10-12: feeding them from
      the history files "would just be adding a system that is wrong and would be removed
      later on anyways", since that state will be the country's own when its system is built.
      Laws, the government's posts, the faction, the overlord, technologies, IC, manpower,
      dissent, national unity, units, convoys, wars and alliances answer.
      `OpenHOI3.SaveCheck conditions <install>` asks every event and decision of every
      country on the start date: 16 events hold for somebody, and **5 of them only because
      an unbuilt condition inside a `not` makes the `not` true** - and they are to fire all the
      same, by decision ("Just fire them");
   3. **what an effect does - built** (2026-10-12, `docs/mechanics/effects.md`; `Sim/Script`,
      with `CEffects` the way in): the scope blocks, `limit` in a scope doing nothing, the
      eleven blocks that pick members with the state of chance each moves, `random` and
      `random_list`, and the effects on what the simulation holds - flags, variables and the
      modifiers an event gives (new state, step 13 of a country's modifier and step 5 of a
      province's, `common/event_modifiers.txt` read for them), a country's figures, goods,
      capital, abilities, ministers, leaders, an alliance and a war, a province's figures,
      buildings, resource, name, cores, owner and controller. The 68 effects nothing had read
      were read for it by a wave of three (`reversing/findings/FINDINGS-effectrun.md`).
      **What the simulation does not hold does nothing and is reported** (`effects.md`,
      section 8): diplomacy, factions, the government and parties, revolts, espionage, the AI,
      units an event gives, and `country_event` and `province_event`, which wait for the next
      piece. `OpenHOI3.SaveCheck effects <install>` runs every effect of the base game once:
      2,882 in 615 blocks, under a second, and nothing trips;
   4. **an event firing and a decision taken - built** (2026-10-12, `docs/mechanics/events.md`;
      `Sim/Events`, the two commands in `Sim/Commands`, `Interface/Events`, and
      `CEventWindows`, `CEventWindowView`, `CDiplomacyView` and `CDecisionEntry` in
      `src/Screens`), read out of the original by a wave of three
      (`reversing/findings/FINDINGS-eventfire.md`): each country's candidate events made
      again each month - a loaded game's none until then, since 2026-11-16 - the day's roll on the mean time to happen,
      the firing - `immediate` first, a person's event opened, the AI's option drawn and
      posted - `fire_only_once` by the event alone, an unanswered event taking its first
      option after four months, and decisions: only the diplomatic ones offered, asked again
      when they run, and nothing recording them, so they may be taken again. **The AI's option
      is drawn on the game's own generator** where the original seeds one with the clock -
      by decision (`docs/compatibility.md`, entry 11). The event window,
      `EventWindow` or `Event_Province_Window`, and the decision list on the diplomacy screen
      were tried with `--click` (`--event=2602` fires an event for the player,
      `--screen=diplomacy` opens the list); **they have not been reviewed in play**.
      `OpenHOI3.SaveCheck events <install>` plays a new game: 46 events fire in its first 120
      days, the death of George V on 14 January among them.
   5. **conditions and effects as words - built** (2026-10-12, `docs/mechanics/scripttext.md`;
      `Interface/Script`), read out of the original by five of the wave
      (`reversing/findings/FINDINGS-scripttext.md`): every condition's line in both wordings
      with its asterisk, the blocks' headers, a scope written on one line after its name, every
      effect's line, a scope's head, the blocks over a set once for each member, `random` and
      `random_list` with their chances - an event option's tooltip, the `immediate` block under
      the description, and a decision's button and icon. **The original's oddities are kept**:
      a key no table has shows as itself (about fifty in the base game), lines with no break
      run into the next, a negative figure keeps its minus inside "Lose". The conditions take
      the corrected indentation (`docs/compatibility.md`, entry 10, extended). Tried with
      `--click`; **they have not been reviewed in play**. `OpenHOI3.SaveCheck scripttext
      <install> [tag] [event or decision]` writes all 689 of the base game's in 150 ms, none
      failing, or one whole.
   6. **the on_actions - built** (2026-10-12, `docs/mechanics/events.md`, section 8;
      `COnActionReader` in `Content/Script`, `COnActions` in `Sim/Events`), read out of the
      original by one of the wave (`reversing/findings/FINDINGS-onactions.md`): the file read,
      the first event whose trigger holds picked - a fired fire-once event standing in front of
      those after it, as in the original - and fired for each country of a decided battle's
      winners and then its losers, land, air and sea, each counting the battle
      (`land_battles_fought` and the two others answer since), and for a pride of the fleet sunk,
      remembered for `$PRIDE_SUNK$`. **In Their Finest Hour only the pride's events exist**: it
      empties the base game's battle and encirclement event files. Encirclement, surrender, coups
      and the bomb wait for what they hang on.
   7. **messages - built for events and decisions** (2026-10-12, `docs/mechanics/messages.md`;
      `Content/Messages`, `Interface/Messages`, and `CMessagePopup`, `CMessagePopups` and
      `CMessageLogView` in `src/Screens`), read out of the original by one of the wave
      (`reversing/findings/FINDINGS-messageshow.md`): `interface/messagetypes.txt` read, an
      answered event's and a taken decision's message made as the player on this machine is told
      it - `US` for his own, `OTHER` and `MAJOR...OTHER` for the rest - with his government's
      posts in its header; the popup, `DefaultPopup`, its lines laid one under the other and its
      buttons by `option`, cascading over eleven places; and the message log on the bottom bar,
      200 messages kept, a row for each piece of a wrapped line, six category filters. Tried with
      `--click` (`--event=2001:SPR` has Spain answer its major event and pops it up);
      **they have not been reviewed in play**. Every other message the original raises waits
      for what raises it.
7. **Stage 4: diplomacy**, taken up on 2026-10-12 and cut in this order: what
   two countries hold of each other; the diplomacy screen showing it; Lua, which the AI answers
   offers with; the agreements; war and peace; factions. Read in one wave
   (`reversing/findings/FINDINGS-diplostate.md`, `FINDINGS-diplomacyview.md`), which moved two
   things forward by decision: **a first slice of Lua into the first piece** - a new game's
   relations come from the mod's own `AI_GenerateNonHistoricalRelation` - and **the alignment
   triangle**, which threat is weighed on.
   1. **what two countries hold of each other - built** (2026-10-12, `docs/mechanics/diplomacy.md`,
      `docs/mechanics/alignment.md`; `Sim/Diplomacy`), read out of the original and **it
      has not been reviewed in play**: every country's record towards every country; **relation** - the
      history's one side at a time, the Lua for the rest, the month's drift written both ways, the
      `relation` effect and condition, and +5 for a land battle won beside a friend; **threat** - its
      two spreaders, to everyone and over the map, from every unit put on the map, a battle or a
      strategic bombing started, convoy ships sunk, the history and the `threat` effect, never
      decaying, with the most-threatening country, `threat` and `is_threatend`; **neutrality** - its
      day, effective neutrality, `neutrality`'s modifier, the effect and the two conditions; and
      **the alignment triangle** threat is weighed on - each country's history point, its step every
      25 days and on a new game's first, the seven terms and the clamp with its sign slip. **A new
      game is the original's start save in every pair's relation and threat, 7,878 of 7,878, in
      every country's neutrality and most-threatening country, 101 of 101, and in alignment 50 of
      101** - 67 with the save's laws given, the rest the AI's opening laws and cabinets, not built
      (`OpenHOI3.SaveCheck diplomacy`).
   2. **Lua, its first slice - built** (`docs/mechanics/lua.md`; `lib/OpenHOI3.Lua`,
      `native/lua`): the original's interpreter, **Lua 5.1.4 built from its source** by
      decision, rounding as the original's 32-bit build does; its state as the
      original makes it, with the install's `autoexec.lua` and a searcher over the layers; and
      the eight methods `AI_GenerateNonHistoricalRelation` calls. The base game's `autoexec`
      stops at the first engine global it reads that is not built, `SpyMission`.
   3. **the diplomacy screen - built** (2026-10-13, `docs/mechanics/diplomacyscreen.md`;
      `CDiplomacyView`, `CDiplomacyCountryEntry`, `CRelationListEntry` and `CFactionBox` in
      `src/Screens`, `CDiplomacyLists` and `CDiplomacyScreenText` in `Interface/Screens`,
      `CDiplomacyTooltips` and `CDriftTooltip` in `Interface/Tooltips`), read out of the
      original - the reading's third part, the triangle, the faction boxes and the tooltips left
      over, the same day (`FINDINGS-diplomacyview.md`, section F) - tried with `--click`, and **it
      has not been reviewed in play**: the five tabs, a button for each faction and each
      continent, the nine sorts on one shared flag, a row with its threat and its seven tinted
      icons; **each country's marker on the triangle**, where its place puts it, made again every
      25 days; a box for each faction with its members; the chosen country's panel - relation,
      both threats, both neutralities with the ceiling for war, the highest threat, its status
      lines, the relation list with its two sorts and the breakdown of a year's change - and every
      tooltip on them, **a marker's the drift towards each faction, term by term**. Text boxes
      draw `@TAG` flags now, as the relation list needs. `OpenHOI3.SaveCheck diplomacy ...
      screen=TAG` prints what it says of a country. What is not built shows nothing or nought:
      *Loose ends*.
   4. **Lua, which the AI answers offers with - built** (2026-10-13, `docs/mechanics/lua.md`,
      sections 3 to 6; `lib/OpenHOI3.Lua/Api`), read out of the original
      (`reversing/findings/FINDINGS-luaapi.md`) and **it has not been reviewed in play**: **the
      base game's whole `autoexec.lua` loads**, the one engine global it reads as it loads being
      `SpyMission`; every class is a global with its static functions and constants; **each push
      is a new value and `==` is one operator for every class**, as luabind does it, so comparing
      two countries is the original's error; iterators; `CFixedPoint` with **`Get()` a float and a
      number on the left working `-` and `/` the other way round**; the methods the eleven
      `DiploScore_*` functions that take nothing but tags reach, on the simulation; and
      `IAiScripts.DiploScore`, which answers **0 on a failure**, as the original refuses the offer.
      What the scripts read that is not built answers nought and is reported once. Nothing asks it
      yet - offers are the agreements' - so `OpenHOI3.SaveCheck aiscore` asks every pair: on a new
      game all eleven answer 6,006 pairs without an error but **`DiploScore_PeaceAction`, which fails
      at `CAIIntel` - espionage - every time**. `JoinFactionGoal`, `LicenceTechnology` and
      `OfferTrade` read the offer itself, and come with it.
   5. **the agreements - built** (2026-10-13, `docs/mechanics/agreements.md`;
      `Sim/Agreements`, `COfferCommand`, `CDiplomacyActions` in `Interface/Screens`,
      `CMessages.Pact`, and `CDiplomacyActionEntry` and `CDiplomacyRequestDialog` in `src/Screens`),
      read out of the original in one wave (`reversing/findings/FINDINGS-agreements.md`) and cut
      in two by this project - **the cut has not been reviewed**: the offer and the
      pacts first, trade and aid - trade, debt, lend-lease, licences, shared technology - second.
      Built: **an offer** as a command, its influence and the diplomat on his way for seven days;
      **the AI's answer** each hour, by the install's Lua - over 30 and under a draw of the game's
      generator accepts - and the answer posted back as a command; offers dropped after 96 hours;
      **alliance, NAP, guarantee, both kinds of transit rights, embargo, influence and alignment**,
      arm by arm with their relation, influence, threat and messages; how each ends by itself, the
      influence's upkeep, a war's ending of influence, and access in the month's drift; NAPs read
      from the history; and **the screen's action list** - the rows in the original's order, greyed
      by its own test or a diplomat, each with its tooltip - and **the request dialog** with its
      description and the word for the AI's likely answer. Tried with `--click`: Germany's offer of a
      NAP to Poland, Poland's Lua answering -410, the refusal at the next hour and "Poland rejected
      our offer of a non-aggression pact." **It has not been reviewed in play.** Reading for it
      corrected the fact base twice: the triples of message keys are not all actor-first (a declined
      NAP tells the proposer `THEYREJECTNAP`) and a revoked guarantee is told to everybody.
      **Trade, the first of the second half, is built** (2026-10-13, `docs/mechanics/trade.md`;
      `Sim/Trade`, `CTradeWindowText` in `Interface/Screens`, `CTradeRequestDialog` in
      `src/Screens`), read in a second wave (`reversing/findings/FINDINGS-tradeaid.md`): a route's
      two blocks and **its price, which reproduces 582 routes of the start save**; the offer and its
      tests, the answer by `DiploScore_OfferTrade`, the route made with its convoy, its day's
      exchange after the cut-off goods are gathered, inactive for want of a convoy or of the goods,
      and its ends - cancelled, at war, under embargo, its convoy gone; **the AI's own trading**, by
      the install's `ProposeTrades` and `EvalutateExistingTrades` each hour for every country nobody
      plays; and the trade window, tried with `--click`: Germany selling Poland 50 metal a day for
      3.75 money. `OpenHOI3.SaveCheck trade` plays it: ten days of a new game make 283 offers, 262
      accepted. **The window was set beside the original's on 2026-10-13, and the
      trade interface was asked to be finished**, read in a third wave (`reversing/findings/FINDINGS-tradeui.md`) and
      built the same day: the window's place, the screen's middle and `diplo_request_pos`, and its
      sliders' "Sell" and "Buy"; **the bar of offers made to the player** with each icon's tooltip,
      blinking and week, and the dialog an offer opens (`CDiploOffers`, `COfferAskDialog`,
      `COfferAskText`); **the production screen's list of trades** with its tooltips, inactive marker
      and cancel dialog (`CTradeListEntry`, `CTradeCancelDialog`, `CTradeListText`); **Automate Trade**,
      the hour's roll of the C runtime's `rand()` seeded to 1 (`CAutomatedTrades`, `CRuntimeRand`); and
      the trade messages' goods. Tried with `--click`: three days bring Germany nine offers, Finland's
      opened and accepted, its line in the list, its tooltip and its cancel.
      **Aid is built too** (2026-10-13, `docs/mechanics/aid.md`; `Sim/Aid`, the four offer kinds in
      `Sim/Agreements`, `CLicenceText`, `CShareTechnologyText`, `CLendLeaseText` in `Interface/Screens`,
      and `CShareTechnologyDialog`, `CLicenceDialog`, `CLendLeaseWindow` in `src/Screens`), read in
      `reversing/findings/FINDINGS-tradeaid.md` C and `FINDINGS-tradeui.md` D and E: **debt** - asked,
      allowed, revoked at the debtor's cost, cleared by war, repaid a 18,250th a peaceful day for ever;
      **shared technology** - a theory read at the higher of the two levels, a new sharer ending the
      old, the day's check, the leadership it costs, the screen's green figure and its tooltip;
      **lend-lease** - who may and by which way, asked for and offered, the hourly look, the receiver's
      convoy, the giver's day to the thousandth and the receiver's whole IC, free to its five shares;
      **licences** - the fee, the buyer's runs at the seller's levels, the AI's answer in the install's
      Lua; and their rows, dialogs and window, and their offers put to the player. Tried with
      `--click`: Britain sharing with France, the licence dialog's list, the empty lend-lease window.
      **It has not been reviewed in play.** Reading for it corrected the fact base and this
      project: reinforcement and upgrades charge their resources whatever the stockpile holds.
   6. **war and peace - the first piece built** (2026-10-13, `docs/mechanics/war.md`; `Sim/Wars`,
      `CWarEffects` and the war conditions in `Sim/Script`, `CWarActions` and `CWarText` in
      `Interface/Screens`, `CWarGoalDialog` in `src/Screens`), read in one wave
      (`reversing/findings/FINDINGS-war.md`) and cut in two by this project, **the cut
      decided on 2026-10-13 ("Ok split it")**: declaring a war and who joins it first, peace
      and the capitulation second. Built: **the casus belli types** of `cb_types.txt` with their blocks and `peace_order`,
      and the casus belli held - every one made in play of the null type, as the original's;
      **war goals** - available, valid for a war, added once a month, the history's own (Italy's
      `puppet` on Ethiopia); **a declaration** step by step - a new war or one widened, its name
      ("War of German Aggression", ordered), the puppets, guarantors and factions in at once, the
      overlord and the allies asked, the relation, the threat of a new war, the message - and
      **limited war**; **the call to arms** both ways, the AI's answer by `DiploScore_CallAlly`, and
      the player's dialog with its warning lines; **the truce** and **mobilisation** - a new game
      mobilises Italy and Ethiopia to 31 January as the start save has them; the `war`,
      `add_wargoal`, `casus_belli` and `add_casus_belli` effects and the `truce_with`, `casus_belli`,
      `has_wargoal` and `has_cb` conditions; the war ceiling's claims term; and on the screen **the
      war rows** with their tooltips and **the declare-war dialog** with its goal, country and
      region lists. Tried with `--click`: Germany, its neutrality set to nought, declaring on Austria
      for a puppet; Japan answering Manchukuo's call against the Soviet Union, taking the lead.
      `OpenHOI3.SaveCheck declare` tries a declaration on a new game. **Nobody may declare war on 1
      January 1936** - every country's neutrality is above its ceiling - so `--neutrality=GER:0`
      and `--declare=TUR:IRQ` are there for trials. **It has not been reviewed in play.** Reading
      for it corrected the fact base four times: a truce counts from the last change of a war's
      membership, not its end; the casus belli type's month count is `months`, not `truce_months`; the surrender sums are over
      cores, nested; and the war's flag the record had named twice is `limited`, as both earlier readings had it.
      **The second piece, peace and the capitulation, is built** (2026-10-13, `war.md` sections 11 to
      14; `CPeace`, `COccupiedLand`, `CSurrender` in `Sim/Wars`, `CProvinceHands` in `Sim/Provinces`,
      `CPeaceText` in `Interface/Screens`, `CAiIntelClass` in `Lua/Api`), after a second wave read what the
      capitulation calls (`reversing/findings/FINDINGS-surrender.md`): **peace** - the row and its gates, the
      dialog with what would change hands, the AI's answer by `DiploScore_PeaceAction` now that **`CAIIntel` is
      built** (all 6,006 pairs answer), the provinces moved, the occupied land given back and the war ended;
      **leaving a war** for peace and `end_war`; **the day's return of occupied land**; **a change of hands with
      consequences** - the 50% building damage, production stopped, a fallen capital's quarter looted; and **the
      surrender progress**, the victory points of owned cores counted each day, with `surrender_progress` and
      Lua's `GetSurrenderLevel`. Tried with `--click`: Germany offering Austria peace - "Very Likely" - and
      Austria accepting at the next hour. **It has not been reviewed in play.** Reading for it corrected the
      fact base: the AI's sighting draw is never negative.
      **Then, by decision ("Continue with your plan"), the rest in the order planned**: **the acting
      capital** - where a government sits and so whose pool is its stockpile, moving at once when its capital
      falls and back when it is held again, with the declared capital moving only on a loss of ownership - and
      **every one of about a hundred reads of a country's capital set to the one the original reads there**, read
      site by site in a third wave (`reversing/findings/FINDINGS-capitalreaders.md`; `CActingCapital`);
      **annexation** (`CAnnexation`, `inherit`) and **the puppet** (`CPuppets`, `create_vassal`), with a faction's
      members kept in the order they joined (`CFactionMembership`); **the government in exile** (`CExile`,
      `form_government_in_exile`, `government_in_exile`) - its own stockpile, its cuts, its seat in its leader's
      capital, the day's hand-over, the puppet swap and what an exile may not do; and **the capitulation** -
      the daily surrender to the best-scoring enemy with `on_surrender`, every enemy's goals enforced in
      `peace_order` by the original runtime's sort, the made-up `conquer`, vassals freed or puppeted, and each peace
      option (`CCapitulation`, `CWarGoalEnforcement`). `OpenHOI3.SaveCheck declare ... occupy` tries it on the
      install: Ethiopia, its units gone and its land held, surrenders to Italy at the day's end and is made Italy's
      puppet by the history's goal; Austria to Germany is annexed by `conquer`; France to Germany goes into exile
      in London. **None of it has been reviewed in play.** **A battle's capture then went through the same
      setter** (`landcombat.md`, section 7): a puppet's conquest goes to its master, ground taken alone among a
      fighting friend's to the friend, an exile's to its faction's head only where the head is an enemy of the
      owner - which this had left out - and a capital taken is looted and its government moves at once; and a
      defending front unit falling back may damage the province's buildings. Read off the bytes for it, the
      record's `CanOperateIn` was corrected: the byte it tests is the co-belligerent one, not military access.
   7. **factions - built** (2026-10-13, `docs/mechanics/factions.md`; `CFactions`, `CFactionOffers`,
      `CFactionProgress`, `CVictoryConditions` in `Sim/Wars`, `CFactionAimsReader` in `Content/Script`, `CFactionText` in
      `Interface/Screens`), read in one wave (`reversing/findings/FINDINGS-factionplay.md`), and with it **diplomacy
      is built as far as the cut set out**: **joining** - `AddCountry` with its war sweep, every join through
      it; **Join Faction and Invite to Faction**, one action, its rows, greying, tooltip and dialog, the AI's answer by
      `DiploScore_InviteToFaction` with the tags turned round for a join, nothing on a proposal, the joiner's
      puppets following and its alliances with the members ended, the messages; `join_faction` and `leave_faction`;
      **the rules** - the Allies' guarantee given by every member and an answered call bringing the faction in;
      **the faction's modifier on its members**, which made Moscow's garrison the start save's (28 of 28 capitals);
      the faction leader at war taking an aligned outsider's resources; **a faction's progress**, the points its
      members hold, hourly - the start save's 115, 263 and 173 of 1172 - with `faction_progress`, the box's bar and
      tooltip; and **the victory conditions** of `faction_aims.txt`, each faction's fifteen primaries, tested daily.
      **Nobody may leave a faction by choice**: the original's leave button is its custom game's. Tried with
      `--click` - Germany's Invite to Faction row, the Axis's "0/15" - and with `OpenHOI3.SaveCheck faction`:
      Hungary asks Germany and is taken in, Sweden refuses Britain. **It has not been reviewed in play.** Reading
      for it corrected the fact base three times: the faction rules are written - so the Allies' faction-wide
      guarantee and call are live - `CFactionAction::Apply`'s "gate" is the direction, and a guarantor by the
      faction is called like any other. **The game's end is not built**: the dialog that ends it was not read.
8. **Stage 4: politics**, taken up on 2026-10-14 after one reading wave (`reversing/findings/FINDINGS-government.md`, five
   briefs) and cut by this project, **the cut agreed ("Sounds good")**: the government's figures and their
   days; laws and ministers changed; the screen; the AI's politics (`docs/mechanics/politics.md`).
   1. **the government's figures and their days - built** (2026-10-14, `politics.md` sections 1 to 8 and 10; `Sim/Politics`,
      `CPoliticsConditions` and `CPoliticsEffects` in `Sim/Script`, `CGovernmentReader` in `Content/Definitions`): the
      ideologies numbered with their colours, the governments and the cabinet posts read; **each party's popularity and
      organisation** from the history, raw, and normalised once more for a country owning land as the start save shows;
      **the monthly drift** - a tenth of the gap to organisation, the ruling party's support, a draw, the penalty for a
      party in two posts or more; **the cabinet's seats** and **a fractured government**'s modifier (step 18) with the
      government's own (step 5); **elections**, due by the last one and the term, won by the most popular party, the
      elected posts filled again and the player told; **the pool of ministers** and the filling of empty posts; **national
      unity's day** - a thirtieth of a monthly figure, slowed away from 50 - and **strategic warfare's three lists**, fed
      by raids, sea and air battles and strategic bombing; and every condition and effect on them. `OpenHOI3.SaveCheck
      politics` sets a new game beside the start save: with the save's laws the ruling party, the government and the last
      election 101 of 101, organisation 1010 of 1010, the posts 1009 of 1010, national unity 99 of 101; the posts of
      countries that own nothing went from 10 to 230 of 230 in the start-date check and alignment from 50 to 60 of 101.
      **It has not been reviewed in play.** Two things follow the save where the bytes say otherwise, both queued in
      the fact base: the pool at a new game and `minister_alive` take a minister with no death date as alive.
   2. **laws and ministers changed - built** (2026-10-14, `politics.md` section 9; `CChangeLawCommand` and
      `CChangeMinisterCommand` in `Sim/Commands`, `CLaws` in `Sim/Politics`, `CLawAllowReader` in `Content/Script`): the
      laws numbered in their groups with their `cost` and their `allow` read as script; **a law changed** - its cost of
      thirty days of money from the whole IC, charged only to a country a person plays and free to take the money below
      nought, `LAW_APPOINTED` to the player, the modifier made again, the build queue priced again and every brigade held
      to its new ceilings with the men given back; **a minister appointed** with `MINISTER_APPOINTED`; and **the
      peace-time revert** of a law no longer allowed, after a war left, a faction joined or left and a puppet made. Neither
      command checks anything: what posts it does - the screen, the AI's Lua, the revert. **It has not been reviewed
      in play.**
   3. **the screen - built** (2026-10-14, `politics.md` section 11; `CPoliticsView`, `CMinisterEntry`, `CLawEntry` and
      `CPossibleLawEntry` in `src/Screens`, `CPoliticsScreenText` in `Interface/Screens`, `CPoliticsTooltips` in
      `Interface/Tooltips`, `CToggleMobilisationCommand` in `Sim/Commands`), read whole and tried with `--click`: the
      government's lines with the elections line running on in yellow; **the parties' pie** in whole percents, its slice
      under the mouse with its change factors and drift, **the organisation bars** and the legend; **the cabinet** with
      Replace, the choice of a minister and SELECT; **the laws** with the choice of a law, its prices and the
      confirmation; mobilising and standing down; strategic warfare's figures; and every tooltip the original gives
      them. The icon now draws a pie and a progress bar with no picture. How the original draws a pie was not read: its
      place is measured on the shadow the art has under it. `--screen=politics` opens it and `--money=GER:500` gives a
      country the money for a law. Rising up from exile, puppets, liberation and occupation policies are not built.
      **It has not been reviewed in play.**
   4. **the AI's politics - built** (2026-10-14, `politics.md` section 12; `CPoliticsAi` and `CMinisterPosts.FillVacant`
      in `Sim/Politics`, the law, minister, post, politics-minister and command classes in `lib/OpenHOI3.Lua/Api`), for
      the countries nobody plays only (`docs/compatibility.md`, entry 6): **a new game's `Laws(country)`** repeated until
      nothing moves, free and silent while the game is made, after the units as the original does it; **a politics
      minister** for each, working once a day in its country's own hour - the weekly filling of empty posts and the
      install's `PoliticsMinister_Tick`, which mobilises, changes laws and ministers. Who plays is now known before a
      game is made (`CGameStateLoader.LoadStart`'s `played`). The laws agree with the start save for 703 of 707 groups,
      national unity for 98 of 101 without the save's laws, alignment for 79 of 101 where it was 60, and the AI
      countries' brigades for 3435 of 3534 where they were 3041; a hundred and ten days change 198 posts in 64
      countries where the original's changed 194 in 63 (`OpenHOI3.SaveCheck politicsai`). The AI's war plans, its own
      declarations, liberation and releasing a puppet are not built and answer false. **It has not been reviewed in
      play.** That ends the cut: politics is built but for espionage, occupation and revolt.
9. **Stage 4: espionage**, taken up on 2026-10-14 by decision after one reading wave
   (`reversing/findings/FINDINGS-espionage.md`, sections A to E, on the earlier reading above them), and cut by this
   project in four, as politics was: spies and their day - every country's presence in every other, the day's
   placing of the free spies by priority, the two commands, the nine missions, catching spies, covert-ops points with
   the coup and `common/covert_ops.txt`'s province operations, the messages and the four keywords, a new game's
   presences, set against the saves' `spies` and `spy_allocation`; what a country knows - the spies' figure, and a
   province's intelligence read whole to stand in for the part `CIntelligence` has; the intelligence screen; and the
   AI's spying in the install's Lua, for the countries nobody plays. One thing is already settled: **the screen's
   `self_button` ("Selected") does nothing, as in the original** - its code binds the other four tabs and never asks
   for that name - by the decision of 2026-10-14, "put it down as not working on purpose".
   1. **spies and their day - built** (2026-10-14, `docs/mechanics/espionage.md` sections 1 to 14; `Sim/Espionage`, the
      four commands in `Sim/Commands`, `CIntelGrants` in `Sim/Units`, `CEspionageConditions` and `CEspionageEffects` in
      `Sim/Script`, `CCovertOpsReader` in `Content/Script`): every country's presence in every country, its own with
      priority 1 and counter-espionage at home; a new game's spies scored in the loop that gives the relations; the
      espionage share placing whole spies by band 3, 2, 1 with the original's cursor that does not wrap; the nine
      missions once a day - counter-espionage with its friend rule and two rolls, military intelligence's grants on
      units (counted down a day; nothing reads them until the next piece), the steal of a technology with
      `stealable` now read, the two party boosts, threat, the two unity modifiers in the modifier rebuild, covert-ops
      points; the coup and its on_actions, covert operations in a province; the six messages; `spies` and
      `spy_mission_count`; `modify_spies` adding nothing, as the original adds a count it never reads; and the
      original's `IsFriend` moved into the simulation from the Lua class. Set against the start save
      (`OpenHOI3.SaveCheck espionage`): every home level 101 of 101, every priority, mission weight and covert-ops point
      10201 of 10201, the levels abroad 10051 of 10100 - each that differs one spy caught on the two days by another
      roll than ours, the original catching more because the triggered modifier `neutrality` gives a country in no
      faction counter-espionage, which was not built - and the free spies 95 of 101 where leadership differs, Germany's by
      the player's history laws (`docs/compatibility.md`, entry 6). With triggered modifiers built later the same day:
      free spies 100 of 101, Germany's alone, and levels abroad 10045 of 10100, 22 catches to the save's 33 - a part of
      the gap and not all of it. Played on to two later saves, Germany's home and every
      priority agree and the pool is short by that leadership alone. **It has not been reviewed in play**, and with
      no screen and no AI nobody gives a mission yet.
   2. **what a country knows - built** (2026-10-14, `docs/mechanics/intelligence.md`; `Sim/Intelligence`): the figure
      for another country, its spies less their counter-intelligence with no floor, which `CAIIntel` now reads - its
      factor, `HasNoIntel`, `GetTheirFactor` and the military mission's share; and every country's level in every
      province read whole, the old daily snapshot of units (`CNavalIntelligence`) gone - the day's rebuild with the
      holder's list at 9, the land beside at 3 and beside a foreign unit at 2 to 9 by decryption against encryption,
      each unit's grants, a foreign unit's owner at 9 on land and 2 or 9 at sea, and radar with its never-forgotten
      stations and its never-reached far ring; **the hour's raise** of the provinces units left or entered, and the
      whole rebuilt in an hour that changed hands or pacts; and `local_intel_boost`. Air missions, the bombing
      missions, a fleet's sighting and the air map ask it as before. With it Germany knows the land along its border,
      where it knew only its own. The raid, the battles and the bombing trials come out as before. Undergrounds and
      the drawing of a fog of war are not built. **It has not been reviewed in play**; the hour's raise, against
      the recollection of 2026-10-10 that it is daily, was raised for a decision and stays, decided on 2026-10-14:
      "Keep it as read."
   3. **the intelligence screen - built** (2026-10-14, `docs/mechanics/espionagescreen.md`; `CEspionageView` and its
      rows `CEspionageCountryEntry`, `CSpyMissionEntry`, `CSpyProductionEntry` and `CForeignMinisterEntry` in
      `src/Screens`, `CEspionageLists`, `CEspionageScreenText` and `CEspionageEstimates` in `Interface/Screens`,
      `CEspionageTooltips`), read out of the original and tried with `--click` (`--screen=intelligence` opens it): the
      player's own figures and the three home missions, the country list with its tabs, faction and continent buttons
      and five sorts on one direction flag, each row's live figures and priority button; the chosen country's spies
      coloured by what they know, its eight missions, its priority, the coup's button and tooltip; the estimates on a
      generator of the screen's own seeded by the month and the player, "?" under one whole spy, IC exact; its research,
      production by the C runtime's roll and cabinet, and its pie; every tooltip the original gives. A new game gives
      every country spies abroad at its start, so the estimates show from the first hour. **It was reviewed in play
      on 2026-10-14** and, beside the original, three things were corrected: a mission's title answers with its
      description; the coup's tooltip is the original's, read for it (`FINDINGS-espionage.md` D.9) - "Their party
      position is too strong" alone where that is why; and "Send Spy Priority" is drawn once, by the rule a look at the
      research queue then gave: a button on a plain sprite draws no words of its own (`interface.md`, section 2).
   4. **the AI's spying - built** (2026-10-14, `docs/mechanics/espionage.md` section 15; `CEspionageAi` in
      `Sim/Espionage`, `CSpyPresenceClass`, `CIdeologyDataClass`, `CIntelligenceMinisterClass` and `CSpyCommandClass` in
      `lib/OpenHOI3.Lua/Api` with six methods on `CCountry`), for the countries nobody plays: an intelligence minister
      for each as a new game is made, its first work one to ten hours on, then once a day in its country's own hour,
      running the install's `IntelligenceMinister_Tick`. `OpenHOI3.SaveCheck espionageai` plays to 21 April 1936 with no
      Lua error: all 77 AI countries owning land have acted, as in the save, and **5921 of 5929 of their priorities are
      the save's**. **It has not been reviewed in play.** That ends the cut: espionage is built.
10. **Triggered modifiers - built** (2026-10-14, by decision: "Commit and then continue with the triggered
    modifiers"; `docs/mechanics/modifiers.md` section 5, `docs/mechanics/strategiceffects.md`;
    `reversing/findings/FINDINGS-countrymodifier.md` section 3, read the same day for the messages and the window).
    `common/triggered_modifiers.txt` read (`CTriggeredModifierReader`); each country's tested once a day straight
    before its modifier is rebuilt and on entering a game, the list keeping the order they came on
    (`CTriggeredModifiers`, `CCountryModifiers.RebuildDaily`), step 14 of the rebuild; `TRIGMOD` and `TRIGMODLOST` to
    the player. And the window the player sees them in, **Strategic Effects**, opened by the bottom bar's button by
    the minimap (`CStrategicEffectsView`, `CStrategicEffectsEntry`; `CStrategicEffectsText` and the original's tally,
    `CConditionTally`): every one the country might have, those it has drawn plain and the rest greyed, each with
    its conditions met out of those counted, and the `trigger` block as the tooltip of its "?" - tried with `--click`,
    and the tooltip corrected by decision from the whole row to the "?" alone. Against
    the start save it closes five countries' leadership - officers, influence and spies 100 of 100, influence at the
    start 78 of 78 - and France's manpower; `neutrality` gives the research the check had given by hand; and the
    spies caught rise from 18 to 22 to the save's 33 (`espionage.md`, section 14). **It has not been reviewed
    in play.**

**Next: occupation and revolt**, the last of stage 4. Read whole on 2026-10-14 in one wave of six readers
(`reversing/findings/FINDINGS-occupation.md`, sections A to F, on top of its first sections and
`reversing/findings/FINDINGS-revolt.md`), which also settled the `counter_espionage` question (`espionage.md`,
section 6). The cut proposed, this project's, in four pieces as politics and espionage were:
   1. **occupation and a province's whole modifier - built** (2026-10-14, by decision: "Please continue
      with the next step"; `docs/mechanics/occupation.md`, `modifiers.md` section 3; `COccupationPolicy`,
      `COccupationPolicies` in `Sim/Occupation`, `CChangeOccupationPolicyCommand`, `CNationalism` in `Sim/Provinces`,
      `COccupationPolicyAllowReader` in `Content/Script`, `COccupationText` in `Interface/Screens`, `CPolicyEntry` and
      `CPossiblePolicyEntry` in `src/Screens`): the policies of `common/occupation_policies.txt` after the game's own
      `no_occupation`; every country's towards every country, set by a first capture - `civil_war` where its `allow`
      holds, else the file's first, keyed by the old holder - by the player's command, and by the history; and what a
      province is given of it, with no war asked. The rest of the province rebuild, read whole: the coast,
      `no_adjacent_controlled`, the policy, land or sea, `manpower`, `nationalism`, what events gave it, and its
      sources kept for the tooltips - all but radar's list and revolt risk. Nationalism: twenty years on a change of
      owner, the same owner included, and a second `owner` line in the history; 0.083 off a month; the condition.
      The politics screen's occupied countries and the choice of a policy, with every tooltip, tried with `--click`
      (`--occupy=GER:POL` hands one country another's land as a capture would). Against the saves: the start save's
      nationalism 5 of 5 (`firstdays`), and the `declare ... occupy` trial gives Italy's capture of Ethiopia
      `collaboration_government` and China's of Guangxi `civil_war`, as the February and April saves hold. **Reading
      for it corrected the fact base**: the province rebuild has fifteen callers and no screen among them, the hourly
      province pass makes a province's modifier again where a building was moved, and a plain change of controller
      makes nothing again. **It has not been reviewed in play.**

      **Then, before piece 2: a province's modifier made again on the original's schedule - built** (2026-10-15, on
      the decision of 2026-10-14 - "plan it before piece 2" - and then "Commit and then continue";
      `modifiers.md` section 3; `CProvinceModifiers.RebuildOne` and `RebuildMoved`, `CProvince.BuildingMoved`). Until
      then every province was made again once a day, a country's reason carried over and never decided for provinces.
      Now: every province at the head of the month's pass, before nationalism comes down; in the hour's look, a
      province whose building a bombing, a defender's damage or the repair moved; at once where the repair crosses a
      whole level; at once, the one province, on a battle's capture that changed its holder, every change of owner, a
      building built by an effect, `add_province_modifier`, a decision with a province, and the occupation command;
      and every province as a game is entered. **What it changes, all the original's**: a plain change of controller
      - the day's return, an effect - makes nothing again until the hour or the month; `add_core` and `remove_core`
      wait for the month; **a modifier an event gave a province outlives its end until the province is next made
      again**; a month's nationalism is the figure before that month's decay; and bombing reaches a province within
      the hour. Reading for it settled in the fact base which provinces entering a game makes (every one, at least
      twice) and that a battle's capture makes one only where its holder changed; a decision's is its scope's
      province, already recorded. Checked, each against the code before: `supplypass` 183 of 234, all three
      `research` pairs, `march`, `firstdays` (nationalism 5 of 5) and `espionage` (10045 of 10100 abroad) the same;
      the `bombing` trial the same building for building, its bombers losing less to anti-aircraft fire, now that a
      bombed gun is felt within the hour and not at midnight. **It has not been reviewed in play.**
   2. **revolt risk - built** (2026-10-15, by decision: "Ok commit and continue"; `docs/mechanics/revolt.md`;
      `CSuppression` and `CRevoltRisk` in `Sim/Revolts`, `CProvinceDay` and `CNearProvinces` in `Sim/Provinces`,
      `CRevoltConditions` in `Sim/Script`, `CRevoltRiskTooltip` and `CRevoltMapColours` in `Interface`): suppression,
      each friendly unit's brigades by their strength over every land province within two edges under one holder, the
      province itself twice; the day's step of a tenth towards the policy's `local_partisan_support`, less a tenth of
      the suppression at most 0.2, only at war, so it freezes at peace; each province's day now one province at a time
      in the original's order - repair, suppression, the rebuild, the step, the return of land; the figure read and
      shown, and the `revolt_risk` static scaled by it as the rebuild's last step; the four conditions, the effect and
      the history's line; a new game's partisan support. **Checked against the 7 and 8 January saves**: Italy's 9987
      gets suppression 0.692 and steps to the save's 0.031 (`OpenHOI3.SaveCheck revolt`). The revolt map mode, after the
      air one on `Tab` and `--mode=revolt`, tried with `--click`; the province view's figure and tooltip are written
      and tested, and wait for the province view. **It has not been reviewed in play.**
   3. **rebels - built** (2026-10-15, by decision: "Ok commit and continue"; `docs/mechanics/rebels.md`;
      `CRebelType`, `CRebelFaction`, `CRebelFactions`, `CRebelChoice`, `CRisings`, `CRebelArmies`, `CRevoltRoll`,
      `CRebelSiege`, `CRebelFactionDay`, `CRebelFactionMonth` and `CResilientRetreat` in `Sim/Revolts`, `CRelease` in
      `Sim/Wars`, `CRebelTypeReader` in `Content/Script`): the rebel types of `common/rebel_types.txt`; the roll in each
      country's day, a draw under the risk over 50 in 10000 - 20000 for the AI - and then one in 365; the type a
      province chooses by its `spawn_chance` and the country an independence type frees; a rising, the faction it
      joins or makes, with its government and its name, and its army of one to four brigades at -1.0 plus the
      occupier's partisan efficiency and below the occupier's technology; who a rebel army takes a province for -
      partisans hand their owner's land straight back - and the siege won; a faction's day, handing land back,
      dropping armies, marching by `movement_evaluation` and disbanding; its month, independence at 15% once the land
      has been held `defect_delay` months, with the release, the armies handed over and a revolutionary war on each
      former owner; `release`, `create_revolt` and `<rebel type> = N`; a province history's `revolt`; `REB` in the core
      effects and `is_core`; the rebels everybody's enemy; `PROVINCELOST` on every capture, `REVOLT`, `PARTISANS`,
      `REVOLTSIEGE` and `INDEPENDANCE`; and the tooltip's "Likely Rebels". Defection, demands and acceptance are built
      as the original has them, which is not at all. **No save has a rising**, so `OpenHOI3.SaveCheck rebels` is a
      trial. **Reading for it read `ReleaseProvinces` and the rebels' capture on the listing, and the country's officer
      figure, which shows a rebel brigade at nought organisation after its first hour** - the rebels have no officers and
      their ratio is counted like anyone's (`rebels.md`, section 13); it is confirmed that the original's rebels have none. The
      script check now reports Their Finest Hour's two `unit_transfer` lines, a key the original drops: thirteen
      problems. **It has not been reviewed in play.**
   4. **undergrounds - built** (2026-10-15, by decision: "Ok. Commit and continue"; `docs/mechanics/undergrounds.md`;
      `CUndergrounds` in `Sim/Revolts`, `CChangeUndergroundMissionCommand`, `CSpawnPartisanCommand`,
      `CSpawnUndergroundCommand`, `CSpawnFullRevoltCommand` and our `CPlaceUndergroundCommand` in `Sim/Commands`):
      the building that carries `local_underground`, its owner and its action; what a finished one starts at, falling
      from 0.4 at the builder's or an ally's nearest air base to nothing at `MAX_UNDERGROUND_DISTANCE`, 0.4 flat for an
      exile in its own land, and where one may be built; the day in each country's - growth by the policy's support,
      found by the garrison's suppression, gone where the owner holds the province again, and the action; partisans
      beside it for its owner, triggered-only types allowed, paying 0.5; the spread to a neighbour of one owner, paying
      1.0, and nothing where it misses; the four commands; Organized Revolt on the politics screen with its tooltip;
      the dissolution when the owner's side retakes a province; and red on the revolt map. **Ordering one is not
      built**: the queue holds units only, and the decision of 2026-10-15 was "Undergrounds now, queue
      later" - `--underground=POL:1440:0` finishes one as a line would. No save holds one, so `OpenHOI3.SaveCheck
      underground` is a trial. The province view gives their mission and spawns (`provinceview.md`); the AI's management of them is not built. **It
      has not been reviewed in play.**

**Then: buildings in the build queue - built** (2026-10-15, by decision: "Undergrounds now, queue
later" and "Ok commit and continue"; `docs/mechanics/buildinglines.md`; `CBuildingLines`, `CBuildingDeployment` in
`Sim/Construction`, `CConstructBuildingCommand` and `CDeployBuildingCommand` in `Sim/Commands`, `CConfirmBuildText`
and `CDeploymentColours` in `Interface`, `CConfirmBuildDialog` in `src/Screens`). Read first in the fact base's
building-queue wave (`reversing/findings/FINDINGS-buildingqueue.md`). A building's `cost`, `time`, `on_completion`,
`completion_size`, `port` and its two lists of buildings read; **the production screen's eight building buttons**,
enabled by the technology that makes the building available and the underground's where the country may train one,
open **the confirm-build dialog** - parallel lines of serial buildings, their cost and time, the colour of what the
share leaves, what it teaches - whose `accept` orders them with no province; **the price**, cost and time by one
category discount and nothing else (159 of 171 lines in the saves to the thousandth); the day as a unit's line, with
the building line's own test that cancels one in lost land; **finished**, the practical in full and one more level
built and none of it working, which the day's repair fills over about ten days, `BUILDING_DONE` to the province's
holder - or, with no province, **a building waiting in the deploy menu**, placed by a click where the map shows grey
and green as the original's mode 14 does; and every line priced again, the country's modifier made again first, on
any day a line is finished, a unit's as well. Tried with `--click`: Germany's air base ordered, finished on 25 March,
placed. **It has not been reviewed in play.** The AI's building is not built; convoys are, since (below).

**Then: the province view - built** (2026-10-15, first of the six pieces in the order decided that day;
`docs/mechanics/provinceview.md`; `CProvinceView` and its four row entries in `src/Screens`, `CProvinceViewText`,
`CProvinceBuildingRows`, `CProvinceUndergroundText`, `CProvinceCovertOpsText` and `CProvinceViewTooltips` in
`Interface`, `CAlliedObjectives` in `Sim/Ai` and `CSetAlliedObjectiveCommand`). Read first in the fact base's
province-view wave (`reversing/findings/FINDINGS-provinceview.md`). **It follows the selection**: a click on a land
province opens it, Shift adds or takes out one, a unit or a sea zone or Escape or its close button lets go; with several
selected it says "Multiple Selected(N)". Written as a province is selected, as the original does: the name, both flags,
the terrain's picture, what the player knows, revolt risk, manpower, leadership and the four resources - **"?" below
full intelligence** - the strategic resource, the ways out with their kinds and the cores; every frame the weather strip
(nought: there is no weather), **the building rows** - each with a key of its own, its pips green, orange and grey, one
level a click in every selected province held - **the underground window** with its bar, its suppression, its mission
stepped by either button and its two spawns, **the covert-operations panel** and **the allied-objectives panel**, whose
asking is kept and read by nothing, the AI not being built. Every element's tooltip. A button now takes the size of a
sprite swapped onto it, as when it is made. Tried with `--click`: Berlin, two provinces, the panel and its tooltip, an
underground. **It has not been reviewed in play.** Every selected province is lit on the map, as an account of the original has it, and the scaffolding readout under the top bar is put away behind a switch of a debug menu of ours on Shift and 4, by decision.

**Then: convoys in the build queue - built** (2026-10-15, second of the six; `docs/mechanics/convoylines.md`;
`CConvoyLines` in `Sim/Construction`, `CConstructConvoyCommand`, the confirm-build dialog's two convoy modes). Read
first in the fact base's convoy-line wave (`reversing/findings/FINDINGS-convoylines.md`). The production screen's
`convoy` and `escort` buttons open the confirm-build dialog for transports or escorts - `convoy` always, `escort` once a
technology gives `allow_escorts` - and `accept` orders `parallel` lines of `serial` items. **An item is ten ships**
(`CONVOY_CONSTRUCTION_SIZE`), at 2 IC a day for 100 days or 4 for 240 (the four defines, times what the technology
gives); **a line is priced once, when it is ordered, and never again**; each finished item adds its ships to the
country's free transports or escorts, what was paid past it going to the next. The counts beside the buttons say
`(+N)`, the queue line "Convoy" or "Escort" with its own counter, and **while the dialog is up nothing behind it on the
screen has a tooltip**, as the original's - for a building's dialog too. `OpenHOI3.SaveCheck research` prices every
line of a save, 107 of 107 on 1 April, and works the lines paid in full forward: 69 of 70 to 20 April. Tried with
`--click`. **It has not been reviewed in play.**

**Then: weather - built** (2026-10-15, third of the six; `docs/mechanics/weather.md`; `Sim/Weather` -
`CWeatherFronts`, `CProvinceWeather`, `CClimate`, `CWeatherPass` - with `CWeatherMapColours` and `CWeatherMapText` in
`Interface/Map` and `CWeatherLayer` in `src/Map`). Read first in the fact base's weather wave
(`reversing/findings/FINDINGS-weathersim.md`), which consolidated and corrected its three earlier weather write-ups.
**Fronts** are born at `default.map`'s low pressure zones and step across the map handing on humidity, low pressure, wind
and warmth; **high pressure** spreads from its zones; **every province's hour** makes cloud, rain, mud and frost and pulls
its temperature toward a target from `climate.bmp`, its terrain, a **season** made again each day and the night; **a new
game runs 96 hours of it first**. Every edge now has its bearing, all 83,596 the base game's cache's, and every province
its climate index, 14,168 of 14,168. **Set beside two reference saves an hour apart, every front's provinces
agree in order and every province's eight figures but some 48 land provinces' - and each of those agrees given another
terrain**: the map's terrain rule, `map.md`'s open question, which `OpenHOI3.SaveCheck weather` now measures. **The
weather map mode** (`Tab` after the terrain) greys frozen ground and draws the original's plain weather icons, as with
`alwaysCounters` on - the only way here (`docs/compatibility.md`, entry 12); its tooltip is the original's. Everything that reads weather - a march, battles on land, at sea and in the
air, supply's mud, sighting at sea - now meets it. Tried with `--mode=weather`. **Screenshots of the original's weather mode were given on 2026-10-15, which measured the pictures; ours has not been reviewed in play.**

**Then: transport and invasion - the first half built** (2026-10-15, fourth of the six; `docs/mechanics/transport.md`,
sections 1 to 12; `Sim/Transport` - `CTransport`, `CLanding`, `CInvasionWarnings` - with `CTransportMission` and
`CInvasionMission` in `Sim/Missions`, `CLoadUnitCommand` and `CUnloadUnitCommand`, `CTransportText` in
`Interface/Screens` and the panel's `CLoadedUnitsEntry`). Read first in the fact base's transport wave
(`reversing/findings/FINDINGS-transport.md`). **A land unit boards a fleet** by the panel's load button - the first fleet
in its port with room, at once and for nothing - or by marching into a sea where its own fleet waits; **a fleet carries its
ships' `transport_capability`** against each unit's `transport_weight`; passengers sail with it, regain nothing at sea,
dig in no further, are not reinforced, take the transports' damage in a sea battle and are lost a brigade at a time when
the fleet can no longer carry them; they get off where the fleet ends its way in port, by the unload button in port, or
**by landing**: 24 at the landing's speed, aboard all the while, with **an attack and a defence scale by the progress**
and the amphibious penalty a hundredth worse with no landing craft - set beside three screenshots of the original's of a
landing at Danzig figure for figure. **The transport and invasion missions** are built, and **`INVADEDUS`** warns the
player the hour after a landing is ordered. On screen: the load and unload buttons, a fleet's list of what it carries
("Can carry 90 (120) weight."), a passenger's counter at sea, the landing's arrow, the orders window's two rows and a
landing unit's battle tooltip. `OpenHOI3.SaveCheck march` now carries the save's passengers - the French fleet's, 1 of 1 -
and `landing` is a trial of a landing. Tried with `--click`. **It has not been reviewed in play.**

**Then: the carrier and carrying by air - built** (2026-10-16, the second half of the fourth; `transport.md`, section 13;
`CCarriers` and `CAirTransport` in `Sim/Transport`, `CParadrop` and `CAirSupply` in `Sim/Missions`, `CLoadOnPlaneCommand`
and `CUnloadFromPlaneCommand`). **A carrier is an air base**: rebase to carrier puts a group of carrier air groups on a
ship of its own country's fleet with room - `carrier_size` wings a ship - at once, and flies it there with no range
measured; aboard, its base is wherever the fleet is, it is at its base only while the fleet is in no battle, it goes
with the fleet when idle, and with no order it is given **carrier protection**, which fights in the air while a battle
goes on in the carrier's province and bombs the enemy's ships there - a fleet in a battle sends it in. A carrier sunk
lets its groups go to the nearest air base. **A land unit boards an air unit** at its base by the panel's plane button
- every brigade `can_paradrop` and at 95% organisation, its weight within the planes' `transport_capability` - and goes
where the planes go. **The paradrop** takes off from its start, at least 24 hours ahead, once the troops are organised
and their attack delay is over, drops them with 7 days of supplies and leaves the planes at a hundredth of their
organisation; a dropped unit attacks with `PARADROP_PENALTY`, wants no supplies, counts once in the width and keeps two
thirds of its organisation its first hour out of a battle; `PARATROOPEDUS` tells the player. **The air supply** carries
to the last short province of its area what it went short of, up to half the planes' capability. Read through further
for it, which settled that **a transport plane shot down takes its paratroopers with it**. On screen: the plane buttons,
the air window's four rows and their order lines. `OpenHOI3.SaveCheck airborne` is a trial of the three - a drop on
Poland, a group of HMS Furious off to Glasgow and back, an air supply to Garmisch. **Nothing of it was looked at on
screen, and it has not been reviewed in play.**

**Then: selection - the drag box and Shift - built** (2026-10-16, asked for before the headless run: "implement the
drag-box-selection; implement the shift-multiselect for units"; `docs/mechanics/selection.md`; `CSelection`,
`CBoxSelection` and `CSelectionGroups` in `Interface/Map`, `CMultiUnitText` and `CMultiUnitTooltips`,
`CSetSelectionGroupCommand`, `CMultiUnitPanel` and `CSelectionBox` in `src`). Read first in the fact base's selection
wave (`reversing/findings/FINDINGS-selection.md`). **The left button acts on its release**: moved 2.0 map units or more
it is a drag, drawn as a white outline two pixels wide, and lets go of the selection - unless Shift is held with units
selected - and takes **the player's land units in it, or if none its fleets, or if none its air units**, a fleet in
port by its naval base and an air unit at its base by its air base; otherwise a click. **A selection is one kind and
the player's own**: Shift and a click adds a counter's unit or takes it out, Ctrl and a click on a land unit - or on a
row of the single panel - brings its whole command, **the single panel's ribbon of the units under it adds them all**
(a screenshot of the original and an account of it, read after), and a click on another country's counter selects nothing. **A right click gives every selected unit its own
order**; several fleets or air units without Shift bring the orders window up for the first and move nobody, and its
Accept gives the order to all of them. Every selected counter wears the ring; the first land unit's hierarchy is drawn.
**Two or more selected bring up `multi_unitpanel`** with a row each - deselect, "select this" (with Ctrl its command,
with Shift kept until Shift is let go), the leader - the three support checkboxes, the multi-unit load, plane-load and
unmount, reorganise (since 2026-11-04, `reorganise.md`), merge (the same day, `selection.md`, section 5.4 - the AI's
join orders merge the same way since), and close; upgrade-all, detach, higher command and disband are shown and not
built. The single panel's new-unit button works the same day (`unitpanel.md`, section 7.1): an empty unit beside the
selected one with the reorganise window between them, or with Ctrl a unit for every brigade but one. **The
digits are selection groups** kept in the game state - Ctrl and a digit sets one, a digit recalls it, again goes to
it. Tried with `--click` (`sd` a drag let go with Shift, `D` one held, `ck1` Ctrl and 1): a box over western Germany,
nine units sent to Cottbus, Ctrl+1 and 1, three fleets from two ports given one move. **It has not been reviewed
in play.**

**Then: stage 2's headless run - made** (2026-10-16, the fifth of the order decided; `docs/mechanics/headless.md`;
`OpenHOI3.SaveCheck headless`). A new game with the install's Lua and nobody playing, run hour by hour from 1 January
1936 to 1 January 1948 with nothing given by hand: **it completes, in 44 minutes** (192 seconds a year, Release), no
country's goods or manpower below nought at any day's end. Its first attempt threw on 29 April 1938 - **a unit's draw
divided by a need of nought** where its capital's pool was below nought; the original's division answers -1 there,
read on its bytes, and ours now does. 1,107 events fired and 994 trade routes stand at the end; nothing is
researched, nothing is built after 1938, goods pile up to the ceiling and the sliders stay where they started - the
AI's work. **What it found to mend, in order**: `country_event` and `split_troops` not built, which end the Spanish
Civil War on its first day and leave Tannu Tuva alone; the classes handed to the Lua lacking a province's
`GetNumberOfUnits` (448 errors, all Finland's politics minister); `lost_national`, which keeps a triggered modifier off
(`headless.md`, section 4). Set beside the save of 20 April 1936 fuel, oil and manpower are the save's to a tenth,
resources a third high, research a fifth behind.

**Then: the run's four fixes - built** (2026-10-16, by decision: "Do the four fixes first";
`docs/mechanics/effects.md`, section 9; `CEventEffects`, `CSplitTroops`, `CScriptedBrigades`; `triggers.md`, `lua.md`).
`country_event` fires its event at once where the event's trigger holds, FROM the root's country; `split_troops` hands
units on a roll each until a share of the brigades has gone, with their officers and a share of the manpower; a unit
type's name gives a brigade to the emptiest unit with room, or a unit of its own, at half strength; `lost_national`
is the share of a country's owned cores it does not hold; a province's `GetNumberOfUnits` counts every unit in it.
Each was read whole first - three of them already in the record, the rest in the fact base the same day
(`FINDINGS-effectrun.md`, section 1.12). Two things settled the same day: **a scripted brigade comes with
every technology at nought**, and a split unit's move to a neighbour is the original's **march back** - read then: a
unit handed over steps aside and, where anyone was left behind, marches straight back, so it is seen moved only where
nobody was. **The Spanish Civil War still ends on its first day here, and waits for the AI**: the event declares the
war before it splits the troops, and the Republic's divisions standing in the Nationalists' new land take it at the
declaration; in the original the AI has moved them to the Portuguese border by then (`effects.md`, section 9).

**Then: stage 5, the AI - its first piece, the tech and production ministers, built** (2026-10-16, the sixth of the
order decided; `docs/mechanics/ministers.md`; `reversing/findings/FINDINGS-ministers.md`, a reading wave of four
the same day). Both are the install's own Lua, so what was built is everything around them: the tech minister's day,
`TechMinister_Tick` with both its arguments true, setting the four leadership shares and filling the research slots,
and a finished level of a technology of several levels taken off an AI's queue again; the production minister's day,
`ProductionMinister_Tick` - convoys, then units in the country's ratios and buildings - and its desperation, made again
three days a month; the daily `BalanceProductionSliders` at the end of every AI country's day, with
`FastNormalizeByPriority` and its hardcoded priority table; the AI's commands, its shares **rounded half away from
nought where the player's truncate** (England's leadership in the April save to the 32768th); the AI's placing of a
finished unit, a lone brigade joining a fitting division in the capital's area and anything else going to the area's
best province; and about seventy engine methods the two scripts reach (`CCountryEconomyMethods`, `CTechnologyClass`,
`CBuildingClass`, `CConstructionClass`, `CProductionCommandClass` and the rest in `lib/OpenHOI3.Lua/Api`). **The
reading corrected the fact base**: the production minister's script runs every day, not twice a month, and the
`automate_*` keys are not the delegation flags. On the way the ration of an elite brigade became the original's, by the
type's group and not its branch (`builders.md`, section 2). **Set beside the reference saves**: the summed research
progress of the eight majors on 11 March 1936 is the Ireland game's to within a few levels - Germany 12.10 against
12.20, the United States 9.20 against 9.02 - and a short headless run to 1937 has every country researching and
building, 107 units more placed, and no Lua error. What the AI still lacks is in *Loose ends*.

**Then: stage 5's second piece, decisions, built** (2026-10-16, by decision: "continue with decisions";
`docs/mechanics/foreignminister.md`; `reversing/findings/FINDINGS-foreignminister.md`, read the same day). The foreign
minister's day for every country nobody plays, in the hour `(number + 13) mod 24`: the decisions pass -
`ForeignMinister_EvaluateDecision` asked of each diplomatic decision that may be taken, all in one scope with one draw,
an answer above nought posting it - and **the prepared wars** a script ties a decision to (`PrepareWarDecision`, with
`IsPreparingWar` and `IsPreparingWarWith` real for the politics script now), pruned for a great country and walked
after the pass, a ready one's decision posted without the Lua. **Readiness counts fronts the military AI draws**, so it
is built only where there are none: Germany's Danzig and Japan's Marco Polo Bridge wait. **The reading corrected the
fact base**: the decision's score is rounded to the nearest, not cut. A headless run to 1938 has the AI take seven
decisions where it took none - the New Deal, the Popular Front, the three interventions in Spain among them - with no
Lua error, once `GetDayOfMonth` and the ceiling for war were handed to the scripts.

**Then: stage 5's third piece, the foreign minister whole, built** (2026-10-16, by decision: "Complete the
foreign minister"; `docs/mechanics/foreignminister.md`; `FINDINGS-foreignminister.md` section I, read the same day). The
engine now runs `ForeignMinister_Tick` itself - the decisions pass is its first call - so every country nobody plays
calls its allies in, asks and gives transit rights, proposes wars, offers and breaks pacts, influences and aligns as the
install's Lua decides; the thirteen diplomatic action classes the scripts make (`CDiplomaticActionClass`), the day's
best proposal posted above 50, the proposed war, `PercOccupied`, a new minister's first work skipped, a ready war with
no decision declared - `conquer`, or the three goals the engine names by tag - and `ForeignMinister_OnWar` told of every
new pair at war. **Set beside the Ireland game's saves, where the original's AI plays every major, the same countries
align and are influenced within days of the original's** - Schweiz and Sweden to the Axis, Turkey to the Comintern,
Britain and South Africa - and neither side makes a NAP by April. A run to 1938 has no Lua error;
`OpenHOI3.SaveCheck foreignai` tells every offer the ministers make.

**Then: stage 5's fourth piece, the AI's strategy, built** (2026-10-16, by decision: "go ahead with it", the
strategy before the military AI; `docs/mechanics/aistrategy.md`; `FINDINGS-aistrategy.md`, read the same day). What each
country's AI makes of every other, which the scripts' `GetFriendliness`, `GetAntagonism`, `GetThreat`,
`GetProtectionism` and `IsMilitarist` read: the personality from the government's ideologies on the triangle, and the
lists - provinces to take and hold, threats, antagonism, rivals, friends, puppets wanted and countries protected - each
weighed by the engine's own terms (casus belli, faction corners, border share, strength, reach, size) and by a walk of
the faction's chosen victory conditions, made as a game is entered, every ten days and at a change of war. **Set beside
the start save, every list agrees entry for entry, in order and weight, for all 78 countries that have one**
(`OpenHOI3.SaveCheck aistrategy`). **The check corrected the fact base** - `protect`'s cap is six and twelve, not seven
and fourteen - and the walk's region scope was read for it. The foreign minister still offers no NAP, guarantee or
alliance in 180 days, and neither did the original by April.

**Then: the military AI's first piece, fronts and theatres, built** (2026-10-17, by decision: "Ok lets do the
military AI", cut into pieces here; `docs/mechanics/theatres.md`; `FINDINGS-theatres.md`, read the same day). Every
area's fronts against another holder's land, a country's fronts of an area split by the enemy it faces - the
front-worthy enemies by the original's compiled-in table - and hot at war; the country's theatres made area by area,
merged and set beside the old by overlap, refreshed on a change of hands and looked after each hour with their
priorities; each theatre's headquarters adopted from a theatre-level unit standing in it or made behind its front with
one `hq_brigade` at a fifth (`CCreateNewHQCommand`'s), and a theatre-level unit no theatre owns disbanded. **Set beside
the start save, all 108 theatres of all 78 countries agree** - number, headquarters with its name and place, heat, key
province, fronts and provinces in order - and the next theatre number, 312 (`OpenHOI3.SaveCheck theatres`). **The check
corrected the fact base twice**: the cached tags past the 28th are not in the array's order, which moved every
front-worthy list after them, and a unit's name is kept per country, not for the game. With the fronts, **readiness for
a prepared war is answered whole** (`foreignminister.md`, section 7).

**Then: the military AI's second piece, the unit agents, built** (2026-10-17, by decision: "Continue with the
next step"; `docs/mechanics/agents.md`; `FINDINGS-agents.md`, read the same day by seven readers). One agent for each
theatre of a country the AI runs: at its first pass the country's whole order of battle is **sorted into the theatres
its units stand in**; each agent is then bound to its theatre's headquarters, switching its battle plan on, and once a
day **builds the ladder of commands under it** - a corps for every four divisions and one more, armies over corps, army
groups over armies, a level a day, every unit kept under or moved to the nearest command with room for five - walks its
headquarters to where they are wanted, **splits fleets and air groups over their caps into units of one and merges small
ones** by join orders, and keeps the theatre's **plan**: stance, air and naval stance, the AI's power against the enemy's,
objectives, an area of operations and the aircraft it wants; divisions are balanced between a country's theatres by need.
**Set beside the 4 January save after 84 hours, every plan's active, three stances, area of operations in order and
objectives agree for all 106 theatres**, and the ladder 87 to 99 of 106 by level (`OpenHOI3.SaveCheck agents`). **The
readings corrected the fact base** - the sort was recorded as an interface call; a slot-78 flag was a frontier at war;
the movers between theatres move nothing by march; an objective's priority is read. Nothing moves a unit towards an enemy
or attacks yet.

**Then: the military AI's third piece, moving and attacking, built** (2026-10-19, by decision: "Ok lets go";
`docs/mechanics/moving.md`; `FINDINGS-moving.md`, read the same day by six readers). The division need read whole
(`agents.md`, section 7.5, the capital's naval floor giving every European Soviet theatre 18); each agent's divisions
committed or held back by its powers, its area's positions scored, the ways to its cut-off land and the second ring; the
hold list; the land block - garrisons, the line at most five a position, the reserve, the fallback from a losing defence,
long marches made strategic redeployments (an order now, with its speed by the roads) - and the attack, its odds by the
AI's own sum of modifiers, a support attack (an order now) for a unit that must stay and a halt for one called off; the
axis; the sea targets handed out, the naval bomber's want, the fleets' hour - home, raids, rebasing, patrols and
intercepts; the sealift's embark and debark and invasions; the movers of fleets and transports between theatres; the
AI's leaders and its stranded units walked home; and **the air** - worn groups rested, groups rebased to the best air
base for their role, then close support, intercepts, logistical strikes and naval and port strikes. **Set beside the 4
January save**, 774 of 908 divisions move or stay as the original's and 275 of 542 head for its province, the axis 106 of
106, embark and debark 93 of 106, air units stand where the original's do 53 of 59, and **the save's three AI air orders,
Italy's intercepts, are made again at their stance and province, each an hour early**; fleets come out 221 to its 222. A headless 1936
runs clean in six minutes and keeps the Spanish Civil War going once it fires. Not built: paradrops (no 1936 order of
battle has an airborne army) and strategic bombing (nothing hands the agents the enemy's war industry).

**Next: waiting on the user's word.** A headless run to 1939 (`headless.md`, section 4, 2026-10-19) found three
things worth looking into first: **China surrenders to Japan in five months**, the Marco Polo Bridge comes a year late, and
the Spanish Civil War is over in six months; and 40 Lua errors from a misspelt faction in Japan's script. **On
2026-11-02 the Marco Polo Bridge was mended** - it comes on 1 July 1937 as the original's does, the reference Ireland
game played on to August 1938 being the measure (`moving.md`, section 21): a province's front was read off a cache, and
the consumer goods demand left out the standing army, so every country the AI runs built too much. **China now lasts six
months to the original's thirteen and a half**, ending the original's way by `china_war_goal`'s event; its technology,
32 levels summed to the save's 56, was the first suspect (`headless.md`, section 4, the last run). **Mended the same
day**: a new game gives every country, the player's too, the original's head start in technology and brings its laws
forward (`research.md`, section 9; `politics.md`, section 13; `docs/compatibility.md`, entry 6 reversed) - every
technology level of the start save, 28,987 of 28,987, and its laws, 707 of 707. Every start-date check run again after
it gives the same or better - alignment 80 of 101, the free spies 101 of 101 and the levels abroad 10052 of 10100, the
ladder of commands to 4 January 91 to 99 of 106 by level, the theatres, the AI's strategy and the moving figures
unchanged - but one: **dissent 85 of 101**, where every miss was 0.018 before, because a new game's consumer goods
share is set before any unit exists and so without the standing army's term (`politics.md`, section 10; with the term
taken out, 94 of 101). **The second suspect is how our China
loses its army**: the original's keeps 80 to 88 units through the war where ours falls from 90 to 54 by December 1937,
and its front breaks in November.

**The replay is built** (2026-11-03, decided as the next step;
`docs/replay.md`, `Sim/Replay`). The AI's steps of the hour are recorded as the game runs - the commands each posted, the
draws it took of the game's generator and the unit numbers it took - and a replay puts them in place of the steps, so
everything else runs again without the AI's cost, some seventy per cent of an hour (`OpenHOI3.SaveCheck profile`: of thirty
days of 1936, the unit agents 10.8 s and the Lua's trading 7.7 s). A recording holds while the rules do not change: it
serves a trace added to a run and a bug reproduced at its hour, not a fix tried. **Two runs of a game agree hash for
hash**, and `OpenHOI3.SaveCheck replaycheck` set a recorded game and its replay side by side **608 of 608 days to 1
September 1937**, through the Spanish Civil War and the war in China, the replay's hours 224 s to the recording's 584 s, every step taken through the file's form and back;
`headless ... record=FILE` and `replay=FILE` do it through a file, about 320 KB a month. No generator of the AI's own
was needed - a replay moves the game's on by the draws the step took - and entry 11 stands. The check mended three
things: the unit numbers the regrouping takes as it posts a split, the naval intercept's joins given directly where every
other join posts a command, and the theatres, which make headquarters units and now run in a replay too. **The client
records a game played with `--record=FILE` and plays one back with `--replay=FILE`** - the players' commands recorded as
they are posted from outside the game, with the drains of the queue before each, the countries played and the seed in the
header - played by whoever played it, looking on and taking no order: any unit selected, the province view and the top bar
up, nothing pressable, and where nobody played each province and unit seen through its own country's eyes - the
clock stopping where the recording ends (`replay.md`, sections 6 and 8); ten days played as Germany replayed hash for hash.
Any country to look through in a game nobody played, `--observe=TAG`, is not built. **A
savegame of our own**, to start a run at 30 June 1937, would save more and serve a fix as well: the game and what of the
AI's own the original's saves hold - theatres, plans, strategy lists - and not the Lua's state, which the original does not
save either and Their Finest Hour's scripts do not keep (`replay.md`, section 7) - not begun.

**Next: China** (2026-11-02, the runs' traces in `OpenHOI3.SaveCheck headless ... inspect=CHI`). Every rule on the
way a Chinese unit is lost is the original's as read: 32 of 50 lost to the end of the war were falling back with no
supplies onto ground Japan held, 13 beaten with nowhere to go, 6 overtaken with enemies where they stood and ahead. A
retreat begins with the attack delay still running and stands until it is out, which was tried in the
original the same day (`landcombat.md`, section 9). What differs is the war: the original's China loses 13 units in
eight months, ours 50 in six; set beside the Ireland game's saves month by month, **ours attacks less - 3 to 10 of its
units carry an attack delay where the original's carry 15 to 39 - and falls back far more, 9 to 27 units at a time from
October where the original has 1 to 5**, with as many Japanese units on its land in both. 113 of its 395 retreats to
the end of the war were the AI ordering a defender out of a battle, not a battle lost. **Read on 2026-11-03**
(`FINDINGS-moving.md` A.4e; `moving.md`, sections 7.6 and 21): the original's AI does order a defender out - no land
battle holds a unit - but the line, the pull, the garrisons, the sealift and the division picker first ask
`CAIUnit_UnitMayLeave`, which ours had nowhere, and which keeps a division of an agent below a theatre beside enemy land
unless more idle divisions stay than enemy units face it; and the rear passes over a division holding ground, which ours
had left out. Built, with the line's stay test and two misreadings in the sealift's mended: to 4 January 777 of 908
divisions move or stay as the save's (774) and 298 of 542 head for its province (275). **And China then lasts less**:
with the rules switched on at 1 July 1937 on a probe, the runs alike until then, its army is 68 units in December 1937
to 83 without, and it surrenders on 28 January 1938 to 19 March without - so the defenders taken out of battles were
keeping it alive, and are not the gap. **Next in it**: how often our China wins and loses its defences, set against the
original's - what its battles are made of, not who orders them. Not built from the same reading: the agent's answer to
a province lost in a small area (`OnProvinceControllerChanged`, `moving.md` section 20).

**Speed and the interface came next** (2026-11-03, by decision: the interface still lacked several parts, and the game
wanted optimising, the original running noticeably faster). The original runs a day of early 1936 in
about 500 ms; ours ran 1094 and, with country tags compared as numbers, the surrender's province count kept, one walk of
a country's units and the air bases kept for the hour, about 760 - the January hashes unchanged (`--perf=SECONDS` in the
client writes where a frame's time goes: nearly all of it the game's hours). Running the trading beside the rest of the
hour's AI was tried and made the hour slower on the test machine; it is not built. **The outliner is built**
(`docs/mechanics/outliner.md`, read the same day in one wave, `reversing/findings/FINDINGS-outliner.md`): its nine
sections under their switches, read from `settings.txt`, the battles, the tree of units with its expand buttons, the
allied objectives and bombings, every row's figure and tooltip and its click, set beside screenshots of the original's -
the land units' tree and the fleets' counts looked at on screen, a battle's row only tested. **The land unit's orders
window is built** (2026-11-04, `docs/mechanics/landorders.md`, read in one wave set against two
screenshots of the original's, `reversing/findings/FINDINGS-landorders.md`): Ctrl and a right click, always Move Order, Support Attack and
Strategic Redeployment greyed by each order's own test, the first of support, move and redeployment ticked, the date
widget an order does not start before, *Synchronise Arrival* under Move Order, the day or night at the date's hour - and
with it the two orders as the original's: a support attack waits beside the province for somebody else's land battle
and joins it, a redeployment starts its attack delay every hour; and a unit's two order hours in the march. Tried with
`--click` and set beside the screenshots: a switched-off checkbox shows its fourth frame, the grey box, and a font of
one advance - the date widget's `timefont` - is drawn a character to a cell, so the date stands in its boxes in the air
window too. Settled for the redeployment's row being open for a province it cannot reach: the original's.
To 4 January the AI's divisions stand as before, 777 of 908. **The reorganise window is built** (2026-11-04,
`docs/mechanics/reorganise.md`, read in one wave set against two screenshots of the original's,
`reversing/findings/FINDINGS-reorgwindow.md`): the multi-unit panel's button for two units that may be reorganised, the
first's brigades left and the second's right, rows made once with arrows never greyed, the move deciding by the
original's eleven tests - the division's limit, a headquarters' first brigade, types not buildable, carriers - a fleet's
positioning penalty without its hundredfold and its ratio counting no submarine, and an emptied unit disbanded as the
window shuts. Tried with `--click`; a `format` in quotes is not taken, measured on the screenshots. The AI's regrouping
goes through the same test, and to 4 January nothing it does differs. The multi-unit panel's merge and the single
panel's new-unit button followed the same day (`selection.md`, section 5.4; `unitpanel.md`, section 7.1). **The
right-click menu and the five side menus were read and are not built** (2026-11-04,
`reversing/findings/FINDINGS-leftovermenus.md`): nothing in the original shows them to a player, and the
decision was not to build them (`docs/compatibility.md`, entry 14). **The in-game menu is built** (2026-11-04,
`docs/mechanics/ingamemenu.md`, read in one wave set against a screenshot of the original's,
`reversing/findings/FINDINGS-ingamemenu.md`): Escape with nothing selected and the top bar's menu button open it on its
six buttons, the clock stopped and the selection let go, and nothing behind it takes a click or a key - an
account of the original; Close goes back from its windows to the buttons and leaves the
clock stopped once one has been open - the original's, which was confirmed in it; Quit asks and goes to the
desktop; and the message settings list every type with its four boxes, the tabs, Reset, and a Close that writes
`messagetypes_custom.txt`, which is now read at every start over the install's types. Tried with `--click`: the
reference file comes out byte for byte as the original wrote it. **Resign goes to the main menu** since 2026-11-06
(`frontend.md`), through no end-of-game screen (*Loose ends*); Save Game opens the save dialog since 2026-11-05 (`savegame.md`, section 11). **The game options window is built** (2026-11-04,
`docs/mechanics/gameoptions.md`, read in one wave, `reversing/findings/FINDINGS-settingswindow.md`): the four tabs,
Apply storing every tab and writing the player's `settings.txt` - the original's 21 keys in its order and format, set
beside two reference files byte for byte by `OpenHOI3.SaveCheck settings` - with the restart notice for a
changed mode or multisampling and otherwise the whole menu shut, as confirmed in the original; Back,
the autosave frequency, the screen's modes, the gamma as a brightness layer live as it moves, and the rest kept and
written. The session writes the file as it ends, as the original does on leaving a game. Tried with `--click`.
**Savegames are under way** (2026-11-04, `docs/mechanics/savegame.md`, read in one wave,
`reversing/findings/FINDINGS-savegame.md`), in the original's `.hoi3` format both ways, by decision: **the
loader's first increment is built** - `--load=<save>` and `OpenHOI3.SaveCheck load` - with the clock, the generator
(restored, `docs/compatibility.md` entry 15), the played country, every province's and country's figures and the
factions; the 7 January 1936 save gives owners 14,168 of 14,168, laws 707 of 707 and technology levels 28,987 of
28,987, and runs on. **The second increment is built too** (`savegame.md`, section 5): the units - every tree with
its brigades, orders, plans, carriers' air groups and passengers - the leaders in service, the queue's three kinds of
line, what waits to be placed and the templates; the 7 January save gives 2,001 of 2,001 units, 4,072 of 4,072
brigades, 3,303 of 3,303 leaders and 179 of 179 lines, the Ireland game's of August 1938 all of its 2,304, 5,127, 4,623
and 253, and four hours on to the next save Germany's every brigade's organisation. **The third's first half is built
too** (section 6): the wars with their goals, every kind of pact, the trade routes, the convoys and mobilisation, all of
both saves' read exactly; the reading mended the Clausewitz reader, which read everything after a block with no key a
level too high. **And its second half** (section 7, read for it in the fact base, `FINDINGS-savedbattle.md`): the
battles and bombings being fought, each side's units taken on as in play and its figures, lists and tallies as written,
the finished battles and the sunk ships - every one of five saves' read exactly, and a day run on from each. With it
three things in play follow the original: a bombing goes at the front of the list of battles, a side counts what joins
it (`size`), and every ship that leaves the game goes on a list of sunk ships. **The writer's first increment is built
too** (section 8, `OpenHOI3.SaveCheck write`): the original's layout, the top level's head, every province, each
country's figures and the factions - a save loaded and written back gives 9,372 of 14,167 province blocks byte for byte
and every other differing only by what the game does not hold; its first run found the loader not reading
`highest_threat`, mended. **And its second** (section 9): each country's military part - the queue, the leaders in
service, every unit tree with its brigades, orders, plans, carriers' groups and passengers, what waits to be placed and
the templates - and the top level's two counters, every number given back as the save's pair; in nine saves of 1936 to
1941 every country's military part is the save's line for line once a brigade's `home` and `builder`, `country_intel`
(read and written since 2026-11-14, `savegame.md` section 20) and a lent unit's keys are taken out, but what a load changes as the original's does. The order keys the game does not
use were read in the fact base for it (`FINDINGS-savegame.md`, C.11) and are written as the original's: an order's halt
figure, kept on the order; a naval order's counts and an air order's figure per air type, nought for good. It mended the
loader on the way: an invasion's landing sea and a deployment's own number are read now, the player's numbers run on
from the history's with no gap, a waiting intercept no longer goes after the fleet its order names, and a naval order's
being told to return is lost on a load as in the original; and a line passes over two numbers after its unit, the first
for its deployment. **And its third** (section 10): the pacts, the wars, each country's pool and convoys, the battles,
the finished battles and the sunk ships - every war, convoy, battle and list of finished battles in fourteen saves the
save's byte for byte, and the pacts too but for a NAP's three figures, which nothing holds. The pacts come out in the
order they were made, whatever their kind, as the original keeps them on one list, so each pact now holds its number
and a new game numbers the history's by date and file; a war made in play is written under its name written out, and a
loaded one's name is read back into its parts, so a war declared after a load is no longer given a name a loaded war
has. **And saving in play** (2026-11-05, section 11; `ingamemenu.md`, section 6): the menu's Save Game opens the save
dialog - the folder listed newest first, the country's name and the hour offered, a row's name chosen, the question
before a file is written over - and the autosave writes `autosave.hoi3` at the settings' frequency, shifting the
original's two older files along, the player's folder being the original's own. The dialog's name is typed into as
the original's edit box takes typing, read for it in the fact base (`FINDINGS-editbox.md`; `interface.md`, section 8):
the keyboard given as the dialog opens or by a click, the six keys, the seven characters refused, the cap of 250, the
caret's blink and the one wrapped line shown. **And the fourth increment's first piece** (2026-11-06, section 12):
each country's politics and research queue, both ways - the queue, the election and surrender dates, the spies caught,
the parties' organisation and popularity, the government, the pool of ministers and the cabinet - every one the save's
byte for byte in eleven saves of 1936 to 1941. A loaded game had had no parties, seats or pool until then, a load not
running a new game's setting-up. The cabinet is allotted again on a load and the save's list kept behind it, as the
original appends it, so a reloaded game writes 22 figures as the original's does. **And its second** (section 13): each
country's spies - the free spies, and in every country the priority, level, date of the last change of missions, the
nine missions and the covert-ops points - the save's byte for byte in the same eleven saves. **And its third** (section
14): the top level's rebel and theatre counters, the game's flags, the victory conditions, the fired events, the
weather's fronts, each faction's progress and the player's Automate Trade - the save's byte for byte in twelve saves. A
loaded game had had no victory conditions chosen, the history's theatre counter and no fronts. **And its fourth**
(section 15): whether a country is in exile, its ten pools of goods, its battles fought, its strategic warfare, where it
stands on the triangle and its lend-lease - the save's byte for byte in thirteen saves; a loaded government in exile had
been none. **And its fifth** (section 16): the AI's strategy - its personality, its eight lists and its theatres with
their fronts - the save's byte for byte in twelve saves but for the figures nothing holds, and **kept on a load as the
original keeps it**, until the first midnight ten days past the start date, where a load had made every strategy and
theatre again, the theatres under new numbers; read for it in the fact base, the ten-day clock starting at the defines'
start date. What is left of section 4's list - the statistics, the gameplay settings, the strategy's production
shares and military access, and a province's `history` - each wants building or reading first; war exhaustion is
built since 2026-11-07.

**And the front end** (2026-11-06, `docs/mechanics/frontend.md`, read in one wave of two,
`reversing/findings/FINDINGS-frontend.md`): the program comes up on the original's main menu, its Single Player opening
the lobby of `lobby.gui` pared down to the one start, by decision - the start's row with its bookmark's
picture and shortcut shields, the world on the map behind it, a country chosen by a click on the map, the saved games
newest first with a row loading its save the moment it is chosen and its player chosen, the chosen country's panel, and
Play making the new game or going on with the save; Options opens the front end's settings window, Exit ends the
program, and Resign comes back to the main menu. The scenarios and the custom games are not offered
(`docs/compatibility.md`, entry 17); **every bookmark and the Quick Start panel are, since 2026-11-15** (below). Tried with `--click`, which the front end plays where no switch
starts a game; it was reviewed in play on 2026-11-06 and the main menu found right, and the lobby's shields
were turned and placed by a screenshot of the original's. **The lobby's options are built too** (2026-11-06,
`frontend.md`, section 4, a second wave of two readers): the panel of `options.gui` with the difficulty, the game mode -
greyed, Arcade not being built (entry 18) - and the five areas a player may hand to the AI, each press said in the chat;
the difficulty's static modifier and its combat modifier on land, at sea, in the air and in bombing; each area handed over
giving the played country its minister, the production one with the strategy's walk, and the finished units placed every
hour where deployment is; all of it saved and loaded (`savegame.md`, section 17). Tried with `--click`; the panel has
not been reviewed in play. **And in play** (2026-11-06, `frontend.md`, section 4.4): the country screens' automation
arrows handing each area to the AI and back, what each screen takes from the player as the original's - greyed, where the original's only stops taking the mouse, by decision (`docs/compatibility.md`, entry 19); the technology screen by its four
settings, read for this - the production screen's slider automation setting the player's sliders each day at its level,
and its automatic-deployment box; with greying in the interface runtime (`interface.md`, section 7). Tried with
`--click`. A launch with no `--country=`, `--load=` or `--replay=` - an empty country prompt among them - comes up on the
main menu since the same day. **The far map mode** asked for - the countries' names on the map, the zoomed-out shader -
is the lobby's last piece named; its reading is in the fact base (`FINDINGS-farmap.md`: A, what switches the far view -
four thresholds on the camera's height - and what changes with it; B, the country names, laid along a least-squares line
through the land a country controls by its capital, bent along a spline, in its faction's bitmap font, with no shader).
**And the file checksum** (2026-11-13, `docs/mechanics/checksum.md`, read in `reversing/findings/FINDINGS-checksum.md`):
the original's figure over the same files at `CREATING_CHKSUM`, and its four letters on the main menu's `version_label`
and the lobby's `checksum` - `OpenHOI3 (GSKH)` for Their Finest Hour, the version name summed being the original's by
decision, so the letters are the original's. `OpenHOI3.SaveCheck checksum` gives the figure of the original's own
log for the base game and for BlackICE 15.1. Tried with `--click`.

**And the original's shaders, translated** (2026-11-06, `docs/mechanics/shaders.md`): by decision,
moddable shaders are supported, BlackICE changing the terrain, the sea and the air map's circles in its own
`gfx/FX` - every effect file is read through the layers, preprocessed, parsed and each pass of each technique written as a
Godot shader (`Content/Shaders`): all 88 passes of the install with BlackICE translate, the one problem `shadowmap.fx`'s
own, and all 88 compile in Godot (`OpenHOI3.SaveCheck shaders`, `--shadercheck=`), for the 3D renderer and for the 2D
canvas; **the buttons, icons and scrollbars draw through the install's `buttonstate.fx`**, translated for the canvas, in
place of the hand-written shader of before. **And the map's land, beaches and sea, drawn as the original draws them**
(2026-11-06, `docs/mechanics/mapdraw.md`): the install's quad cache read (`CQuadCache`, `OpenHOI3.SaveCheck quads`, the
fact base's figures every one), the original's camera made to agree with the 2D view, and each tile through
`terrain_2_0.fx` and the sea through `water.fx` as translated, fed what the executable feeds them
(`FINDINGS-mapdraw.md`), their technique picked by the map mode's style and the 500 threshold - tried with
`--screenshot`, and it has not been reviewed in play. **And the land borders** through the cache's border meshes and
`TerrainShader_Border`, province lines grey and country lines red, none in the far view, their junctions from the
map cache's corner pixels made again for the controllers as the original does (the fact base's B.7) - **and the far
view's country names** (`docs/mechanics/mapnames.md`): each country's name laid over the land joined to its capital as
the original lays it out, bent along it, in its faction's font - **and the sea zones' borders** through
`borders_2_0.fx` and **the selected province's highlight** in the lookups (the fact base's B.8, B.9), after which nothing
of this project's own is drawn over the surface, `PoliticalMap.gdshader` left as the flat map for an install with no quad
cache. The far view was set beside the original's and it agrees. **And the rivers and the surf** through
`river.fx` and `beach.fx` (the fact base's C.6, C.7): the rivers traced out of `rivers.bmp` and made into strips as the
original makes them, the surf's quads from the cache's wave pixels - near the foam, far the coast's black line - their
counts the fact base's every one, tried with `--screenshot`, and they have not been reviewed in play. **And the
trees** through `TreeShader` (the fact base's C.8): planted as the original plants them where settings.txt's `renderTrees`
is on - `--trees` plants them regardless - a pixel of a tree palette entry keeping a tree by trees.txt's density, each four
flat cards of its sheet, snowed in the weather mode below −5, at most 16 tiles at once under height 100. **And the
province names** under height 100 (the fact base's farmap B.7): every province's name on its text position, turned and
scaled as positions.txt says, wrapped at 320 only at spaces, fading in over 1.5 seconds as its tile comes into view, in
kart_font's typewriter face, for which Liberation Mono now stands - with which the map's surface is whole but for terra
incognita, a scenario's. **And the hierarchy's lines and the air map's rings** through `hierarchy.fx` and `circle.fx`
(the fact base's hierarchy 7 and airmap 8), on the surface, the 2D layers kept for a map drawn flat. **And the counters
through `counter.fx`** on the canvas (counters 10), with the corner's three marks - red short of supplies or fuel, yellow
an attack delay, orange an upgrade - on the player's own (`mapunits.md`, section 2). **And the
shields through `maskedflag.fx`** (`FINDINGS-maskedflag.md`; `interface.md`, section 5), the flag by its look's
technique and the overlay over it, the canvas route taking the mask's coordinates from `CUSTOM0`; none of the four effects the
interface imitated in C# is left imitated. **And the weather's pictures, the progress bars and every sprite through the
effect it names** (the fact base's weathersim C.3.1, `FINDINGS-progressfx.md`, `FINDINGS-buttonstatefx.md`) - the bars'
upside-down pictures now the original's quad and not a measured flip. Left open by those readings: whether a multi-frame
button whose file sets no frame takes its frame from its state (`interface.md`, section 5). **And the path arrows through
`arrow.fx` and every plain billboard through `billboard.fx`** (the fact base's movement 11, `FINDINGS-billboardfx.md`) -
the arrow's band stopping a twentieth short of its end as the original's does, the pointer no longer scaled by its
counter - with which nothing drawn here imitates an effect of the original's (`shaders.md`, section 6). Open from the
billboard reading: whether the pictures the original moves after hanging them - the large air pictures and the battle
icons - ever stand where they are moved to; a screenshot of the air map mode with an air battle and a bombing in one
province would settle it. **And the minimap and the map mode buttons** (the fact base's `FINDINGS-minimap.md`;
`docs/mechanics/minimap.md`): the original's fixed picture with its day and night slid by the hour and the camera's
view a white frame whose size is a straight line in the height, a left press jumping the camera there, the sixteen
buttons with their pressed frame and `MAPMODE_N` tooltips - `Tab` gone, the original having no key - a game starting
in the terrain mode, and **the province's tooltip standing over the minimap's corner**, not the mouse, which corrected
the fact base. **And the ten map modes that were left** (2026-11-07, `docs/mechanics/mapmodes.md`; the fact base's
`FINDINGS-mapmodesfigures.md`, `FINDINGS-mapmodescountries.md` and `FINDINGS-mapmodesrest.md`), read in three batches
and tried with `--screenshot`, not reviewed in play: infrastructure, victory points, strength and resources
with its icons on the buildings' points; diplomatic, intel and theatre; simplified terrain, region with its colours out
of `interface/colors.txt`, and naval with its patrols, raids and escorts, its four icons of the week's battles and its
large naval bases - each with its part of the province's tooltip, each coloured again when the original colours it, and
the lobby's map political with the owner's colour as the ground, as the original's front end draws it. Reading for them
corrected the fact base's map mode records. **And war exhaustion** (2026-11-07, `docs/mechanics/warexhaustion.md`): read
whole in the fact base already, built in a session - a new game's figure by the calendar months at war or since peace,
the month's 0.041 at war under its maximum, all of it paid back to neutrality in the first month of peace, the
`war_exhaustion` modifier step, condition and effect, and the save's key. **No save can check it**: every reference
save was made with BiceLib's patch zeroing it each month written into the executable, so the start save's Italy is
0.000 where ours is the original's 0.191; neutrality still agrees 101 of 101. **And the map cache built here** (2026-11-08,
`docs/mechanics/mapcache.md`, `Content/Map/Cache`): read whole in a wave of five readers (`reversing/findings/FINDINGS-mapcache.md`),
every file of `map/cache` made from the map files in the original's formats, **each the original's byte for byte on two maps** -
Their Finest Hour's shipped cache and the one the original built for BlackICE - in about 2.3 seconds. **The game now reads the cache from the player's Documents folder alone** (`docs/compatibility.md`, entry 22), never the install's, and builds it there where it is not the map's instead of drawing flat. **And the end of a game** (2026-11-08, `docs/mechanics/endgame.md`, read in `reversing/findings/FINDINGS-endgame.md`): the dialog that names a winner, the game held at its end date, a player with no land sent
to the end, the end-of-game screen with its result worked out afresh, the victory conditions' window behind the goals button - which the reading found to be the same window - and their alert, and the lobby's
choice of conditions. Reading for it settled how the font places a centre-measured text box. **Next up**: what remains of the interface below, the console, or the military AI's gaps (*What the moving wave left*).

**The console is built** (2026-11-09, `docs/mechanics/console.md`) for the commands the table rates easy and medium, each answering as the original's; the table's other commands answer `Not built.`. The plan as it was, 2026-11-08: the original's 53 commands are read whole in the fact base
(`reversing/findings/FINDINGS-console.md`, section C), and the debug menu (Shift and 4) already has `debug fow` - for the
countries played alone (`docs/compatibility.md`, entry 21) - and `cpaint`. The plan is a console like the original's,
opened by `§` or `` ` ``, taking its command names, aliases and arguments and giving its answers, each command posted
as a command so that a cheat is replayable, where only the original's `tag` goes through its queue; the debug menu keeps
the switches that take no argument. Rated against what is built:

| | commands |
| --- | --- |
| **easy** - the system exists; the command adds, sets or calls | `money`, `supplies`, `fuel`, `crudeoil`, `metal`, `energy`, `rarematerials`, `givemeall`, `addmanpower`, `addofficers`, `noneutrality`, `setnationalunity`, `election`, `reorg`, `surrender`, `guarantee`, `changeowner`, `changecontroller`, `conquerall`, `event` (the `--event=` switch already fires one), `revolt`, `alwayspartisants`, `instantresearch`, `instantconstruction`, `tooltipdebug`, `printtech`, `copyprov`; `debug tooltips`, `debug nopopup`, `debug minzoom`, `debug color`, `debug wireframe` |
| **medium** - more wiring, or a small reading first | `tag` (the played country changed touches much of the interface), `aipause` (one country's AI steps left out), `teleport`, `date`, `debug render <layer>` (the map's layers are cut differently here), `debug yesmen` and `debug spy` (what each flag changes to be read), `detailedlog` (the save writer would serve), `reloadinterface`, `reloadfx`, `fullscreen` |
| **not worth it now** | `nukes` (no nuclear system: to be built with it, *Loose ends*), `morehumans` (multiplayer), `paint`, `rpaint`, `ipaint` (the map editor), `tutorial`, `spawnactor`, `reload`, `reloadtexture`, `window`, `randomlog`, `debug info`, `debug profile`, `debug log_profile`, `debug textures`, `debug assert`, `debug save`; and `weather` and `crash`, which the original refuses outside its developers' build |

**What of the interface is missing**, inventoried 2026-11-08: the install's 58 `.gui` files set against the names the code looks up, each gap
then set against the executable and the fact base. It is name matching: a button can count as found because its name is a common word (`close`,
`build`), so a dead button inside a built screen may be hidden, and only some names put together in code were ruled out by hand. BlackICE's 20 extra
files are not in it. **Every window and part of a screen it found is built as of 2026-11-09**, the full-screen picture behind the main windows the last of them (`countryviews.md`, section 6).

Not counted as missing: multiplayer (the metaserver, chat, the in-game lobby, hosting and joining); scenarios and custom games, by the settled decision above;
the tutorial, the GamersGate store (`micro_interface`) and the `nudge` editor; and what is dead in the original - the side menus, the right-click menu
(`FINDINGS-leftovermenus.md`), `SettingsMustRestartWindow` (`FINDINGS-settingswindow.md`), and `AllyCallDialog`, `ShieldedInformationDialog`,
`capture_ship_entry` and `sup_odds`, which the executable never names, and the outliner's `outliner_configuration`, which it makes and never shows (`FINDINGS-outliner.md`, A.8). The victory conditions, their alert, the goals button and the end of the game were built from this list the same day (`docs/mechanics/endgame.md`). Suggested next: the theatres and
statistics screens, the most visible dead buttons.

The other open choices are the third piece's gaps - the fleets that do not merge, which want a reading in the fact base
first (`CANDIDATES.md`, *What the moving wave left*); or the next piece of the AI.

The interface runtime is in, with three screens - the top bar, production and technology -
the four windows the production screen orders a unit from, the deploy menu with the
alert that opens it, and the panel of a selected unit. Windows, icons, buttons,
checkboxes, text boxes, scrollbars, list boxes and boxes of overlapping elements are built
from the original's `.gui` files on a canvas that scales, found by the names the
original's code used, and filled with the figures in the original's own formats and
colours (`docs/mechanics/interface.md`, `topbar.md`, `countryviews.md`, `sliders.md`,
`productionscreen.md`, `technologyscreen.md`, `builders.md`). **A game can now be driven by hand as far
as the simulation goes**: the six IC sliders and the four leadership sliders set a
country's shares, and the technology tree and the research queue set what it
researches. What the screens write into their elements was read out of the original
for them: `reversing/findings/FINDINGS-topbar.md` and
`reversing/findings/FINDINGS-countryviews.md`.

The routine that rebuilds a country's modifier has been read from end to end
(`docs/mechanics/modifiers.md`, sections 4 to 6). What it showed that could be built without
the script's conditions is built: strategic resources as the original shares and scales
them, the provinces' modifiers, `war`, `peace` and `dissent`, and what a minister does to
the decay of what a country knows. What is left of every mismatch with the savegames is
now a named cause, and each is stage 4 or the AI: see *Loose ends*.

Done so far inside this stage: the stockpile (`docs/mechanics/stockpile.md`); the six
shares of IC with the two that spend without units, consumer goods and supplies
(`docs/mechanics/production.md`); the two conversions, energy into crude oil and crude
oil into fuel (`docs/mechanics/ic.md`, section 4); leadership with its four shares, of
which officers, diplomatic influence and spies are made each day
(`docs/mechanics/leadership.md`); research - the queue, what a level costs, what a day
adds, what finishing one does, and the monthly decay of what a country knows
(`docs/mechanics/research.md`); manpower - what a day adds and what a new game starts
a country with (`docs/mechanics/manpower.md`); the interface runtime with the top
bar; and the production and technology screens. And the first two pieces of stage 3: the
unit types, the historical models and the orders of battle, with what a brigade holds on
the first hour and the officers a country's units bring it (`docs/mechanics/units.md`);
and what a unit draws each day - what it uses in supplies and fuel and the stock of both
a new game starts with, troop rotation, and the reinforcement and upgrade shares of IC
with the needs their sliders show (`docs/mechanics/upkeep.md`). And the third: the
officer ratio, the most organisation a brigade may have, the hourly return towards it,
and what a crowded base does to a fleet or an air group
(`docs/mechanics/organisation.md`). And the fourth: the build queue - ordering a unit,
what it costs and takes, the production share's day, the unit that comes out and where
it may be put, and what a new game makes of the units its files start under construction
(`docs/mechanics/buildqueue.md`). And the fifth: the windows a unit is ordered from - the
division designer with its brigade picker, and the ship, air and brigade builders - with
what a country may build and how many, the combined arms a design shows, the country's
division templates, and what a new unit is called (`docs/mechanics/builders.md`). And the
sixth: the units on the map - where a province's units stand, what a counter is made of
and which units have one, the two figures it shows, and the alert and the menu a finished
unit is placed from, alone or with a unit in the field (`docs/mechanics/mapunits.md`).
And the seventh: movement, for land units - the edges between provinces with their kinds
and the distance of each, the order by a right click, the route as the original finds it,
the speed and each thing that scales it, and the hourly march
(`docs/mechanics/movement.md`). With it the rule that gives a province its terrain was
read and replaces the one given before (`docs/mechanics/map.md`, section 9). And the eighth:
the panel of a selected unit - the units above it, its header with its men and its two
bars, its status, what it is doing, its brigades and the units under it, with the three
switches for reinforcement, upgrades and priority, and the button that halts a march
(`docs/mechanics/unitpanel.md`). And the ninth: fleets and air units - where each may
go, ports, the range and the distances by sea, the window of orders, the move and the
change of base, and the buildings on the map with the port and the airfield a unit in
one is picked by (`docs/mechanics/fleetsandair.md`; `mapunits.md`, section 9). And the
tenth: a land battle - what starts one, the front and the reserves, the round and its
damage, the end, falling back, the province taken and the attack delay, with the game's
generator of chance (`docs/mechanics/landcombat.md`). And since: supply - the network, the
day's carrying, convoys and the map mode (`supply.md`, `convoys.md`, `supplymap.md`) - a
battle between fleets (`docs/mechanics/navalcombat.md`), and a fleet's missions with the
raid on convoys (`docs/mechanics/navalmissions.md`).

There are five savegames of one game to check against now, from 1 January to 20 April
1936. The first is the start; the later four are a month or less apart, which is what
checks anything that moves from day to day.

**When a spec needs something read out of the executable**, the reading is done in the
fact base and entered in its record, not only written up: this repository's `CLAUDE.md`,
*Where knowledge about the original comes from*.

**All sixteen alerts are built** (2026-11-09, `docs/mechanics/alerts.md`), the row under the top bar tested every three seconds of real time with each tooltip and click as the original's, and with them a round of the interface: the bottom right's panel, the custom game's picture under the decisions hidden, a ship technology's levels by name with what each does, the lobby's map at its one zoom with the whole chosen country lit, the wheel and the arrow keys leaving the map alone under a main window, and the settings window of a message's type on a right click (`messages.md`, section 4.2).

**The nuclear bomb is built** (2026-11-09, `docs/mechanics/nukes.md`): the month's bombs, the order with its row and its hours, the drop with its messages and `on_nuke_dropped`, and `province_event`, with the console's `nukes` and a trial, `OpenHOI3.SaveCheck nuke`. The AI makes bombs and does not drop them, the enemy's war industry not being handed to its agents (*Loose ends*).

**The script gaps of the base game are closed** (2026-11-09, `reversing/findings/FINDINGS-scriptgaps.md`): `release_vassal`, `create_alliance`, `surrender_inherit`, `load_oob`, the two port conditions, and the undeclared war (`war.md`, section 15) with its region's map colour; tribute (`stockpile.md`, section 7); and the AI's Lua answers on declaring war, puppets and liberation, with its two commands.

**A brigade is shown as the model its levels are** (2026-11-14, `docs/mechanics/units.md`, section 5): the unit panel's
numeral, the reorganise window's, the summary, the brigade card's label and picture and a sunk ship's row take the model
nearest the levels the brigade carries, as the original does everywhere, marked `+*` where its levels match none
exactly, and a faction member's model takes its leader's name before the game's. So a save loaded shows its brigades as
they were - II and III where all had shown as I - and a brigade's and a line's `builder` are held, read and written.
**And given as the original gives them** (2026-11-14, `units.md`, section 5): a brigade's names its owner from the
first whole level of equipment the upgrade share brings it - which is where nearly every `builder` in a save comes from -
and every line ordered in play, or to change a brigade's type, names its country where no licence names another.
`OpenHOI3.SaveCheck load` from `Ireland1936_02_06_02` to `_03_11_04` gives every brigade whose levels the hours left as
the save's the save's `builder`, 3,819 of 3,819 (3,885 of 4,075 in all, where 3,666 did before); a new game run to 20
April 1936 writes it on 1,540 brigades and 84 lines, where it wrote none.

## Loose ends

- **The launcher - built 2026-11-18, what is left** (`docs/launcher.md`). Built: the Avalonia window
  (`launcher/OpenHOI3.Launcher`), its logic under test (`lib/OpenHOI3.Content/Launch`), `--install=` in the engine, the
  `engine` key of a descriptor, `engine.txt` from `tools/build_engine.ps1`, and the three builds in `tools/`; run end to end on
  BlackICE GitHub. Left, each waiting on something: a save recording the engine it was made with and the check between
  players covering the engine, for the enhanced engine (section 8); a mod's release putting its engine in its
  folder, for the first mod that names one (section 9); fetching missing engines and updating itself, if wanted later; trying it
  off Windows.

- **Twenty defines nobody is sure the original reads** (2026-11-18). OpenHOI3 asks for none of these. The fact base calls
  eleven of them readerless: `SURPRISE_BONUS`, `UNDERGROUND_PARTISAN_STRENGTH`, `MILACC_CANCEL_RELATION_CHANGE`,
  `NAP_INFLUENCE_COST`, `RELATION_INFLUENCE_COST`, `WAR_THREAT`, `SPY_MISSION_CHANGE_DAYS`,
  `SPY_CONNECTION_DETECTION_CHANCE`, `SPY_BASE_CONNECTION_DETECTION_CHANCE`, `REBEL_ACCEPTANCE_MONTHS` and
  `ROUGH_COUNTRY_RUIN_EST_MODIFIER`. Nobody has looked for readers of the other nine: `CORE_GAIN`, `CORE_LOSE`,
  `IC_TO_CONSUMER_GOODS`, `MAX_PROVINCE_SELL_PRICE`, `COMBAT_EVENT_DURATION`, `MINIMUM_STRENGTH`,
  `NAVAL_STACKING_PENALTY`, `JOINBLOCK_INFLUENCE_COST` and `PURCHASE_INFLUENCE_COST`. A static search cannot settle
  them, because the defines are reached in ways it misses (`reversing/findings/FINDINGS-definesuse.md`, section 8), so
  the running game will, through the Cheat Engine bridge, once it is free (one session uses it at a time). The probe is the fact base's `probes/hoidefwatch.lua`, queued in its
  `CANDIDATES.md`, *Live probes queued*. Any field found read becomes a mechanic to build here.

- **Pictures code gives an icon** (`docs/mechanics/interface.md`, section 5): the original draws every one through its
  picture sprite, in the icon's sprite's box with the sprite's texture coordinates; here only the country screens', the
  ledger's and the top bar's skins do since 2026-11-16 (`CGuiIcon.ShowPicture`), which mended BlackICE's diplomacy and
  politics screens. The portraits (`GFX_empty_position`), the terrain, tactic, ship-model and technology pictures still
  go through `CGuiIcon.SetTexture` at the picture's own size, which is the same wherever a picture is its sprite's size
  and was not checked where it is not. And a button on a text sprite with no font draws nothing (section 2), by its
  type's font alone: what the original does where code later gives such a button another kind of sprite or a font was
  not read.

- **The mod layer's** (`docs/mechanics/mods.md`): an `archive` is not read; `userdir.txt`, `-settings=`, `-logpostfix=`,
  `-exclude_dlc=` and `-dlcmod` are not taken; the player's folder is not a layer of game files (`docs/compatibility.md`,
  entry 28), and `settings.txt` and the map cache are read from it alone, with no fall through the search path (entries 13
  and 22); the version label names the frontmost of several mods, which the original's choice is not read. BlackICE's
  `autoexec.lua` loads whole since 2026-11-16 - its `lfs` taken by the global `OpenHOI3` and the install made the working
  folder (`mods.md`, section 6) - but its old utility needs `wx` (wxLua) and its overlay BiceLib, neither of them here.
  The original's Lua API it called that ours lacked - the variables and `CSetVariableCommand`, the unit list, the shares,
  `CMinisterTypeDataBase`, the `CModifier` constants and the rest of `modsurvey`'s list - is built since 2026-11-16 (`lua.md`,
  sections 4 to 5.1); a month of its AI leaves 39 Lua errors, each `IsUnitAvailable` handed nil for a unit type its scripts
  name and its files do not declare, which the original would not survive (`lua.md`, *Ours*).

- **Script still unbuilt**: `add_division` (unused by TFH and BlackICE, its starting strength not read through), `remove_fow`, the four mission conditions (an Europa Universalis III leftover, no mission file in either install), `has_hostile_spy_mission` (the original compares against an unset byte) and the AI's `add_ai_strategy` / `fixed_ai_strategy`. Built on 2026-11-16, for BlackICE's layer above all: `remove_brigade` with the original's end-of-hour removal queue, `military_construction` in a running game's `load_oob`, `total_of_ours_sunk`, `total_sunk_by_us`, `surrender_inherit`'s surrender mode and `release_vassal = REB` (`effects.md`, section 8), and `lost_IC`, the four war-record counters and the six `last_*_losses` (`triggers.md`, sections 5.1 and 5.2). **The undeclared war's** hostility is asked at battles, convoy raids and air and fleet targets, not at the AI's war-time gates or the land's movement (`war.md`, section 15).
- **A unit taken out of the game, everywhere but the new paths**: the original's `RemoveUnit` leaves the removed unit's
  children at the top of the order of battle (a theatre's marked orphans) and, with its first flag clear, charges the
  country - the lender for a lent unit - the officers of the strength removed
  (`reversing/findings/FINDINGS-effectrun.md`, section 3.11). `CCombatRetreat.RemoveFromGame` does that only where it is
  asked to (`childrenToTop`), which the end-of-hour removal of `remove_brigade` and an annexation do since 2026-11-16;
  every other caller - a beaten unit's removal, merges, the theatres and the AI's agents - still hands the children to
  the removed unit's superior and charges no officers. Which of those callers the original passes the first flag set for
  is not read; the theatres' and agents' callers wait for the AI's work to settle.

- **What a load still does not give back**, checked path by path on 2026-11-14 (`OpenHOI3.SaveCheck reload`;
  `savegame.md`, section 20): every path is under an entry of `docs/enhanced.md`'s *A whole game's replay* or is noise
  the check now leaves out. ~~Whether a battle at sea, in the air or a bombing has its modifiers made again on entering~~:
  every battle's are, of every kind, as the original's entering does, read the same day - a battle at sea or a bombing
  of ships saved before its first round drawing for its surprise, no other (`savegame.md`, section 20). The order of a
  country's ministers among its modifier's sources, which the tooltips list, is the posts' since the same day, as the
  original walks its cabinet's fixed slots (`modifiers.md`, section 4). `country_intel` is read and written since, its
  first number the country's place plus one - the original's id of it, written and read unchanged, read the same day.

- **The AI drops no nuclear bomb**: its strategic targets, the enemy's war industry handed to the agents whose air bases are nearest, are not built (`moving.md`; `nukes.md`, *Ours*).

- **The multiplayer log's `ding` is the one sound effect not built**, there being no multiplayer. Every other effect of `interface/sound.sfx` the original plays plays here since 2026-11-12 (`docs/mechanics/sound.md`, section 7): the clicks, a province, a map mode, the zoom and the speed, a unit selected and ordered, an alert, an event window, a battle's window, and what the simulation sounds - convoys lost and sunk, a level finished, war declared by and on the player, an offer coming in and the answer to twelve kinds, a revolt. **The first of them have been heard in an export, and not those the simulation sounds.** Whether the title theme loops at the main menu is to be heard in the original (`music.md`, section 3).

- **An agent's reassign flag is set only as it is made**: the original also sets it when a unit is created for it and when a unit lands or reaches the area it was bound for (`CAIUnit::OnUnitCreated` and slot 71; `reversing/findings/FINDINGS-liveread.md`, section 43). And on entering July 1941 ours sends Italy's 2nd Navy to the Metropolitana's embark, which the original does not; why was not read.

None of these blocks the list above.

- **Only a theatre's headquarters can be handed to the AI** by the unit panel's box (`unitpanel.md`, section 9); the original lets a corps, an army or an army group go too, and the agents here are a theatre's. The AI's own liberating and puppets are still reported unbuilt (`politics.md`, section 11.1).
- **An expeditionary force's first piece is built** (`docs/mechanics/expeditions.md`): the window, the rows, the offer and answer, the hand-over and the return, the flag and what a lent unit may not do. A lent unit's officers, supply, reinforcement and combat figures are its lender's. The AI lends and sends home through its agents (`agents.md`, section 10.5), with nothing asked of the other country, the AI's command carrying a unit's whole tree; no AI offers one to the player. An annexation and, since 2026-11-14, a government going into exile send home what was lent to the country, both sides told, and shatter it at its lender's (`expeditions.md`, section 5).
- **The deploy menu's drag into the order of battle browser** (built 2026-11-13, `mapunits.md`, section 6): where the original's join finds no province for a dropped unit, what its following attach does was not traced; here the unit stays waiting.
- **The map's surface** (2026-11-06, `docs/mechanics/mapdraw.md`, section 6): it takes about 0.9 s as a game is made,
  and the front end makes its meshes again for each world it shows - the quad cache it reads once - which could be kept
  between worlds; a map whose cache is missing or stale - its `time` stamp not the map files' (section 2.1, since
  2026-11-06) - has one built by `docs/mechanics/mapcache.md` since 2026-11-08, and is drawn flat by `PoliticalMap.gdshader` only
  where it has no terrain to build from. **The cache is the player's Documents folder's alone**, the install's never read by the game; a mod's own folder (`user_dir`) is not
  asked until mods are layered. **A rebuilt beach is the original's only given the noise's starting count**, which varies from one of the
  original's runs to the next (`mapcache.md`, section 8). **`adjacencies.bin`, `navaldist.bin`, `climate.bin`,
  `provincecache.bin` and `quad_format.bin` are built for compatibility with the original and read by nothing here**, the game working
  those figures out as it starts (section 12). **`map.bin` is tested by date for
  the rivers' sources alone** (`CMapBin`, `docs/compatibility.md` entry 20): the land borders' junctions read its raster
  whenever the `time` stamp is current, where the original would build the file again first. Of the raster's two high bits
  the land mesh reads 0x20, and nothing found reads 0x10 (`mapcache.md`, section 4).
- **`CRandom` seeds as the original since 2026-11-07**, the shift in its seeding keeping the sign (the record's
  `MT19937Seed`), where it had used the textbook unsigned one; its twist, leaving a word that is odd and negative alone,
  was right and is checked against the bytes the same day. A loaded game is untouched, its generator's state the save's;
  a new game rolls differently from then, so **every recording made before it no longer replays**. The savegame tool's
  trials were run again the same day under both seedings, side by side: those that roll moved a little (the raid 44
  transports and 7 escorts where 38 and 3, the trade, the foreign minister's licences, the AI's spies and laws, the
  agents 779 of 908), and `CLAUDE.md` gives the new figures beside the old; the landing, the airborne trials, diplomacy,
  the AI's strategy and the replay check came out the same. Two figures quoted there were already stale before it - the
  trade trial's 285 offers of 2026-10-13 (145 under the old seeding) and the air battle's ten over Warszawa (four battles
  in all under either) - and the trade trial had stopped running at all, a command executed without `MayExecute`. Nothing marks the hovered
  province, as in the original; revolt risk's map style is ours.
- **A series names each division afresh since 2026-11-02**, as the Ireland game's saves show the original does
  (`docs/mechanics/buildqueue.md`, section 6) - measured and not read, so whether the original takes the new name by the
  same routine as a new order, and whether a series of ships or wings is named afresh too, are open.
- **The foreign minister leaves out** (2026-10-16, `docs/mechanics/foreignminister.md`, sections 10 and 12) where in
  the AI's pass a minister's hour falls. Its readiness for war is whole since 2026-10-17, and waits on units standing on
  the front, which nothing moves there until the unit agents.
- **The theatres leave out** (2026-10-17, `docs/mechanics/theatres.md`, section 12): the fronts by the sea; what a played
  country at war does in a merge; and they are looked after as one step of the hour after every country's units, where
  the original does each country's right after its own.
- **The unit agents leave out** (2026-10-17, `docs/mechanics/agents.md`, section 13): the war stance's combat values. The
  air units' move between theatres by group difference is built since 2026-11-13 (`agents.md`, section 10.3). The division need is read whole since 2026-10-19, and
  the balancing agrees with the 4 January save in all 106 theatres since 2026-11-09, the AI's lending of units with it (`agents.md`, sections 10.5 and 14).
- **Moving and attacking leaves out** (2026-10-19, `docs/mechanics/moving.md`, sections 19 and 20): paradrops and
  strategic bombing, nothing yet handing the agents the enemy's war industry; the Allies' Baltic ban; a friend's area
  reinforced by sea. ~~**Fleets come out too many**~~ - 221 to the 4 January save's 222 since 2026-10-20, a join no
  longer undone in the pass that ordered it, and a war with an empty side no longer keeping its last country at war;
  ~~**Japan's air groups stay apart**~~ and ~~**Italy's three intercepts are given an hour before the save's**~~ - read on
  2026-11-12 from a new game the original played hour by hour (`reversing/findings/FINDINGS-liveread.md`, section 56): at
  peace every air group is a candidate for the regrouping, worn or not, and an air unit attached under an agent sets its
  regroup air pending; set beside that game's own save of 4 January every country's count of air units is the original's
  and so are the intercepts' hours, so the reference save, a game of its own, differs from a new game there (`moving.md`,
  section 21). Italy's war
  on Ethiopia keeps pace with the original's - 7 provinces held on 2 February to its 8 in the Ireland game's save - after
  the corrections of that evening and of the reference Ireland game: stale areas, the pull's fuller test, the supply
  gate of a route and the AI running twice in the first hour (`moving.md`, section 21).
- **The AI's strategy leaves out** (2026-10-16, `docs/mechanics/aistrategy.md`, section 12): military access, the
  production shares and the brigades to aim for - the military AI's; `add_ai_strategy`, which the base game and BlackICE
  never fire; `has_building` and a unit type in the walk; historical friends, which no file gives. **The ten days are
  asked once a day**, where the original also asks them at every diplomatic change, and **a change of government waits
  for them** where the original sometimes makes the strategy at once.

- **The tech and production ministers leave out** (2026-10-16, `docs/mechanics/ministers.md`, *Ours* and *Not
  established*): the AI's unit plans, so a finished brigade's choice of division weighs no want of its type; the
  original's start-date technology, so the research its AI does is set beside the saves' progress and not their levels;
  **the Soviet Union's supply share runs low** in a run to 1937 (168 supplies on 1 January 1937) - not looked into; the
  order a session runs posted commands in, which the reverse order of lines moved to the front rests on; and the units
  loader's numbering of types, built as the files' order. The 1937 run's Spanish Civil War fired on 30 January, the
  draws having moved with the ministers'; it still ends on its first day.

- **The headless run leaves out** (2026-10-16, `docs/mechanics/headless.md`, sections 4 and 5): ~~`country_event`,
  `split_troops` and `infantry_brigade` as effects, a province's `GetNumberOfUnits` for the Lua, the condition
  `lost_national`~~ (built the same day, `effects.md` section 9); **the Spanish Civil War, which ends on its first day here and waits
  for the AI** to move the Republic's units off the Nationalists' victory points, as the original's does; and **a measure past April 1936** - no reference save lies later, so
  "plausible against a real savegame" rests on the first four months.

- **Selection leaves out** (2026-10-16, `docs/mechanics/selection.md`, *Ours*, *Not built* and *Not established*):
  Ctrl and a click on the single panel's battle row, which here opens the battle and selects nothing more; the selection
  groups are not saved; the rows do not move right for an open side menu; and **whether the
  original ever sees Alt**, which by the bytes cancels a box and by its keyboard is never set - here Alt does nothing.

- **The province view leaves out** (2026-10-15, `docs/mechanics/provinceview.md`, *Ours* and *Not established*):
  which provinces the original does not simulate and opens no view on - none is known but the sea's;
  ~~the weather, which is nought until it is built~~ (built: `weather.md`); and what an asked ally is shown of an allied objective. **The
  scaffolding readout under the top bar is put away behind the debug menu's switch, Shift and 4** (`src/Debug`), by
  decision: it alone shows the supply and the country's figures.
- **Weather leaves out** (2026-10-15, `docs/mechanics/weather.md`, *Ours*, *Not built* and *Not established*):
  nothing of the weather's own now - **attrition**, which reads the temperature and wind, is built since 2026-11-14
  (`upkeep.md`, section 3a). Screenshots of the original's of
  2026-10-15 settled the pictures' size and place, that they draw at every zoom, and the tooltip without the province's
  name; and **only the `_simple` pictures are drawn**, `alwaysCounters` off being unsupported by the decision of that day
  (`docs/compatibility.md`, entry 12). **The player's settings are read from the original's `settings.txt` in the
  Documents folder** (`docs/mechanics/settings.md`; `docs/compatibility.md`, entry 13) - which of them apply, and when,
  `settings.md` lists; `counter_scale` is applied since 2026-11-07. And **about one land province in two hundred has another
  terrain in the original** than the map's rule gives it here: `OpenHOI3.SaveCheck weather` names each - 48, every one a
  province without a `text_position` and a near-tie in its square that the original gives to our runner-up (`map.md`,
  *What is not established*).
- **Convoy lines leave out** (2026-10-15, `docs/mechanics/convoylines.md`, *Ours*, *Not built* and *Not established*):
  BlackICE's history lines of transports and escorts, and two things a screenshot would
  settle - whether the transports dialog draws the file's `on_complete_icon` and frame, which nothing sets, and which
  category picture a convoy line shows (the buildings', last in the file, is inferred and drawn).
- **The simulation runs on the frame's thread, so full speed draws one hour a frame** (2026-10-13). Built
  with *Play OpenHOI3 (fast)* an ordinary hour takes about 15 ms and the game runs at about 43 frames a
  second, but an hour that turns a day takes about 90 ms and the map stops for it once a game day. The
  hour's trading already runs side by side as the original's does (`trade.md`, *Ours*); what is left is
  the day - production, home production, supply, the surrender, trade routes - and, if that is not enough,
  the simulation on a thread of its own, which every screen that reads the state would have to allow for.
  `OpenHOI3.SaveCheck profile` says where an hour's time goes. **Measured on 2026-11-04**: the runtime is not
  the lever - .NET 9 and 10, Server GC, the GC's budgets and the JIT's tiering all came out within the noise - and
  allocation was. The route finder made five arrays the size of the map on every search, half of everything allocated
  and every full collection; it keeps them now (`CRouteScratch`), and 30 days of the trial went from about 24.5 s to
  20.5 s, an ordinary hour from 28 ms to 24, the state's hash the same every midnight. Then the same day the high
  pressure's hourly walk and the convoy's route took the same arrays, and `CProvince.Edges` became an array, a
  `foreach` over the read-only list it was having made an enumerator each time: 19.9 s to 18.9, no full collection
  left in fifteen days, weather and the supply pass with its convoys the same line for line. What allocates most now
  is the Lua's fields and strings and a `float` boxed for each figure the Lua classes hand over. **Whether a C++
  rewrite would help** was studied on 2026-11-05 (`docs/performance.md`): little for its cost, since the Lua is C
  already; a port of the simulation alone is to be weighed on a profile of the finished game.
- **Where the AI runs in multiplayer is not settled** (2026-10-13). Here the AI runs inside the hourly tick,
  which every machine would run, and draws on the game's shared generator - which countries trade this
  hour, the AI's event option, the roll that answers an offer. The working understanding is that the
  original's host alone runs the AI's Lua; if so, the AI here has to come out of the tick into a step that
  runs on the host and only posts commands, with its draws kept off the shared generator or carried in the
  commands. The fact base cannot say yet, and what it has argues the other way - the original's AI draws on
  the game's generator, so a machine without it would fall out of step at the first draw: the question
  and the reading that would settle it are in `reversing/CANDIDATES.md`, *Does only the host run the AI in
  multiplayer?*. Nothing needs it before there is multiplayer. The hour's trading keeps eight states and a
  generator per run either way, which makes a game replayable from its seed on any machine
  (`trade.md`, *Ours*).

- **Lua's API is built as far as the ministers reach** (`lua.md`): some three hundred methods, where the original
  registers some six hundred. Nought or nil, reported once: a modifier's `GetValue` of any kind but the global IC's, and
  the tech minister's monthly re-plan (`GetTechModifers`, `GetFolderModifers`). No `CFixedPoint` is
  written by `tostring`, nor made by a script. The lists of neighbours are worked out at each call,
  a third of a millisecond. **The original's main thread ran its Lua at
  24-bit x87 precision** (Direct3D 9's doing) and this runs it in doubles: whole numbers agree, what
  the AI's fractions do is not established. **The interpreter is built for 64-bit Windows only**, with the
  Visual Studio C++ tools, as `openhoi3_lua.dll` - a name chosen because Lua for Windows puts a
  32-bit `lua51.dll` on the PATH, which Godot loaded in its place.
- **War and its end leave out** (`war.md`, *Not built*): the cabinet a new puppet's government would fill; an exile's
  free supply from friends, its brigades at its leader's technology and its partisans turned to militia when it comes
  home (its sending home of the units lent to it is built since 2026-11-14, `expeditions.md`, section 5); and a
  landless country's units, which the original
  destroys each hour. **A change of hands leaves out** the air and naval units based in the province, which the original
  sends to the old side's nearest base, and radar. **A
  pact is one object for both countries**, so annexation ends both sides of one (`war.md`, *Ours*). **Units on ground
  they may no longer stand on are not marked** (`war.md`, *Not built*), and the revolt war is not built.
- **Factions leave out** (`factions.md`, *Not built*): the victory conditions' random fill for a faction with fewer
  primaries than the count, reported.
- **The agreements leave out** (`agreements.md`, section 9): the player handing his diplomacy to the AI as the
  original does it, under which his own offers are answered like an AI's and the 96-hour drop applies to him - the
  lobby's automation gives his country a foreign minister (`frontend.md`, section 4), and offers to him are not checked
  against it. The dialog's influence and alignment drift
  says each day's `align_towards` and the days; a message's `VAL` and `TIME`, which no text of the
  base game uses, are not given. **Every `DiploScore_NonAgression` answer on a new game is below
  nought** (`OpenHOI3.SaveCheck aiscore`), so the AI declines every NAP; whether the original's
  answers are too was not checked.
- **Aid leaves out** (`aid.md`, *Ours*): the licence list's combat-value comparison (technology levels
  stand in), a licence row's stat block, a convoy's lend-lease efficiency lowered by sinkings, and the
  production screen losing its tooltips while the lend-lease window is open. **Every licence model shows
  "+*"** in a new game: the exact-match rule behind the mark is inferred from the record's word. The foreign
  minister proposes licences, and in a new game's first 180 days no debt or lend-lease (`foreignminister.md`,
  section 9); the lend-lease day was never set against a save.
- **Trade leaves out** (`trade.md`, section 16): what becomes of a route
  when either country ends, not read; and the sound a new offer
  plays. **Automate Trade is a command here**, where the original's checkbox writes its local player's
  own flag (`trade.md`, *Ours*). **The offer dialogs' background**, `SandboxFloaterBackground`, is
  drawn as its texture is, see-through grey; nobody has set it beside the original's.
- **Of the diplomacy screen, three things were not read** (`diplomacyscreen.md`, *Not established*): which rows'
  checkboxes the original clears when another is chosen, the order of equals under the nine sorts, and the order of a
  faction's members (the history's, here the countries').
- **The alignment triangle's revanchism takes a faction's share of the victory points as nought** (`alignment.md`;
  `CAlignment`), though faction progress is kept since 2026-10-13 (`factions.md`), so it weighs every held core in
  full. **Its first step agrees with the start save for 80 countries of 101** (`politics.md`, section 13).
  The most-threatening country is worked out for every country each day, where the original does it
  for countries a flag it sets marks.

- **Events fire and decisions are taken** (2026-10-12, `script.md`, `triggers.md`,
  `effects.md`, `events.md`), and much around them waits:
  - **`on_encirclement` fires since 2026-11-14**, a province's days without supply counted as the
    original counts them (`supply.md`, section 18), the start save's every figure given; a battle's
    `last_*_losses` are kept since 2026-11-16 (`triggers.md`, section 5.2), not saved, as the original's;
  - **of messages, none is written on the map** (`onmap`, `messages.md`, *Ours*), and of the bottom
    bar the chat is hidden; whether the original's button that opens the log shows only while it
    is shut was not read;
  - **of a loaded save** (`savegame.md`, *Ours* and *Not established*), a land side's list
    of units falling back is not held - the original's is filled by nothing but a load, so it is
    always empty (`FINDINGS-savedbattle.md`, A.11) - and the two
    conditions that count sunk ships stay unanswered, their bodies unread (section 7).
    `OpenHOI3.SaveCheck load` lists every key left - since the sweep of 2026-11-14 (section 19),
    which made a pair's dates and debt, the offers waiting, the casus belli, a province's
    underground and last convoy attack, the AI strategy's scalars and the null technology's level
    read and written both ways - and `out_of_supply_days` and the ledger's graphs since - only keys
    of systems not built: a province's `capital` and `history`, a brigade's `home`
    and a few more, each of which the original reads. Which hour first sees the officers
    a day adds is not settled; and the first midnight after a load drops two supply convoys the
    original keeps;
  - **whose officer ratio holds a lent unit's organisation is not settled** (`savegame.md`, *Not
    established*): here a lent unit's officers are its lender's (`expeditions.md`, section 4), and
    the Ireland game's saves of 6 September 1939, where Japan has lent 26 divisions to Manchukuo
    and Mengjiang, the six of 23 May 1940 and the twelve of 16 July 1941 fit neither "the lender
    counts them" nor "the receiver does" in every case; it wants a reading, and those saves are its
    check;
  - the front end's multiplayer menu, tutorial list and credits are not built, and are greyed there
    (`frontend.md`, section 2);
  - **`debug_saves` other than nought autosaves nothing** (`savegame.md`, section 11): the
    original's saves under it are named by a routine not read;
  - **of the player's settings, the gamma, the multisampling, the hierarchy's lines, the simple
    water, the trees and the surf, the counter scale, the zoom speed and the outliner's sections are
    applied** (`settings.md`, section 1; `gameoptions.md`, *Ours*): the screen's size, rate and
    full screen are not taken at a start, the scroll and mouse speeds and the legacy camera are not
    the camera's, nothing draws names on counters, and the counter distance is kept and written and
    does nothing. The game options window lists the settings' mode and the screen's, not the card's.
    What the outliner changes is not carried back into the file;
  - **no event is asked in a new game's two first days** - ours; whether the original's are was
    not read (`events.md`, section 2);
  - **the decision list's button's `SELECT` is wider than its 32-pixel picture**, as the files
    have it - not set beside the original;
  - **a condition the simulation cannot answer is false** (`triggers.md`, section 8;
    `CUnansweredConditions`): `remove_fow` and the AI's missions - a country's war record and the last battles answer since
    2026-11-16 (`triggers.md`, sections 5.1 and 5.2), blockades and a province's days out of supply since 2026-11-14
    (`supply.md`, section 18). Inside a
    `not` that makes the `not`
    true, and **such an event fires** - decided on 2026-10-12: "Just fire them";
  - **an effect on what the simulation does not hold does nothing** (`effects.md`, section 8;
    `CUndoneEffects`): `remove_fow`, `add_ai_strategy`, `fixed_ai_strategy`, `add_division` and
    `remove_brigade`;
  - **a change of controller, by script or by a battle, leaves the aircraft and ships based in the
    province where they are** - the original sends them elsewhere (`war.md`, *Not built*);
  - **`has_hostile_spy_mission`** is unread in the original, not unbuilt here, and answers false
    until it is read;
  - **the lists the original keeps a day** - neighbours, allies, the nations a country could
    release - are worked out when asked (`triggers.md`, section 6);
  - **a tactic's or a trait's `trigger` still goes through the small reader it was built
    with** (`CCombatCondition`), which should become this one's - the battle's conditions answer
    false here for want of a battle in the scope, so that is where they go next. A technology's
    `allow` is this one's since 2026-11-14 (`research.md`, section 5);
  - **BlackICE's events have not been read**: a mod is not a layer here yet, and its 178
    event and 98 decision files are the harder test;
  - **missions, spy missions, ministers' and leaders' ids** are taken as written, nothing
    here reading the lists they name (`script.md`, *Not checked here*);
  - **a tag spelled like another of the original's own words** is not caught: only `PHI` and
    `PAN`, the two the base game's and BlackICE's tags collide with, are known.
- ~~**A new game's consumer goods share leaves out the standing army** (2026-11-02)~~ - read and built 2026-11-14: the
  shares are set once the units are read, under the laws brought forward and before an empty post is filled; all 24
  untouched countries' are the start save's, Germany's 0.224 among them, and dissent 101 of 101 (`production.md`,
  section 1).
- ~~**A technology's requirement text knows the base game's conditions only** (2026-10-12,
  `technologyscreen.md` section 9)~~ - since 2026-11-14 the `allow` block is script and its
  account `CConditionText`'s, a decision's, so every condition with words in `scripttext.md` says
  itself. Still open: a building under `any_owned_province` is "in" the province numbered
  nothing, whose name is empty here.
- **The battle window's tooltips are whole since 2026-11-14** (`combatview.md`, section 12): the
  bombed side's rows say their figure and a bombing's row in a unit's panel what of the province
  it bombs, with the original's two words each over the other's goods (`docs/enhanced.md`); neither
  was looked at on screen. A ship's positioning is scaled by `targeting_chance`, taken to be
  the figure the original names `naval_positioning_bonus`.
- **The unit panel's tooltips are whole since 2026-11-14** (`unitpanel.md` section 10): a fleet's or an air unit's base
  and how well it serves, the organisation a brigade regains an hour, the digging-in cap with the technologies'
  `dig_in_cap` - which the day's digging now holds a unit to as well (`landcombat.md`, section 4) - a strategic
  redeployment's wait by its own key, and a brigade's model by its levels with `+*` and a faction leader's name. They
  have not been reviewed in play. One thing is ours: a leader's traits on the way are in the traits file's order,
  where the original's are in the order they lie in its memory, which nothing can give again.
- **The step of ten in the builder windows is Ctrl on the word of a tooltip** (2026-10-11):
  the original's own buttons say "Ctrl + LMB to add or remove 10", and the key its code asks
  was not traced (`builders.md`, section 6). It was shift here until then.
- **Which end of a scrollbar that runs up and down adds is not settled** (2026-10-12): the
  original's scrollbar is given a flag that swaps its two buttons, and what sets it was not
  read (`interface.md`, section 2). The sliders, across, take away on the left as here.
- **A land battle has its leaders since 2026-10-11**, and their aggressiveness since the plans, given to the attacker's
  pick alone as the original gives it (`landcombat.md`, section 11). Of the things that bear on a unit in a round, three
  wait for what they read - shore bombardment, mission efficiency, landing craft -
  and three technology figures are left out for want of their keys; `landcombat.md`,
  section 4, lists them. A round gives no practical knowledge (section 3).
- **What is left of the battle window's list** (`docs/mechanics/combatview.md` ends with it): the naval map mode's
  icons and a land marker's first three hours. Units in a battle stand in a stack for the attackers and one
  for the defenders since 2026-11-14, as the original's (`mapunits.md`, section 3); what a click on the original's marker does was not read - here it opens the window.
  An enemy the player knows under intel level 3
  shows the unknown counter's "?" since 2026-11-13, its picture alone, as the original's
  (`combatview.md`, section 5).
- **Whether a land counter beats the large air base picture in the air map mode is not read.** Here the large air base is
  asked first in that mode. In the original every picture is scored and a counter always beats a building picture - the
  large air base's among them, 1.0 to at least 1.1 (`reversing/findings/FINDINGS-seaair.md`, section 6, read off the bytes on
  2026-11-13) - so it hangs on whether land counters are drawn, and so hit, in the air map mode. Outside it a counter over an
  airfield's picture takes the click here as in the original, and an air unit at its base under a land stack is reached
  from the air map mode (`docs/enhanced.md`, *Interface*). `--select=<name>` picks one for a trial.
- **The air and naval map modes' greys are the original's since 2026-11-07**: the surface is handed each province's
  colour as it is, and the install's `terrain_2_0.fx` and `water.fx` grey the textured ground and lay the colour over it
  and the water, as the original's do; `CMapView.AirOver` mixes them itself only for the flat map, with no map cache.
- **The air map mode is drawn by rules of ours where the original's were not read**
  (`airmap.md`): its icons stand about
  the province's own point, not its label's; the small building pictures are still drawn
  under the large ones; the large pictures and their frames are worked out once a day; a
  ring's range is the shortest of the unit's wings'; and the rings and icons show from any
  height.
- **The air orders window's map rests on one inference** (2026-11-14; `fleetsandair.md`, section 4): a province out of
  the unit's reach is handed to the shaders as the original's "nothing", alpha nought, in map style 16, and comes out the
  ground greyed and darkened and the sea near black - which holds only if the original's land is drawn with the alpha
  test off, *inferred* and not watched in the running game (`mapdraw.md`, section 4). The paradrop's start held every
  frame is built and was not tried on screen - no 1936 order of battle has a transport plane.
- **Every text box breaks its lines as the original's font does since 2026-11-14** (`interface.md`, section 6): an
  `instantTextBoxType` by `CFontLineBreak`, a `textBoxType` by `CTextBoxLineBreak` - the original draws the two through
  different routines of its font - a box of fixed size cut first to the original's " ..." (`CFixedSizeCut`), and a
  tooltip, a row of the message log, a line of the lobby's chat, the diplomacy status list, the technology panel's
  effects and an edit box's text laid out by the original's text layout (`CTextLayoutLineBreak`) - the edit box showing
  the caret's line, the last where a screen set the text. **A button's caption** is laid out as the original's button
  lays it out (`CButtonCaption`; `reversing/findings/FINDINGS-textdraw.md`, section 1): on a text sprite as a
  `textBoxType`'s text in the sprite's picture - cut, broken, centred each way, cut off past the picture - and with no
  sprite as an `instantTextBoxType`'s in its `size`; a scrollbar's end buttons keep one line in the middle, how the
  original draws theirs not read. **A `textBoxType` is placed by its own routine since the same day**
  (`CTextBoxPlacement`; `reversing/findings/FINDINGS-textdraw.md`, section 2.2.1): a centred line without its border,
  a right one a pixel short of it, every line at the border in a box with no height, no orientation in it, the lines
  centred up and down where it has a picture behind it, and **cut off past its height and width less its border** -
  and **the picture behind its text is drawn**, nine parts stretched, two short of the shown part's far edges where its
  border is not nought (`CTextBoxPicture`, section 2.2.2), the tooltip's and an edit box's alike. Not built: the one-unit
  squeeze of that picture on screen and the original's nearest sampling of it. **Where an edit box puts its line is
  settled since the same day** (`CEditLinePlacement`; `interface.md`, section 8): as that routine places a line that
  fits - the border in twice across, and down the border twice or, with a picture, centred in the box's height - set
  beside a screenshot of the original's save dialog with a name typed and one of its unit panel, glyph
  row for glyph row; `game_name`'s line is two lower than before and its caret one higher, and every edit box's text
  one further right. Measured and not read: the line with a picture stands a unit higher than the one without, against the
  reading's arithmetic - why was not found. **A box whose font is a `font = { }` entry is another routine's** (read
  2026-11-15, `reversing/findings/FINDINGS-textdraw.md`, section 2.5): the console's line, in `FPS_Font`, has its cell
  the border twice in each way and is never centred, and such a font is drawn at the size whose cell is its `height`, in
  its own colour (`CGuiFonts.MakeTrueType`); its two background routines were not read, and no screenshot of the
  original's console has been set beside ours. The screens have not been reviewed in play since these changes.
- **Four rules of how the interface draws are measured on screenshots and not read**: a
  bar's picture upside down, where a turned flag stands, where a round shield puts its
  flag, and where a name too long for its box is cut - the last of which ends ten pixels
  later here than in the original. `docs/mechanics/interface.md`, sections 5 and 7.
- **Where the heavy border runs beside occupied ground** is read off one screenshot of the
  original's and not out of the original: at the edge of what a country holds, not of
  what it owns. Which colour the stripes are is an account of the original - the wide bands the holder's,
  the narrow the owner's. `docs/mechanics/map.md`, section 10.
- **The game's rolls start from a seed of 1** every game, so the same orders fight the same
  battle. Nothing sets it yet; a new game should. `CGameState.Seed`.
- ~~**Of `positions.txt` a province's points are read but for its city**~~ - **every point is read since 2026-11-14**:
  where its units stand, where its name is written - its terrain is taken around that - where its port is, the land end
  of a distance to the sea, where each of its buildings is drawn, and its city, where the capital marker stands on the
  owner's acting capital (`docs/mechanics/map.md`, section 4, and `mapunits.md`, section 9;
  `reversing/findings/FINDINGS-citypoint.md`). Checked with `--click` on Lisbon, Their Finest Hour's one province with a
  `city`, and not set beside a picture of the original's. Left: **the order the original draws a group's pictures in**
  was not read, so whether a building on the marker's point lies over it or under is the reading's guess. ~~**A building
  the file places is drawn half a pixel west and south of where the 2D map draws a world point**~~ - **mended 2026-11-15**:
  the original stands every picture it hangs on a province at the file's point exactly, a world point, so
  `CMapBuildings.PlaceOf` answers world points, placed or not, and the 2D layers - the buildings, the capital marker, the
  air and naval modes' large bases and rings, the resource icons - draw them through `CWorldPoint` (`mapdraw.md`,
  section 3; `mapunits.md`, section 9). Checked with `--click` at Kiel and Berlin against main: every building picture
  half a pixel east and north of where it was, and a click on the port's and the airfield's still picking their units.
- **Of an air unit's orders seventeen are built**: air superiority, the intercept, the eight
  bombing missions, the change of base, and since 2026-10-16 carrier protection, the paradrop,
  the rebase to carrier and the air supply (`transport.md`, section 13), the nuclear bomb
  (`nukes.md`), and since 2026-11-14 the raid on convoys (`navalmissions.md`, section 8.1). The
  window lists the reserve as the original does, and it cannot be ticked.
  `docs/mechanics/fleetsandair.md`, sections 4 and 5.
- **A battle at sea, a battle in the air and a bombing have no marker on the map**, because
  the original gives them none: its marker is a land battle's alone, and from the battle's
  fourth hour (`combatview.md`, section 8). So they are reached by the row in a unit's
  panel and by nothing on the map. **The sea battle had a marker here when it was
  reviewed in play**, and its going was not raised for a decision: one line of `CCombatViewText.HasMarker`
  brings it back if the user remembers the original otherwise. What the original shows of them
  on the map is in its **air and naval map modes** - icons of the battles that are over: the
  air mode's are built (`airmap.md`), the naval mode's of convoys sunk and raiders fought are
  not (`combatview.md`, *What is not built*).
- **The retreat button sends a land or an air unit where a beaten one goes, as the original's**, since 2026-11-14
  (`combatview.md`, sections 2 and 11), and a fleet where a beaten one runs, once its count to break away has run (section
  10, since the same day).
- **A battle in the air has been set beside the running game for one hour and never watched.** Its window and its
  row in a unit's panel were tried with real clicks and they have not been reviewed in play
  (`combatview.md`, section 11); an hour of one over a province came out the original's, every draw and figure, on
  2026-11-10. A battle earns no practical. `aircombat.md`, section 10. `OpenHOI3.SaveCheck airbattle` is a trial of it.
- **Bombing has been set beside the running game for three hours and never watched** - three land bombings of August
  1938, an hour each, the original's on 2026-11-10; its window is the air battle's and as little
  looked at. The practical a bombing earns is not built. `bombing.md`,
  section 13. `OpenHOI3.SaveCheck bombing` is a trial of it.
- **The fog of war on the map is built since 2026-11-07** (`mapunits.md`, sections 3 and 9): a stack the player knows
  under 2 is not there, under 3 it is one counter with the unknown picture, a name under 5 is `???` and the figures under
  7 `?`; a building shows where the province is known at all or the building is `visibility`, a base in use from 2; the
  terrain mode and the others dim what is known under 2. A unit's level is worked out when the counters are made again,
  where the original copies it as the unit enters a province.
- **A building has two levels now, and most rules were written when it had one.** A
  province's modifier is made from what works of a building, and whether there is a port or
  an airfield is asked of what was built. The supply network's roads are what was built, as
  the original keeps that figure, and a base's room is what works of it, as was read. **The
  rest were not read for which they mean** - the order of an area's harbours, where a depot
  may be, a convoy's choice of harbour - and are given what was built, so that bombing does
  not rearrange a network. `bombing.md`, section 7.
- ~~**No AI gives an air mission**~~ - the AI's air body gives them since 2026-10-19 (`moving.md`, section 17): close
  support, intercepts, logistical strikes and naval and port strikes. It gives no strategic bombing, nothing yet
  handing it the enemy's war industry.
- **A building's picture is drawn at a size and from a zoom that are the reading's
  and ours**, never set beside the original; and a selected fleet in port has nothing on
  the map to show it is selected. `docs/mechanics/mapunits.md`, section 9.
- **A unit now moves before its organisation returns, not after.** The reading for fleets
  found that the original marches its units in the routine that looks at their orders, one
  country after another, before any unit's own hour. `docs/mechanics/tick.md`, section 3.
  Nothing yet reads one from the other.
- **A province's terrain no longer follows the rule given before.** That rule, from
  modding the original, was the terrain covering most of the province; the original's,
  read on 2026-10-09, counts only a square of twenty pixels around the province's name. It
  changes 384 of the base game's 10,621 land provinces. Nothing in a savegame says which
  is right, so it rests on the reading. `docs/mechanics/map.md`, section 9.
- ~~**A unit too weak is not kept off an enemy's ground**, as the original's route keeps it, nor a
  country from attacking out of a neutral that merely lets it through; and no message says a unit
  has arrived.~~ All three built 2026-11-14 (`movement.md`, sections 1, 3 and 6). ~~Left: the rest of the
  click's test of a province - a unit in a battle sent only where it may fall back~~ - built 2026-11-14 whole
  (`CMovementRules.MayMoveTo`, `movement.md` section 1): a land unit at sea sent only to land beside its sea, a unit in a
  battle only where it may fall back, none with a battle in the next province of its way, a fleet only where the sea joins
  its own past closed waters. Left: **the AI's `CEU3AI::MoveUnit` asks the same test as its second gate**
  (`reversing/findings/FINDINGS-liveread.md`, section 88), which `CAiMoves` does not yet; the route not asking a fleet's
  goal whether closed waters cut it off (`fleetsandair.md`, section 1); ~~and the click's marks~~ - built 2026-11-14,
  `legal_selection_projection` and `illegal_selection_projection` through the install's `project.fx`, cut to a land
  province's own triangles (`movement.md`, section 7; `reversing/findings/FINDINGS-movement.md`, section 13) - and
  ~~`selection_projection`, the mark under a selected unit, whose maker was not read~~ - read 2026-11-14, the original's
  only where `alwaysCounters` is off and so never drawn here (`movement.md`, section 7;
  `reversing/findings/FINDINGS-movement.md`, section 13.8) - ~~leaving an air unit in a battle, which the
  original sends straight on where this project opens the orders window~~ - built 2026-11-14, sent with the sent mark
  and nothing asked, and a unit in a battle sent wherever it comes among several selected (`fleetsandair.md`, section 4;
  `selection.md`, section 3). ~~`EXPRETURNED`'s Goto,
  whose message carries no unit~~ - built 2026-11-14, the message carrying the unit and its Goto selecting it whoever
  holds it (`messages.md`, section 4.1). `docs/mechanics/movement.md`, *What is not built*.
- **The march's speed is checked against savegames and its arrival is not.** The
  distances agree with the original's cache exactly and what an hour adds agrees with
  saves hours apart, division by division; the hour a unit arrives and what it keeps are
  as read. ~~**There is no weather**~~ - built 2026-10-15 (`weather.md`): mud and frost slow a march here as in the
  original's January. `movement.md`, section 5.
- **The AI's two careful route finders weigh a fleet or an air unit as the plain one does**, where the original's
  weigh them otherwise; for a land unit they are built (`movement.md`, section 3; `moving.md`, section 19). A player's
  order uses the plain one.
- **The arrow of a selected unit and the pointer beside every moving one are the
  original's own**, each read and held against a screenshot of the original's given on
  2026-10-09: the arrow a band along a curve through the path, hollow ahead of the unit
  and filled behind it, from the original's picture of it. One cut is added at the very
  end so that the tip is always whole; the original's last is a little short of it and
  its tip is whole all the same, which was not made out. Where a moving unit's counter
  stands is from one screenshot, by a stack whose depth had to be counted off its edges.
  `docs/mechanics/movement.md`, section 7, and `mapunits.md`, sections 1 and 2.
- **How a rotated icon is placed is measured and not read**, from the unit panel alone (`docs/mechanics/interface.md`,
  section 7). Where an edit box puts its text is read since 2026-11-14 but for one unit up and down that differs with a
  picture behind the line, measured on two screenshots (section 8). A unit is renamed from its name box since 2026-11-12
  (`unitpanel.md`, section 3).
- **Attrition is built since 2026-11-14** (`upkeep.md`, section 3a): a land unit out of battle wears down each day by its
  province, the weather and the ground, and the panel shows its figure. The panel's tooltip breaking the figure into its
  causes is not built.
- **The hierarchy of a selected unit is on the map, less the ring an AI's command adds.** A
  selected land unit is joined to its commander and everything under it to theirs, in the
  original's picture and colours, a unit out of radio range by a broken dark red line
  (since 2026-10-11); H switches the lines off and on, as the settings' `render_hierarchy` does; and the counter of
  every unit under it wears the original's second ring colour. The lines are
  drawn at any zoom, and a selected unit an AI commands does not wear the original's green ring (`ECounterRing`),
  though a headquarters is handed to the AI since 2026-11-09 (`unitpanel.md`, section 9).
  `docs/mechanics/mapunits.md`, section 8.
- **What shows on the map at which zoom is the original's, told in its camera's heights**:
  nothing but the map from a height of 500 up, the far counter under that, the near one
  under 100. The decision for the two views, 2026-10-09, and the camera read for
  the figures (`mapunits.md`, section 7). The zoom here goes out further than the
  original's camera goes up, until the whole map is on screen.
- **A province's five stacks are the original's since 2026-11-14**, the moving stack at the original's 2.5 east and
  1.0 north by decision, where a screenshot's measure had put it. A battle's two sides have their stacks, on the
  point on land and 2.4 to either side at sea, and which counter of a stack is on top is read: the player's own,
  then the higher rank, then the selected unit's place in the province's list (`mapunits.md`, sections 1 and 3;
  `reversing/findings/FINDINGS-mapstacks.md`). Of two units the list does not part, the original puts first the one
  that came into the province first; this game keeps no such record, and puts first the one made first.
- **The provinces a unit may be placed in are tinted green our way**, over the mode's colours, which have been
  reviewed in play and passed; the original paints the same grey and green for a unit as for a building, left so until it has
  been looked at (`buildinglines.md`, *Ours*; `mapunits.md`, section 6).
- **Loading runs under the loading screen** since 2026-11-11, on a thread of its own where it touches no engine object; making the front end's map still holds the screen still in two pieces, about 1.1 s and 0.8 s with a frame drawn between, where it was one of 3.2 s until 2026-11-14 (`docs/mechanics/loadingscreen.md`, *Loose ends*) - on the first front end of a run alone since the same day, a return to the menu keeping the files no game changes and so the surface: read in about 1 s where it was 4, its map made in 20 ms (section 3).
- **Things the start save shows that are not explained**, since 2026-11-14 (`OpenHOI3.SaveCheck firstdays`):
  every stockpile's metal, rare materials and money, every brigade, technology, ability and line of the queue and every
  country's manpower are the save's, Germany's every figure but 0.030 of supplies; what is left is **the supplies of 47
  countries the AI runs**, most of them 2.010 more than the save's, and the energy, crude oil or fuel of a handful - the
  United States, Japan, the Netherlands - which the AI's shares and its first day's conversions decide. **The AI's own
  shares by the save** are its production minister's, and when the original's first sets them, before the first hour,
  was not read. **What repeats the Lua's look at a new game's laws** - three passes for Germany - was not found; built as
  a look repeated until nothing is posted (`politics.md`, section 12). And **whether the filling of empty posts waits
  on the queue as the laws do** is open: the shares and Brazil's manpower are as the set-up leaves them before the
  filling, but the money of the countries that do not exist counts their filled ministers in the two days; here the
  posts are filled at once, after the shares are set (`politics.md`, section 7). Answered on 2026-11-14 and off this
  list: home production for three countries and governments for countries that do not exist (`resources.md`,
  section 5), what sets a country's shares at the start, and why sixteen untouched countries make 10% more supplies than
  the save's laws give (`production.md`, sections 1 and 7). Answered before: three countries' extra money is the gold
  they hold, and a country at war starts with five days fewer of resources for each month of the war
  (`docs/mechanics/stockpile.md`, section 2).
- **The four windows a unit is ordered from have been tried by hand and never measured
  against the original.** They are built from a reading of its code, the figures and the
  rules checked in the listing. The first try, on 2026-10-08, found four
  things wrong; they are mended and on the second the windows were found right
  (`builders.md`, section 10). Parts of them are ours for want of a reading - what saving a
  template does, and how a country with no `default_templates` comes by any.
  `docs/mechanics/builders.md`, *What is not established*.
- **Which element takes a click is ours, but for two things seen in play.** Nothing
  of how the original hands the mouse to an element was read. A button, a scrollbar and
  an icon take a click, the uppermost first; a window, a text box and a list box do not;
  and an icon hands its click on to a button of its own window that it lies on, as the
  original's does - a click on a line's counter picks the line. What an icon does over
  anything else is a guess. `docs/mechanics/interface.md`, the last section.
- **A texture no layer has under the extension it is named with is still looked for
  under another**, `.png` among them. The original looks for a `.tga` as a `.dds` and for
  nothing else, and reports the rest missing. `interface.md`, section 5.
- **An order is refused here that the original would carry out.** Its commands ask
  nothing and its windows do the refusing; here the command refuses too, so that nothing
  can be ordered round the window. Not a departure a mod can meet: the same things are
  refused, a step later. `builders.md`, section 9.
- **A unit type's `available_trigger` is asked since 2026-11-14** (`builders.md`, section 2), of
  the country alone, as the original's count of what may still be built asks it; the base game's
  seven are empty and pass, so only a mod's decides anything.
- ~~**Six countries' queues start somewhere else than the original's**~~ - answered 2026-11-14: the original prices a
  new game's lines under the laws its own new-game pass brought forward, and the Lua's changes come after the two days
  (`buildqueue.md`, sections 5 and 9). All 30 lines start where the start save's do, and a new game as a player gets it
  has all 30 where the save has them two days on. What the default check's two days still differ in is the shares the
  AI has set by the save.
- **A battle at sea was read and never watched.** What decides its course - the order
  ships are drawn up in, who is shot at - rests on the reading alone (`navalcombat.md`,
  the end). `OpenHOI3.SaveCheck seabattle` is a trial of it.
- **The sea battle's window was measured against no picture of the original's.** It was
  reviewed in play on 2026-10-10 - "The sea battle window looked perfect"
  (`combatview.md`, section 10) - so where a ship's picture stands in its row and which of
  its two frames it shows have that review's eye and no measurement; the land battle's window had
  four things wrong that only screenshots of the original showed. Its sounds are built since 2026-11-12 (`sound.md`, section 8).
- **Carrying by sea and by air are built, and neither was reviewed on screen in play**
  (`transport.md`); the carrier, the paradrop and the air supply were tried only with
  `OpenHOI3.SaveCheck airborne`. The AI drops no paratroopers (`moving.md`, section 20); and the technology figures `paradrop_mission` and
  `transport_supplies_mission` do nothing, no reader of them having been found. Three small things
  are ours and listed in the spec's *Ours*: the nearest air base a carrier's groups go to and the
  order the planes' load button looks in - a paradrop's start is held every frame while its row is ticked since
  2026-11-14, as the original's (`transport.md`, section 13.4).
- ~~**A fleet's `select_land` and `select_air` buttons do nothing**~~ (found in testing, 2026-11-14) - **built
  the same day**, the two clicks read whole (`transport.md`, section 12): pressable where anything is aboard and where a
  carrier has aircraft, each lets go of everything selected, the fleet among it, and selects the passengers or the
  carriers' air groups in order, the map left where it is. **An air unit's `select_land` may be pressed and does
  nothing**, as the original's (oddity 21, `enhanced.md`). Tried with `--click` on the French transport fleet of the
  7 January save and the Home Fleet of 1936; it has not been reviewed in play.
- ~~**A fleet's and an air unit's strength figure is empty**~~ (found in testing, 2026-11-14) - **built the same
  day**: the count of its own ships or wings, plain - the Home Fleet's 19 - read out of the original (`unitpanel.md`,
  section 3) and looked at with `--click`.
- ~~**A carrier air group's panel lists no units above it**~~ (found in testing, 2026-11-14, on a screenshot of
  the original) - **built the same day**: the original hangs an air group aboard a carrier under the carrier's fleet in
  the order of battle - as an order of battle or a save is read, and as it is rebased to the carrier - and the panel
  walks up the tree and nothing else. So here too: HMS Furious's 1st Air wing shows "Home Command Theatre" and "Home
  Fleet", a click on the fleet's row selects it, the order of battle browser and the outliner show the groups under
  their fleet, and the fleet's own list of units under it leaves them out (`unitpanel.md`, sections 2 and 8;
  `transport.md`, section 13.1, which lists what else follows - the radio reach, the traits, `split_troops`, a sunk
  carrier's groups staying under the fleet). Tried with `--click`; it has not been reviewed in play. **Left**: what
  the removal of a headquarters does with the units under it - the original's `CGameState::RemoveUnit` leaves each with
  nobody above it, where here an army's, a corps' or an army group's go to the headquarters above it; a removed fleet's
  are left so since this item, the rest not (`reversing/findings/FINDINGS-transport.md`, section D.1).
- ~~**A raid from the air is not built**, nor any message about a raid~~ - **built 2026-11-14**: the raid on convoys
  from the air, its pick of a province and its bombing of a convoy's ships, and the three messages of a raid,
  read out of the original and tried with `OpenHOI3.SaveCheck raid ... air` (`navalmissions.md`, sections 8.1 to 8.3).
  The patrol's `PATROLTRACK` is built the same day (`navalmissions.md`, section 3.1), tried with
  `OpenHOI3.SaveCheck raid ... patrol` and a row of the log clicked. Left: a lend-lease convoy's lost efficiency,
  whether the original's AI ever gives the air raid, and none of it has been reviewed in play.
- **Who is played by a person is state the client sets**, `CGameState.Played`, and not a
  command: it is asked in one place, the idle fleet's, and wants a proper home when a
  game can be joined or left.
- **A fleet whose last ship is sunk is removed from the game at once**; what the original
  does with an empty fleet was not followed.
- ~~**Seven countries that do not exist make about a tenth fewer supplies than the save says.**~~ All 23 do since
  2026-11-14: the start-date run's "supplies made in a day, where nothing has moved the shares" gives 23 of 23, the
  shares set where the original sets them and the Lua's changes of law run after the two days
  (`reversing/findings/FINDINGS-startshares.md`; `docs/mechanics/production.md`, sections 1 and 7).
- **What a province yields is now read, and is still computed as it was measured.** The
  original takes the technology first and then one factor that is the sum of the province's
  and the country's modifiers; here they are multiplied in turn. They agree with the save for
  78 countries of 78, and could part under a law and a local modifier at once.
  `reversing/findings/FINDINGS-supplyconvoys.md`, section 8.
- ~~**The supply map mode shows the player's own land only.** It takes what the player knows of a province as 9 where
  he holds it and nothing elsewhere (`CSupplyMapColours`), though what a country knows of every province is worked out
  since 2026-10-14 (`intelligence.md`), so every other country's land is the grey the original gives land one knows
  nothing of; and its tooltip says of anybody else's province only its name and owner.~~ Mended 2026-11-18, after testing
  found foreign land empty with the fog of war off: its colours and its tooltip ask the player's level of
  each province, as the original's do, and it is coloured again on a change of the diplomatic statuses too.
  `docs/mechanics/supplymap.md`, section 2.
- ~~**A new game's laws are brought forward before its two days, and none is changed after them.**~~ Investigated as
  asked on 2026-11-12, and answered on 2026-11-14: the engine's own pass brings the laws forward before the
  days, and **the install's Lua's changes of law go through the command queue and run after them**, which is what holds
  every brigade to the running game's ceilings by the first screen. Built so (`politics.md`, section 12; `tick.md`,
  section 6): `OpenHOI3.SaveCheck firstdays` gives every brigade's organisation the start save's, 3,792 of 3,792 -
  Germany's reserves at 28 where they stood at 35 - so a reserve sent to attack before the first hour no longer fights
  above its ceiling. **Left**: that the Lua's changes run after the days rests on the command route and the save, not
  on the queue's drain read off the bytes (`inferred`); a breakpoint on the law command while a new game is entered
  would settle it (`reversing/findings/FINDINGS-startshares.md`, section 2).
- ~~**A counter always carries its unit's name**, where the original reads the player's `show_unit_names`: on, a near
  counter carries the unit's name cut to eight letters, and `???` below intelligence 5; off - a new install's default -
  its leader's name, and nothing for a unit with none (`docs/compatibility.md`, entry 7, which ended with the settings;
  `reversing/findings/FINDINGS-unitrename.md`, section 4). The setting is read and written (`CUserSettings.ShowUnitNames`,
  the game options' box) and the counters do not ask it. Put back as open, 2026-11-12.~~ Built
  2026-11-14: the counters follow the setting as read, from the start and from Apply at once, a change of leader shown
  the moment it is made while it is off, and `--unitnames=no|yes` overrides it for a run (`mapunits.md`, section 2);
  compatibility entry 7 undone.
- **Starting a game takes about eleven seconds**, measured 2026-11-12 with the optimised build and `--country=GER`:
  1.8 before the first loading line; **4.8 making the world** on the loading screen's thread - 2.2 of it the two first
  days (the supply filling 1.0, the first day 0.8 with the first convoys' routes about 0.5 of it), the opening laws,
  technology and modifiers 0.7, the province history 0.4, the AI's strategies 0.3; **3.2 the map and the interface** on
  the main thread, the window not answering - the 3D surface's meshes 1.2 (about half our arrays turned into the engine's,
  half the engine's own), its first style 0.6, the interface 0.7; and 1.3 preparing the map modes. Matching a mesh's
  semantics once an input and not once a vertex was tried and saved 40 ms. **From the menu the surface is no longer made
  again** since 2026-11-12: the game takes the lobby's (`loadingscreen.md`, section 4), and from Play the main thread is held
  1.06 s where it was 2.34, the map modes take 58 ms where they took 1.14 s, and `LOAD_GFX` to the game is 1.27 s where it
  was 4.34. A game from a switch still makes its own. **Nor are the borders' corners read again or the country screens made
  as a game is entered** since the same day: the main thread is held 0.42 s and `LOAD_GFX` to the game is 0.60 s. And the
  export ships its code compiled ahead (`PublishReadyToRun` in `OpenHOI3.csproj`): a new game made and a day run in the
  savegame tool, published both ways, took 7.4 s where it took 7.9, for some 8.5 MB more in the export. What is
  left wants more change of structure: the supply filling's networks passed side by side, which is checked against saves
  network for network, and the passes of a new game country by country. A loaded 1941 game held the main thread 2.5 s
  after the surface was kept, not measured before it and not looked into.
- ~~**A province's days out of supply are not counted**, and nothing is raised on its first.~~ Counted since 2026-11-14,
  read and written in a save, and `on_encirclement` raised on the first day where the area is landlocked (`supply.md`,
  section 18); Their Finest Hour's ids there are no events.
- ~~**The fuel a new game starts with is exact for 18 countries of 76**~~ - a new game's fuel is the start save's for
  76 of 78 countries since 2026-11-14 (`OpenHOI3.SaveCheck firstdays`: the Soviet Union 0.053 and Colombia 0.006
  apart), the opening laws and shares now standing where the original's do. The 18 of 76 is still what the start-date
  run's day-nought sum gives ("fuel at the start: all a country's provinces hold, less two days' refining"), which
  reasons from the files and not from a game's own first days. `docs/mechanics/upkeep.md`, the end.
- **The AI's politics neither liberates a country nor releases a puppet** (2026-10-14, `politics.md`, sections 11.1
  and 12): both stay reported as unbuilt. What repeats a new game's look at the laws in the original
  was not found, and a minister's hour rests on a numbering of countries that is ours.
- **Whose units share a base, and whether a technology's `naval_base_efficiency` is what
  widens a port**, are `inferred`. `organisation.md`, section 4.
- **A new game's theatre headquarters' brigades agree with the save in everything, 86 of 86,
  since 2026-11-14** - in experience and organisation too once the Lua's changes of law were
  run after the two days (`politics.md`, section 12). The order of battle under them is the start save's on the first screen,
  106 of 106 theatres level by level, the AI's first pass running as a game is entered
  (`agents.md`, section 2.1).
- **Leaders in battle are built whole since 2026-10-11** (`docs/mechanics/leaders.md`,
  sections 9 to 11) but for two things: the aggressiveness a tactic is picked with, built since 2026-11-09 and until then
  the AI's, and `winter_attrition`, which counts in attrition since 2026-11-14 (`upkeep.md`, section 3a). Nothing of a leader's
  experience or progress towards a trait is set against a save: saves hold battles since 2026-11-05, but not the
  generator's state, so none can be played forward (`landcombat.md`, section 8).
- ~~**An order of battle that names a leader no file declares** has him ignored; what the
  original does was not read.~~ Read 2026-11-14: the original does nothing with the line - no log,
  nothing made - and the unit keeps what an earlier line gave it, or is given the best free leader as
  any unit without one is; built so, and the line reported (`leaders.md`, section 5;
  `reversing/findings/FINDINGS-modifierkinds.md`, section 5).
- **Only the player turns a country's `auto_assign_leaders` off**, with the leader window's
  checkbox: every country starts with it on, as in the original, and nothing here changes it for any
  other country.
- ~~**A block of a unit type is read and not kept**: `extra_amphibious_defence`.~~ Kept on each type since
  2026-11-14 and asked as the original asks it, of a fleet's summed table, which never holds a ship's - so it
  does nothing, as in the original (`transport.md`, section 9; `docs/enhanced.md`). What the original does with
  a name in it that is no unit type was not read; ours reports it (`units.md`, section 1).
- ~~**A new game's manpower is the original's for 43 countries of 78**~~ - 78 of 78 since 2026-11-14
  (`OpenHOI3.SaveCheck firstdays`): a new game's pool starts at its most after the engine's pass of the laws, which is
  Germany's 882, and the history's decisions are taken as a game is set up - the United States' `new_deal`, which
  costs it the 40% that left 255 (`reversing/findings/FINDINGS-startshares.md`, sections 2 and 4;
  `docs/mechanics/manpower.md`, sections 4 and 6; `events.md`, section 13).
- ~~**Strategic resources are not shared across continents**~~ - since 2026-11-14 they are, as the original shares
  them: by naval bases beside one of the coast regions `map/coastlines.txt` names (`docs/mechanics/modifiers.md`,
  section 6). Left: the original's list of a country's naval bases is a day behind a base finished in play, here it
  is not (section 7); and none of it has been set against a running game - no save shows a country's levels.
- ~~**A resource once had is never lost here, and that the original behaves so is only `likely`**~~ - `confirmed`
  2026-11-14 for as long as the game runs, and a load forgets it in the original and here
  (`docs/mechanics/modifiers.md`, section 6; `docs/enhanced.md`). Not watched in a running game.
- ~~**A technology's `allow` block is read for the six kinds of condition the base game
  uses**, and none of the six has been read out of the original.~~ Read and built 2026-11-14:
  the original holds the block as the general trigger and asks it of the country alone, so it is
  read as script here (`CTechnologyAllowReader`) and answered by `CConditions`, any condition a mod
  writes with it; a home port is one the country **owns** with a naval base built, in its acting
  capital's area, and a theory or practical is no condition. The start's technology 28,987 of
  28,987 still. `docs/mechanics/research.md`, section 5.
- **Leadership makes officers that nothing takes away.** Influence is spent by diplomacy and
  spies are placed by espionage; a country whose presences are full or at priority 0 adds to its
  free spies without end, as the original's does. Officers are used - the ratio of them to what the units call for is what holds
  organisation - but only battle loses any. A country does start with
  the officers its units bring (`docs/mechanics/units.md`, section 7).
- **The savegame check's main run still reasons from day nought.** It loads a game with
  its two days not run and takes two days of each figure off the save, system by system,
  which is how each rule was first pinned down; its model of the units' two days has an
  hour before each day, where the original - and now the game - has one hour after both.
  The two give the same figures on the start save. `OpenHOI3.SaveCheck supplypass ...
  countries` is the check that runs the game's own first days (`tick.md`, section 6).
- ~~**The laws the original's first two days ran under are not all known.**~~ Known since 2026-11-14: the laws the
  engine's pass brings forward, the install's Lua's own changes coming only after the days
  (`reversing/findings/FINDINGS-startshares.md`, section 2). The start-date run, which loads the save's laws, says which
  countries fit only with their history's put back - on 2026-11-14 sixteen, every one by its industrial policy and none
  by its training law (`docs/mechanics/upkeep.md`, section 9). That the Lua's changes run after the days is `inferred`,
  as the entry on a new game's laws above says.
- **An icon or a button is the pixel size of its texture.** That is right for the
  original's art and is the one thing a sharper replacement must not change; what says
  the size a texture was designed at is still not decided (*The interface*, above).
- **Only Arial's stand-in has Arial's widths.** Liberation Sans was drawn to them, so
  text in it lands where the original's did. Fira Sans for Trebuchet MS, Barlow
  Condensed for Franklin Gothic, EB Garamond for Garamond, Anton for Impact, Breitkopf Fraktur for Olde English (the Axis
  names, downloaded by each builder) and Code Red October for KyrillaSansSerif (the Comintern's) are near relatives,
  sized by the height of a line and narrowed where they come out wider; which stands
  for which is a choice made by eye and easy to change (`fonts/readme.md`). Two display
  typefaces the original names - Eurostile (the counters') and SV Basic Manual (the date's) - and BlackICE's Verdana have no
  stand-in and are drawn in Liberation Sans, with a line in the log; all three are small, so the distance fields of
  2026-11-18 do not reach them. A large font is drawn from its own glyphs, so Breitkopf Fraktur and Liberation Mono's
  place as Another Typewriter's draw nothing in the base game or BlackICE today: `old_english` and `kart_font` are
  large. `docs/mechanics/interface.md`, section 6.
- **How scrollbars and boxes of overlapping elements lay themselves out is ours.**
  Neither was read out of the original; each is the plain reading of what the files
  declare. ~~So is where a line of text breaks.~~ That was read on 2026-11-14 (`interface.md`, section 6). **The list box was the third, and when it
  was read on 2026-10-09 the plain reading turned out wrong** - the original spaces
  nothing and cuts nothing off - so the other two are worth less than they look.
  `docs/mechanics/interface.md`, sections 2 and 6.
- **The base game's ship list is 620 high in a frame of 300.** That is what its file
  says, and by the routine a list is as many lines as its height holds: a thirteenth
  ship type is drawn across the frame's lower edge, where the
  game is remembered never drawing one. It shows only with every ship type to build, which
  `--all-units` gives. Not settled: `docs/mechanics/builders.md`, section 10.
- **The two screens have been tried by hand.** They were used on 2026-10-08:
  dragging a slider, the folder buttons, the queue's priority and cancel buttons,
  `start`, the queue's dates reading year first and a falling resource in red with no
  minus sign are all right. Four things were wrong and are mended - a locked slider is
  grey, the list's arrows are the right way up, text is placed as the original's font
  places it, and the panel's category picture is full size. The figures themselves
  have not been compared one by one with the original's.
- **What the two screens' buttons do is ours**, from how the game is remembered and from
  its own tooltip keys: the original's handlers were not read.
  `docs/mechanics/technologyscreen.md` and `productionscreen.md`, what is not
  established.
- **What the production screen's queue lines show was read out of the original and has not been looked at beside
  it**; what their buttons post is ours. `docs/mechanics/productionscreen.md`, section 6.
- ~~**How the technology panel's `effects` list draws a row wider than 180 or than the list** was not read; here nothing
  is cut.~~ Read 2026-11-15: **nothing clips it** - the original's interface clips no object to another, its draw
  list's scissor cutting to a rectangle that is only ever zeroed for an object nothing flags, and an
  `instantTextBoxType` draws every letter it laid out - so a line stands whole up to about 11 units past the 310-wide
  list, as here (`docs/mechanics/technologyscreen.md`, section 7; `interface.md`, section 2). The rows are the
  original's since 2026-11-13 - the tooltip's text at its level, laid out and cut as it cuts it - and since 2026-11-14
  a filled row is 180 wide and its text's height, so the rows stand 14 apart. Left: where the list's scrollbar stands,
  and so whether a long line runs under it or over it (`interface.md`, *What is not established*, item 11).
- ~~**A misspelt modifier is not reported.** The modifier kinds are not a closed list here,
  so any key a block does not own is taken as one.~~ Since 2026-11-14 the kinds are the original's closed list -
  its 107 keys, the 32 missions by their orders' keys and a `suseptibility_` for each faction, 142 in Their Finest
  Hour - and a key outside it is reported and dropped in every file of modifiers, buildings included, where the
  original logs it, or drops it in silence (`docs/mechanics/modifiers.md`, section 2). The base game, Their Finest
  Hour and BlackICE use none. On the way, a mission's efficiency modifier is read by the mission's own key, as files
  write it; it had been read as `<mission>_eff`, which no file can set (`navalmissions.md`, section 10).
- ~~**A country's modifier is not yet all of the original's eighteen sources.**~~ Since 2026-11-14 it is every one, a
  puppet's master's faction's influence and a government in exile's the last, and its laws come among the sources in
  `laws.txt`'s order of groups as the original's, where they had come in the order the history gave them - so the
  top bar's manpower tooltip lists War Economy before Massive Education Investment. The one step not built is a
  modifier of the country's own that nothing in the original fills (`docs/mechanics/modifiers.md`, sections 4 and 7).
- ~~**A province's modifier is every step of the original's but radar's list, and no port is held blockaded.**~~ Since
  2026-11-14 it is every step: radar's list kept as the original keeps it, by a province's rebuild and a change of hands
  with consequences, and taken off as well as put on (`intelligence.md`, section 4, corrected); and a port blockaded as
  its controller is set, with `is_blockaded`, `blockade` and a country's share of home ports cut off
  (`docs/mechanics/modifiers.md`, section 3, steps 8 and 13). `blockaded` is empty in Their Finest Hour and BlackICE.
- **Of the rebels, what is left out** (2026-10-15, `rebels.md`, *Ours*): a resilient
  faction's weighing of the odds, which asks a unit's power, no type of either install being resilient; what the
  original does with a rebel army of under 0.1 strength, a routine not read; a brigade's home province; and the
  province's history, of which only the last day the rebels took it is kept.
- **An underground is ordered as any building is** since 2026-10-15 (`buildinglines.md`); `--underground=` and the
  trial finish one at once. **The AI does nothing with undergrounds**: its production minister builds them from a Lua
  method not read, and its C++ manager asks a unit's power and an unread test of an area (`undergrounds.md`, *Not
  established*).
- **Each spec ends with what is not established.** Those are questions for the reverse
  engineering. Production's, the oil conversions', leadership's, the starting
  stockpile's, research's and manpower's were answered by reading the executable; those
  readings are `reversing/findings/FINDINGS-distribute.md`,
  `reversing/findings/FINDINGS-leadership.md`,
  `reversing/findings/FINDINGS-researchshare.md` and
  `reversing/findings/FINDINGS-manpowergain.md`, and are entered in the fact base's record.
  So was what a country's modifier is made of, the question five of the specs shared:
  `reversing/findings/FINDINGS-countrymodifier.md`. The two screens' were read before
  they were built: `reversing/findings/FINDINGS-countryviews.md`. And what units draw
  each day was read for its spec: `reversing/findings/FINDINGS-unitdaily.md`; and the
  officer ratio and the return of organisation for theirs:
  `reversing/findings/FINDINGS-organisation.md`; and the build queue for its:
  `reversing/findings/FINDINGS-buildqueue.md`; and the counters and the deploy menu for
  theirs: `reversing/findings/FINDINGS-counters.md` and
  `reversing/findings/FINDINGS-deploymenu.md`.
  The rest have not been filed in its work queue.
- ~~**Bookmarked starts are planned, not built**~~ - **built 2026-11-15** (`docs/mechanics/bookmarks.md`, read in
  `reversing/findings/FINDINGS-bookmarks.md` and `FINDINGS-laterstart.md`): the lobby lists every historical bookmark
  and a click makes the world at its date, the Quick Start panel's four buttons start their games, and the start
  checks of `OpenHOI3.SaveCheck` make their game at a start save's date. What a later start does not yet give as the
  reference saves of its first hour have it is `bookmarks.md`, section 3.1 - after a second round the same day,
  which brought alignment to 101 of 101 at every date and built an exile's day: New Zealand's posts at 1941 (the AI's
  hour-0 pass, not built), Japan's and China's fronts at 1939 (areas kept province by province), the order of a
  faction's members, and 2.010 supplies a country holding its own seat misses by.

A replacement for the engine still leaves a few things of the original's out. These are settled
decisions rather than things merely unbuilt, so nothing should be designed around them
coming back later. **Every place where a file that is valid in the original is
deliberately treated differently here is listed in `docs/compatibility.md`**, with who
decided it, what a mod loses by it and what would undo it: these two, and the ones
about the interface and its text.

- **Custom game starts.** No screen for assembling a game from arbitrary settings
  before it begins.
- ~~**Bookmarked starts.**~~ Taken out of this list on 2026-11-15 by decision, and built the same day
  (`docs/mechanics/bookmarks.md`).
- **Isolated scenarios.** The original ships nine self-contained battles - Fall Blau,
  the Bulge, Operation Downfall and so on - each with its own dates, map extent and
  victory conditions, living apart from the campaign. There are none, and none are planned (decided on
  2026-11-15: "Scenarios should still be considered out of scope").

Together these leave the campaign as the one way into a game, from any of its bookmarked dates. A campaign
start is one set of starting conditions to balance and one code path into the simulation; a scenario, with its own
map extent and victory conditions, would be a second simulation to keep.

It also means nothing here reads `scenarios/`, nor a bookmark of `common/bookmarks.txt` marked `is_cgm`
(`docs/mechanics/bookmarks.md`; `docs/compatibility.md`, entry 17).

## Where things stand

Early. What exists is the reading of an install, the beginnings of a data model, the
first three screens, the four windows a unit is ordered from, and the units on the map
with the menu that puts one there and the orders that send one somewhere - by land, by
sea and by air - in five projects:

| Area | What it does |
| --- | --- |
| `lib/OpenHOI3.Sim` | The game state - dates, countries, provinces, terrain - and what drives it: fixed-point numbers, the command queue, and the hourly tick with its daily, monthly and yearly passes, and the first systems in them: each province's modifier and each country's, from its laws, ministers, provinces and the strategic resources it has the use of; its IC, what it produces at home, the stockpile that goes into, the six shares its IC is divided between, with the money, supplies, dissent and resource bill that come of them, the conversions of energy into crude oil and crude oil into fuel, its manpower, its leadership with the officers, influence and spies its shares make, and its research with the monthly decay of its theories and practicals. And the units: the kinds of brigade, ship and wing, each one's own technology levels and figures, the chain of command they sit in, what each holds as a game starts, and what a day does to one: the supplies and fuel it draws, the men who go home, and the reinforcement and upgrades its country's IC pays for. And what an hour does: the officer ratio is worked out, and each brigade's organisation is held under it and comes back towards it. And the build queue: ordering a unit, what it costs and takes, the day's work the production share pays for, the unit that comes out, where it may be put and which unit in the field it may join. And movement: the route, the hourly march, where each kind of unit may go, the orders that send a fleet or an air unit and change its base, its range, and the distances by sea. And land battles: what starts one, its front and its reserves, the hourly round, the damage and what it costs a country, falling back, shattering, the province that changes hands and the delay an attack leaves, on the game's one generator of chance, with the tactic each side picks by weight and what the two tactics make of the battle. And a fleet's missions, battles at sea, an air unit's two fighting missions, what a country knows of a province, and battles in the air. And leaders: who is in service, which unit each may command, the choice of one for a unit, and how far a command reaches, with what it does to organisation and to what a unit uses. And events and decisions as they are read - an event with its options, a decision, a mean time to happen, a trigger and an effect - none of which does anything yet. References nothing, so the simulation runs headless |
| `lib/OpenHOI3.Content` | Reads the Clausewitz script format, finds and layers a Hearts of Iron 3 install with the mods selected over it as the original lays them (`docs/mechanics/mods.md`), and reads its localisation tables, its map with its continents, each province's points and terrain and the edges between provinces with their kinds and distances, where its buildings stand, which provinces have a port and onto which sea, the two kinds of unit counter, its defines, its buildings, laws, ministers, technologies and strategic resources, its province, country and war history, its unit types, historical models and orders of battle, its leaders and their traits, its combat tactics, its interface files, and its events and decisions with every trigger and effect in them, by the original's grammar, each problem in them reported. No engine in it either |
| `lib/OpenHOI3.Interface` | What the screens show, still with no engine: the text of every figure of the top bar, the production screen with the lines of its build queue, the technology screen, and the four windows a unit is ordered from - their columns, a design's totals, the order panel - in the original's format and colour, the colour escapes in text, how a set of sliders shares out what one of them gives up, and where an element goes on a canvas that scales. And of the map: which units have a counter and how they stack, what a counter says and where its parts go, and the painting of one out of its plate, its mask, its unit's picture and its size mark; the buildings that show on the map and which unit a click on a port or an airfield picks; the rows of the window of orders; which arrow a unit's way is drawn with; and what a battle shows - its name, the attacker's share of the two sides' worth, the marker's colour and number, the labels of each side and the icons of what is in play |
| `lib/OpenHOI3.Lua` | The install's Lua, on the original's own interpreter - Lua 5.1.4 built from its source in `native/lua` - as the original runs it: every standard library, a searcher over the install's layers, `autoexec.lua`, the base game's whole; the classes the original hands its scripts, each a global, as far as a new game's relations and the AI's answers to offers that take only tags need them, with `CFixedPoint` and the iterators; the call that gives those relations, and the AI's answer to an offer. A C# function never raises a Lua error itself, and Lua runs only in a protected call |
| `src/Map` | The map, political or by terrain: drawn by a shader from a province-id texture and two colours per province - its owner's, and its holder's striped over that where they differ - with pan, zoom and picking. And the units on it, as counters: a stack to a province, the near counter or the far one by the zoom, a click or a drag box to select them, a right click to send them somewhere and the way it is going drawn; and the buildings, the port and the airfield among them, with a click that picks a unit in one, and each country's capital marker at its acting capital's city point; and the markers of the battles the player has a part in, with a click that opens one |
| `src/Gfx` | The Godot client's side: decodes the original's DDS/TGA textures and sprite declarations, and finds the typeface for each font the interface files name |
| `src/Gui` | The interface runtime: builds a window from the original's `.gui` declaration - its icons, buttons, checkboxes, text boxes, scrollbars, list boxes and boxes of overlapping elements, in the file's order and at the file's positions - and finds them again by name |
| `src/Screens` | The screens, one class each. The top bar: the date, the country, its resources, IC, manpower and the rest, and the buttons that stop, slow and speed time. The production screen: the stockpiles with what the day did to each, the six sliders that divide IC, and the build queue with buttons that move a line and take it out. The four windows it opens to order a unit: the division designer with its brigade picker, and the ship, air and brigade builders. The technology screen: the four sliders that divide leadership, the tree a folder at a time, the theories and practicals, the research queue and a panel for one technology. The alert that says units are waiting, and the deploy menu it opens: the units waiting, the units one of them may join, and a click on the map to place it. The panel of several selected units, a row each (`CMultiUnitPanel`), and the panel of the unit selected on the map: a list of the units above it, its header, its status, its order, its buttons, its brigades, the units under it and the battles it is in, each a class of its own. The window of orders for a fleet or an air unit. And the window of a battle, with its two sides and a line for each unit on them |
| `tools/OpenHOI3.SaveCheck` | Compares what the libraries compute with a savegame the original wrote, field by field; and, given two saves of one game, what a day of research adds with how far each technology moved between them. It also sets every starting brigade beside the save's, by number, and runs the save's two days of rotation, reinforcement and upgrades to set each beside it again, its organisation and its build queue included; and the queue again between two saves, run forward from the earlier and set beside the later. And the edges between provinces against the original's own cache of them, and what an hour adds to a marching division against two saves hours apart, with where the hours between took every fleet and air unit. And the distances by sea against the original's own cache of those. And the supply network against the provinces a save marks as out of supply and as beside a capital, and the carrying of supplies against what a start-date save holds in every province, with the convoys a new game makes against the save's, way for way and ship for ship. The oracle for everything above - but for a battle, which no save has: `battle` fights one with the install's own units and says how it went, as a trial, `seabattle` does the same with two countries' fleets, and `raid` sends one country's raiders at another's convoys for a month. And `firstdays` sets a new game as a player gets it, its two days run, beside the start save country by country - the save's since 2026-11-14 in all but what the AI's shares decide - where `supplypass ... countries` does it like for like, with the save's laws given. `techgap` looks at the technology levels the start save has above the history's, and `airbattle` flies an air mission with the install's own units and tells the battles it comes to; `bombing` flies a bombing mission for some days and tells what it did to the province, `landing` puts a division aboard a transport fleet and lands it on an enemy's coast, and `airborne` drops paratroopers, rebases a carrier's air group or flies an air supply. `script` reads an install's events and decisions and lists every problem in them, with no save needed, and `modsurvey` lays a mod over the install and lists everything it asks that this game does not answer |
| `tests/` | 2306 tests over the parsing, the layering, the map, the numbers, the tick, modifiers and strategic resources, IC, resources, the stockpile, production, the conversions, leadership, research, manpower, unit types, historical models, orders of battle and what a brigade starts with, what units draw each day, the officer ratio and organisation, the build queue, what a country may build and what a new unit is called, the interface files, font descriptors, the sliders, the three screens' figures and the builder windows', the counters and who may join whom, the edges between provinces and a unit's march, what the weather does to it, the unit panel's figures, ports, the distances by sea, a fleet's and an air unit's orders and range, the buildings on the map and the window of orders, a land battle from its first hour to the province taken, what the hour makes of a unit in one, day and night, the tactics and their file, what a battle shows, the supply network, the carrying of supplies and what a unit draws and brings, convoys - the heap, the way, the harbours, a sailing and the sharing out of ships - the supply map mode's colours and lines, who is whose puppet, a battle at sea from the sighting to the fleet that falls back, a fleet's missions with the raid on a convoy, an air unit's two missions with the battle in the air, the eight bombing missions with the bombing, what a bomb does to a province and the day's repair, what the window shows of a battle in the air and of a bombing, a mission's area with the air window's ladder, sliders, hours, dates and colours, the list of finished battles with the air map mode's colours, icons, rings and lines, and leaders - their files, their skill, who may command what, the choice of one, how far a command reaches, what a leader does in a battle on land, at sea and in the air, the experience and traits a battle gives, and the window a leader is chosen in with the four commands it posts and the original runtime's sort, and how a tooltip is shown with what a province's, the top bar's, the production and technology screens', the sliders', the builder windows', the unit panel's, the deploy menu's, the orders windows', a battle's and the leader window's say - the designer's terrain summary, the practical's discount, a brigade's summary, a leader's own and what a trait does among them - a modifier's sources written out and the counter in text - and the reading of event and decision script, its words, its triggers and effects by the original's grammar, an event, a decision and the problems found, and what a condition answers - the scope and its forms, the blocks and the sets, the leaves and what is reported unbuilt - and what an effect does, with flags, variables and the modifiers an event gives, and an event firing and a decision taken, with their text, and what countries hold of each other - a new game's relations, the month's drift, a battle's gain, neutrality and its effect and conditions, threat's two spreaders, the most-threatening country and the alignment triangle's clamp, step and drift - and Lua: the interpreter's rounding and numbers, errors and objects crossing both ways, the install's scripts found through the layers and the relation they give, a class's global, its fields, one equality for every class, iterators, `CFixedPoint` and its backwards arithmetic, the API's answers and an offer put to the recipient's AI - and the diplomacy screen: its lists and sorts, its panel and status lines, the relation list, the ceiling for war, every tooltip and the drift tooltip - and the agreements: an offer, the AI's answer, each pact's arms and how it ends, the screen's rows, tooltips and request dialog, and a pact's message - and trade: a route's price, terms, convoy, day and ends, the AI's trading, the trade window and list, automate-trade and an offer put to the player - and aid: debt and its repayment, shared theories, lend-lease's rules and IC, licences and their Lua - and politics: the parties, the cabinet, elections, national unity, a law or a minister changed, the politics screen's text and tooltips, and the AI's politics - a new game's laws to their fixed point, the minister's hour, the refill of empty posts and what the politics script is handed - and spies, their placing, their missions, catching them, the coup and covert operations, and what a country knows of another and of every province, the intelligence screen and the AI's spying - and triggered modifiers, their file, the day's test, the list's order, their messages and the Strategic Effects window's rows and tally - and occupation, the policies, what sets one, a province's whole modifier and when it is made again, nationalism and the occupied countries on the politics screen - and revolt risk, suppression, the day's step, the figure read, its conditions, effect, tooltip and map colours - and rebels, the types and their reading, the roll, the type chosen, a rising and its army, the capture, a faction's day and month, independence and the release, and what the player is told - and undergrounds, a finished one's strength by its air base, where one may go, its day, partisans, the spread, Organized Revolt and its tooltip, the dissolution and the map's red - and building lines, their price, an order joining a line, a finished level not working, a building waiting and where it may go, the lost line cancelled, the dialog's words and the placing map - and the province view, its columns and keys, pips, figures and their gates, weather strip, ways, underground window and tooltips, and allied objectives - and convoy lines, their price set once, an item of ten ships, the dialog's time and the queue line - and weather: the season, a province's hour against the original's, the arc, a front's step and spread, the high pressure, a front's birth, the bearing, the climate index and the zones, and the weather map's text and colours - and the player's settings.txt - and transport: a fleet's room, boarding by command and by marching, getting off, too much aboard, a transport's damage shared out, the landing's march, wait, scales and penalty, the two missions and the warning, and the screen's buttons, list, counters, arrow and lines - and the carrier and carrying by air: a carrier's room, the rebase to carrier and carrier protection, a group following its fleet and let go when it sinks, boarding a plane, the paradrop with its delay, supplies, penalty, width and settling, the air supply, and the plane buttons, the air window's rows and their order lines - and selection: one kind and the player's own, Ctrl's whole command, the drag and the box's three walks, the selection groups, the multi-unit panel's line, checkboxes, rows and carrying, and the orders window's rows for several fleets - and the tech and production ministers, their hours, the AI's commands and their rounding, the sliders' priority, desperation, the roles and the placing of a finished unit - and the foreign minister, its hour, the decisions pass and its answer, the prepared wars, their pruning and walk - and the AI's strategy: a list's add, cut and scaling, the personality, the threat, antagonism, rival and protection terms, the walk of the victory conditions with its region, when a strategy is made and the prepared wars it checks, and its weights in Lua - and fronts and theatres: an area's fronts and a country's with their heat, the front-worthy table, a theatre of a front's own and the rest placed, a merge, the old theatre kept by overlap, a front's chain, a headquarters adopted, made and disbanded, readiness for war, names per country and what marks theatres - and the unit agents: an agent made with its theatre and bound by the pass, the sort into theatres, commands made a level at a time and filled five to a command, a new command's brigade and name, fleets and air groups split and merged, the stances, the area, the objectives, a unit's power and the aircraft wanted, and moving and attacking - committing, the position cap, the scores, the orders and the odds, and the land orders window, the reorganise window, the merge and the split - where silent misreadings are expensive |

Not built yet: the cached asset build, every screen but those, and nearly every
system the tick is there to run. The game also does not stop at the end date.

In the game: the mouse wheel zooms, a middle or right drag pans, the arrow keys scroll, a
click selects a province - or, on the top counter of a stack, its unit, and clicked again
the next unit under it - a right click sends the selected unit to the province under the
pointer, with shift after whatever it was already told, and on its own province halts it,
and the buttons by the minimap choose the map mode (`minimap.md`). Sent at a province an enemy
stands in, a land unit fights for it from where it is: its arrow is red, its panel says so
and ends in a row for the battle, from the battle's fourth hour a round marker with a
number stands between the two sides, and a click on the row or the marker brings up the
battle's window, whose cross closes it and whose button at the upper left calls the
player's units off. A fleet's battle at sea, an air unit's in the air and a bombing have
the row and the window and no marker. A selected
province has a readout at the left - a second click on it puts the readout away - which
says who is attacking whom there and for how long, and a province that changes
hands takes the colour of whoever now holds it, with thin stripes of its owner's. A click on a port's or an
airfield's badge selects a fleet lying there or an air unit based there, and again the
next; a right click with one of those selected brings up the orders for that province -
tick one and accept - and with shift sends a fleet straight there. A selected unit's panel comes up
at the left: a click on a unit above it or under it selects that one, the cross beside its
order halts a marching land unit and takes any other unit's order back - an air unit then
flies home once its battle or bombing is over - the button on its province's name takes the map there, and the three
boxes switch its priority, its reinforcement and its upgrades. A click on its leader's
name or portrait opens the window a leader is chosen in: a click on a row gives the unit that
leader, the arrows on a row or at the head promote and demote, the column headings sort and
sort back, Filters opens a list of traits to tick - a right click on one shows it alone - and
the buttons at the foot leave the unit with nobody, take every leader off after asking, and
turn the country's own choosing of leaders off. While it is selected lines
on the map join it to its commander and the units under it to theirs, and H switches them
off and on. When a unit is finished an icon comes up under the top
bar; a click on it opens the list of units waiting, a click on one of those chooses it,
and then either a click on a green province places it there or a click on a line of the
list puts it with that unit. Space or
Pause stops and starts time, and plus and minus change its speed; so do the top bar's own
buttons, and clicking its date. The top bar's Production and Technology buttons open
those screens, and Escape or the cross closes one. On either, a slider is dragged, a
right click locks it, and the button under its title sets it to what it needs; on the
technology screen a click chooses a technology and a double click starts or stops its
research; on the production screen a line of the build queue has four arrows that move
it and a cross that takes it out, and the Division, Air Wing, Flotilla and Brigade
Attachment buttons each open the window that orders one: pick from the list, set how
many, and Start Production. In the division designer a click on a brigade adds it and the
cross on its line takes it out; in the ship builder the button beside a technology
chooses the level to build with; a held plus or minus repeats, and steps by ten with
Ctrl. A game starts paused, as Germany - `--country=ENG` after `--` on the command line
plays another, `--order=infantry_brigade,artillery_brigade` orders a unit of those
outright, `--all-units` gives the country played every unit type to order whatever
technology or country its file keeps it to (*Play OpenHOI3 (all units)* under `F5` starts
that way, and *Play OpenHOI3 (choose country)* asks for both), `--war=GER:POL` puts two countries at war - there is no other way to start one
yet - `--builder=ship:destroyer` opens a window with that picked, `--days=8` runs
that many days at once - long enough for the ship Germany starts with six days from done -
`--screen=deploy` opens the deploy menu, and
`--click=563,349;r700,573;h48` clicks the mouse at those points of the canvas in turn -
with `r` the right button, with `s` shift held; `h48` lets 48 hours go by, `kh`
presses the H key, `ck1` Ctrl and 1, and `d560,140>760,340` drags - `sd` letting go with shift held, `D` holding on. **A
screen is checked with `--click`, not only with a switch that reaches past its buttons**:
the four builder windows passed every check that opened and filled them by name while
seven of their buttons could not be pressed.

The top bar's **Theatres** and **Statistics** buttons work since 2026-11-08 (`docs/mechanics/theatresscreen.md`, `statistics.md`): the theatres, their fronts, what a plan is short of and a Build that orders it, and the 26 pages of tables and pies. Tried with `--click`; they have not been reviewed in play. A theatre's and a front's needs are worked out as the screen is filled, where the original refills an inactive plan daily. Of the ledger, the tooltips of its columns, a unit's order and model, points off the map and four of the goods pools are not built (`statistics.md`, sections 2 and 3). **The Strategic Warfare Manager** works since the same day (`docs/mechanics/strategicwarfare.md`): the records of each war, every tally but attrition - which is not built - the window and its charts through the install's `linechart.fx`, saved and loaded byte for byte. It has not been reviewed in play. **The convoy windows and the go-to box** work since the same day (`docs/mechanics/convoywindows.md`): the production screen's list, making a convoy from two lists of ports, each convoy's ships and goods, the three automatic flags, and the box that finds a province. Tried with `--click`; they have not been reviewed in play. **The order of battle browser** opens from the unit panel since the same day (`docs/mechanics/oobbrowser.md`): the player's whole tree in one list or two, Theatre Forces, and a unit, a brigade from the panel or a leader dragged onto a row; and **a brigade's card** (`docs/mechanics/brigadecard.md`), with a ship's sunk list and the pride of the fleet. Tried with `--click`, and they were reviewed in play and found good. **Attaching, detaching and making a headquarters** from the unit panel and the multi-unit panel work since the same day (`docs/mechanics/attaching.md`), and they were reviewed in play and found good. **Battle plans** work since the same day (`docs/mechanics/battleplans.md`): the window, its three lists, importing the install's historical plans, saving to files and in the saved game, and the tools that draw arrows, borders, icons and text through the install's effect files. Tried with `--click`, and they were reviewed in play and found good. Since 2026-11-13 the arrows draw through the effect's stencil too, a colour at a time, so two of one colour cross without the crossing darkening (section 11). The diplomacy panel's war goals and influences, the battle-start and election popups, the single and multi-unit Disband, the production screen's theatre needs, the air builder's description, every message's Goto (`docs/mechanics/messages.md`, section 4.1) the picture, label and description of what a builder window has picked and changing a brigade's type (`docs/mechanics/upgrades.md`) since the same day; tried with `--click`, and they have not been reviewed in play.

An earlier iteration reproduced the original's front end directly from its `.gui` files
and ran its main menu and lobby. That work left the tree in commit `72b0eb6` and remains
in git history. It is where the lessons below come from, and today's `src/Gui` started
from its. The lobby it drove was said not to be coming back until 2026-11-06, when today's
`src/FrontEnd` built it anew from a reading.
