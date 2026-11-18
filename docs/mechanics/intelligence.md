# What a country knows

Two things, which the original keeps apart: **how well a country knows another country**, which is its spies there
and nothing else; and **how well it knows each province**, a level from 0 to 9 for every country in every province,
rebuilt once a day and raised every hour where units have moved. Read out of the original in the espionage wave
(`reversing/findings/FINDINGS-espionage.md`, section C), on what the air and naval readings had found of it
(`reversing/findings/FINDINGS-airbattle.md` section 7, `reversing/findings/FINDINGS-navaldetection.md`). The second
piece of espionage (`espionage.md`), built after the spies themselves.

Figures are thousandths unless said otherwise; a level is a whole number.

## 1. How well a country knows another

Nothing keeps it. It is worked out when asked, from the asking country's presence in the other (`espionage.md`,
section 1):

    F = level x 1000 / MAX_SPY_LEVEL (10000)  -  the other's counter_intelligence

One spy is 0.1, ten spies 1.0. **There is no other term**: neighbours, an alliance, a faction, a master and its
puppet, a war - none of them counts. **And no floor**: counter-intelligence above the spies' share makes F negative.
Their Finest Hour's counter-intelligence comes from the civil laws - `open_society` -0.25, `limited_restrictions`
-0.10, `repression` +0.1, `totalitarian_system` +0.25 - and the `efficient_sociopath` minister, +0.1.

What reads it:

- **The AI's `CAIIntel(us, them)`** (`lua.md`, section 7): its factor is F over 1000 as a float; its uncertainty
  `clamp(1 - F + 0.2, 0.2, 1)`; and it has no intelligence where the presence holds less than one whole spy.
- **The intelligence screen** (piece 3), which hides behind F with a floor of 0.1 where the AI's is 0.2.
- Nothing else: the diplomacy screen shows its figures plainly.

`offmap_land_intel` and `offmap_naval_intel` are military espionage's (`espionage.md`, section 7) and the AI's
estimate of strength, not F. **`peace_offmap_intel` and `offmap_political_intel` are read by nothing** found, so
three minister types do nothing by them.

## 2. How well a country knows a province

One level per country, 0 to 9. **The savegame keeps none of it**: it is worked out afresh as a game is entered. The
thresholds its readers ask:

| level | what it gives |
| --- | --- |
| 2 or more | a province where air missions pick targets (`aircombat.md`, section 6), and where `CAIIntel` counts a unit as seen |
| 6, 7, 8 | each doubles a fleet's chance of sighting another there, and 2 or more gives a quarter more (`navalcombat.md`, section 1) |
| 9 | a sighting with no roll |

The original's map draws a unit by it too - hidden under 2, a picture of the unknown under 3, its name `???` under
5, its figures `?` under 7 - and shows a province's buildings from 6; **nothing draws a fog of war here yet**.
The map modes ask the player's level of each province for their colours and their tooltip lines - the supply mode a
half brightness under 2, grey under 6, a flat green at 6 and 7, its figures from 8, and in its tooltip the roads and
the depot above 5 and the figures above 8 (`supplymap.md`); the others in `mapmodes.md`, `airmap.md` and `weather.md`.

**A country's list**, which shares its 9s and its 3s: every country it holds an alliance with, its master and its
puppets, and the members of its real faction. Worked out where it is asked, as it stands.

## 3. The day's rebuild

Once a day, late in the day (after the provinces' own day and the events, before the victory conditions), and as a
game is entered. Every province is a real one here, so the original's test that a province takes part in the game
always holds.

1. **Every level to nought** - or, in the original, **to 9 with the fog of war off**, its console's `debug fow` [exe]: every
   country then knows every province at 9, the AI's included, nothing below lowers it, the player's country is given 9 on
   every province once more at the end, and a sea battle starts without the roll to find the enemy (`navalcombat.md`).
   **Here the fog of war off is the countries played's alone** - they know every province at 9, given at the rebuild's end,
   and their fleets find an enemy's without the roll - and the AI's countries know what they would with it on: the
   maintainer's word, 2026-11-08, "make it not affect the AI" (`docs/compatibility.md`, entry 21). Built since 2026-11-08
   (`CGameState.FogOfWar`, `CFogOfWarCommand`), a switch of the debug menu (Shift and 4) that posts the command. *Ours*
   besides: what everyone knows is made again the moment it is turned, where the original's flag waits for the day's
   rebuild; and it is not saved, as the original's is not.
2. **Undergrounds**: a country's underground provinces give it and its list 9. *Not built*: there are no undergrounds.
3. **Neighbours.** For every land province P with a controller, over its edges, each land neighbour N:
   - N is known **at least 3** by P's controller and each country on its list;
   - for each unit in P whose owner is not P's controller - and is not the owner of the unit before it in P's list of
     units - N is known by that owner at least `min(2 + s, 9)`, s from the owner's `decryption` against N's
     controller's `encryption`: 0 where it is below, 1 where equal, and where above, 2 - or, where `d x 10000 /
     1000` is above 1000 (d the difference in thousandths), `d x 10000 / 1000 / 1000 + 1`. So a lead of 0.1 or less
     gives 4 and each 0.1 more one more.
4. **The province itself**, for every province P:
   - **9** for its controller and each country on the controller's list;
   - 9 for its underground's owner and its list (*not built*);
   - each unit's **intelligence grants** (`espionage.md`, section 7) with days left give their country at least the
     grant's level, held to 9;
   - each unit whose owner is not P's controller gives its owner **9 on land**, and at sea 9 where a battle at sea is
     being fought in P or the unit is a patrol shadowing somebody, **else at least 2**;
   - **radar** (section 4).

Every "at least" raises and never lowers; nothing in the rebuild lowers a level but its first step.

## 4. Radar

For every province P, and every country C that exists and has `listening_station` from a technology:

    s = -1 where C's decryption is below P's controller's encryption, 0 where equal,
        else max(1, d x 10000 / 1000 / 1000)                     d the difference in thousandths
    m = C's land_intel_boost / 1000 over land, its naval_intel_boost / 1000 at sea, truncated
    near = clamp(s + 2 + m, 0, 8)

Where C knows P at near or more already it is skipped. Otherwise for each of C's **radar provinces** with a
`radar_level` above nought:

    reach = radar_level x (RADAR_RANGE (0.18) x visibility(P) / 1000) / 1000
    R     = 250000 x reach / 1000 / 1000                         truncated twice
    P is seen where 16 x (dx^2 + dy^2) < R^2                     the centres, dx the short way round the map

and P is then known at `near` by C and each country on C's list. **The original works out a far ring as well, at
`s + 1 + m`, against a radius of 350000 - which is never reached, since every province inside it is already inside
the near ring**; it is not built.

`visibility(P)` is 1000 where P has no controller or no unit; otherwise `1000 + r x t / 1000 x BASE_RADIO_STRENGTH
(10) / 1000`, with r the largest `radio_strength` of the first brigade of each unit in P, never below 1000, and t the
controller's `radio_strength` technology `x 1000 / 100000`, never below 100. No technology of Their Finest Hour's
gives `radio_strength`, so **ground with troops on it is seen from twice as far**: a level-1 station sees 90 km of it
and 45 km of empty ground.

**A country's radar provinces** are a list kept by two things (`modifiers.md`, section 3, step 13; read whole
2026-11-14, `reversing/findings/FINDINGS-modifiersources.md`, section 3, which corrects this section):

- **a province's rebuild**, for a province with an owner whose `radar_level` is not the level last applied: put on
  its controller's list where the last was below 1 and the new is above 0, else taken off where the last was above 0
  and the new is below 1; the new level then kept as the last applied;
- **a change of controller made with consequences**: put on the new controller's list where its `radar_level` is not
  nought, taken off the old one's whatever it is, the level kept as the last applied.

A province is on a list once. **A plain change of controller moves nothing**, so land handed back by the day's return
or taken by an effect goes on seeing for its old holder until its level next moves; and a radar bombed from 1.5 to
0.5 comes off the list while one built up from nought to 0.5 is on it. Neither the lists nor the levels applied are
saved: a loaded game puts every owned province with radar on its controller's list as it is entered. This section said
until 2026-11-14 that a province was never taken off; the original takes it off as above.

## 5. The hour's raise

At the end of every hour, after everything else in it: each province **asked for** in the hour is raised - nothing
lowered - as the day does it, without the radar:

- 9 for its controller and its list;
- each land neighbour at least 3 for the controller and its list;
- each unit's grants, and its owner, where not the controller, 9 on land and at sea 9 or at least 2, as in section 3;
- then for each such unit **every land neighbour** - here with no test that it takes part - at least 2, 3 or 4 as
  the owner's decryption is below, equal to or above the neighbour's controller's encryption, **with none of the
  day's scaling**;
- its underground's owner and its list 9 (*not built*).

**What asks for a province**: a unit leaving it or entering it, and `local_intel_boost` (section 6). **What asks for
the whole rebuild instead**, that hour: a province changing owner or controller, an alliance made or ended, a faction
joined or left, military access, a NAP, an expeditionary force, a radar province added or taken off, and a
province's rebuild that finds its `radar_level` moved. Every request lasts the hour.

So **a fleet that has sailed into a sea province knows it at 2 by the end of the hour**, and land at 9 - and a fleet
that sails into a sea province at war knows nothing of the coast beside it at the day's turn, where the hour had given
it 2 to 4: the daily rebuild's neighbours are land's alone.

## 6. `local_intel_boost`

`local_intel_boost = N`, an effect in a province's scope with a FROM country: for every province the scope
province's **controller** controls whose centre is within N of the scope province's - `16 x (dx^2 + dy^2) <= N x N`,
N whole and in the game's kilometres - every unit there, of anybody's, gives FROM a grant of **8** for
`SPY_MILITARY_INTEL_LOCAL_TIME_BASE` (15) of its days, and that province is asked for (section 5). Nothing without a
province or without FROM. Their Finest Hour's one user is the covert operation `steal_battle_plans`
(`espionage.md`, section 11).

## What looks wrong and is built so

The original's, kept, none put to the maintainer:

- F has no floor, and nothing but spies feeds it;
- the far radar ring is never reached;
- a country's own units on its own ground give it nothing beyond the controller's 3 on the land beside;
- the hour gives a fleet at sea 2 to 4 on the coast beside it, which the day's rebuild takes away;
- the hour's step from decryption is unscaled, at most 4, where the day's goes up to 9;
- a radar province lost by a plain change of controller stays on the old holder's list, and one whose level falls
  from above a whole level to below it comes off while one that rises to the same level from nought is on;
- the AI's uncertainty floor is 0.2 and the screen's 0.1;
- `peace_offmap_intel` and `offmap_political_intel` do nothing.

## Ours

- **What asks for a province is found at the hour's end**, from each unit's province against where it stood at the
  last one, and what asks for the whole rebuild from the count of provinces that changed hands and the pacts as they
  stand against the last hour's - where the original files each request where it happens. Every raise is a "9" or an
  "at least", so the order they come in changes nothing.
- **A request for the whole from radar** - a radar province added or taken off, a province's `radar_level` found
  moved in its rebuild - is a flag the hour's end reads (`CGameState.IntelWholeAsked`), cleared by every whole rebuild;
  the original's request is a mark at the head of the hour's list, freed at the hour's end. Since 2026-11-14; until
  then it was not done.
- **The original's map figure for the screen's country and its friends** (`stack_intel`), which only draws counters,
  is not kept: nothing draws a fog of war yet.

## Not established

- Whether a unit's grants count down once a day (`espionage.md`, *Not established*).
- The original's battle "of kind 1" at sea is taken to be a battle between fleets; the fact base took it from an
  earlier record and did not read it again.
