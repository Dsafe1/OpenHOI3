# The tech and production ministers

Stage 5's first piece: research and production for the countries nobody plays. Both ministers are the install's Lua -
Their Finest Hour's `script/ai_tech_minister.lua` (`TechMinister_Tick`) and `script/ai_production_minister.lua`
(`ProductionMinister_Tick`, `BalanceProductionSliders`, `BalanceLendLeaseSliders`), with `ai_support_functions.lua`,
`utils.lua` and the countries' files - so what this spec sets out is what the engine does around them: when it calls
them, for whom and with what, what each engine method they reach answers, and what the commands they post do. Read out
of the original in one wave, `reversing/findings/FINDINGS-ministers.md` sections A to D; the ministers' objects and their
hours are `reversing/findings/FINDINGS-ai.md`. The politics and intelligence ministers, which this follows, are
`politics.md` section 12 and `espionage.md` section 15; the bridge and the classes are `lua.md`.

**Only for the countries nobody plays.** The player's country has no minister here (`docs/compatibility.md`, entry 6):
the original makes the player's tech and production ministers only where he has handed the ministry over, and there is
no handing over here.

Figures are thousandths unless said otherwise. A **share** is the 15-bit kind (`CFixed15`, `numbers.md`).

## 1. The ministers and their hours

As a new game is made, after the intelligence minister and the foreign minister (`foreignminister.md`, section 1),
every country nobody plays is given a **production minister** and a **tech minister**, each first working `draw % 3 +
1` hours away, a draw each on the game's generator - in that order, the original making its five in the order foreign,
production, tech, politics, intelligence (`FINDINGS-ministers.md` A.1 and B.1; `FINDINGS-ai.md`; *ours*: after the
politics and intelligence ministers', whose draws were built first). From then on each works every hour, and does anything only in
**its hour of the day**: the tech minister at `(number + 3) mod 24`, the production minister at `(number + 5) mod 24`,
where the number is the country's place among the countries plus one (*ours*, as the politics minister's:
`politics.md`, section 12). A country that owns no land and is not in exile is passed over; nothing works while a game is
being made. The hourly step runs after the intelligence minister's.

**A third hour, the placing of finished units** (section 6), is `(number + 8) mod 24`, once a day, from the first hour.

## 2. The tech minister's day

In its hour, **`TechMinister_Tick(minister, true, true)`** - the minister handed over as `CAITechMinister`, and the two
arguments, "set the sliders" and "research", always true for a country nobody plays (the original's are the player's
`automate_tech_sliders` for his own). A Lua error is reported and ends the day's work. Their Finest Hour's script, every
day: works out the four leadership shares and posts them (section 3), and where the research slots that gives - the
research share of the total leadership, rounded up - exceed what is queued, picks technologies to fill them, a random
pick from what each of nine areas wants, and posts each.

**The original's monthly re-plan is not built**: two arrays it makes on the turn of each month are read by nothing in the
engine and only by two Lua getters no file of Their Finest Hour calls (`FINDINGS-ministers.md` A.2). `GetTechModifers` and
`GetFolderModifers` are reported unbuilt if a mod's script asks them.

**A finished level of a technology of several levels** (`research.md`, section 4: it goes back on at the end of the
queue where its next level may be researched): for a country with a tech minister, **`CStopResearchCommand`** is then
posted, which takes it off the queue again - so an AI country never keeps a technology of several levels queued past a
level, and its minister chooses again in its hour. Refused where the technology is no longer queued.

## 3. The research and leadership commands

**`CStartResearchCommand(tag, technology)`** puts the technology **at the end of the queue**, checking nothing but that
it is not queued already (`FINDINGS-ministers.md` A.4): not whether it may be researched, not the slots. The queue may run
past the slots, and a technology not allowed may sit in it getting nothing - the script asks `minister:CanResearch` first.
Each day's research shares its leadership out again as the queue stands (`research.md`, section 2), so a start counts from
the next day's research.

**`CChangeLeadershipCommand(tag, officers, diplomacy, espionage, research)`** - the script's NCO is the officers' share.
As the command is made, from the four numbers:

    x     = max(0, number)                         ; NaN is 0
    sum   = (float)(a + b + c + d)                 ; added in double
    q     = (float)(x / sum)
    share = trunc(q x 32768 + (q >= 0 ? 0.5 : -0.5))

**rounded half away from nought**, where the player's sliders truncate (`sliders.md`); and **written as given**, never
refused, never normalised again - four shares may sum a 32768th or two over the whole. England in the 20 April 1936 save
holds 6554, 2621, 983 and 22610: the script's 0.2, 0.08, 0.03 and 0.69 through this rounding exactly.

## 4. The production minister's day

In its hour, in order (`FINDINGS-ministers.md` B.1 and C.1):

1. **The counts by unit type** are made: for each type, the brigades of every military line in the queue, each times the
   line's Size (`CAI:GetProductionSubUnitCounts`), and every brigade of every unit the country has, and of every finished
   unit waiting to be placed (`GetDeployedSubUnitCounts`). Brigades, not units; nothing asks where a unit is or what it is
   doing. Nothing the script posts moves them until the next day.
2. **The desperation** is made again where the day of the month, counting the 1st as 0, is 0, 14 or 28 - the 1st, the
   15th and the 29th - or where it has never been made; otherwise the last is kept (section 4.1).
3. **`ProductionMinister_Tick(minister)`**, every day. A Lua error is reported and ends the day's work.

Their Finest Hour's script: with the production share's IC less what the whole queue asks a day available, convoys first
(where the country has a port), then - where it has spare manpower - units in the ratios of the country's file, by land,
air and sea and the rest of the IC to buildings, each a random pick; then, one day in seven by `GetAIRand`,
`PrioritizeBuildQueue` (section 4.2).

### 4.1 Desperation

`CCountry:CalcDesperation()`, 0 to 1100 (`FINDINGS-ministers.md` B.1). **Nought at peace.** At war:

    total = controlled = 0
    for each core province, in the country's order of cores:
        if its owner is the country and its area is the acting capital's or touches it:
            w = weight(province)
            total += w;  controlled += w where the country holds it
    total = max(total, 1)
    d = 1000 - controlled x 1000 x 1000 / (total x 1000)
    d += 100 where the country does not hold its own capital (the real one, not the acting)

    weight(p) = 1 for a province nobody owns, else
                w = p's manpower / 1000 + 1, plus FORT_LEVEL / 1000 where p is in its owner's acting
                capital's area or on its continent, else w / 5; at least 1

*Hardcoded*: the 100, the fifth, the area rule. *Mod-able*: what the provinces' manpower and forts are. The minister keeps
it as `d / 1000` in single precision; the engine reads that copy only in `PrioritizeBuildQueue`, and the scripts call
`CalcDesperation()` themselves.

### 4.2 `PrioritizeBuildQueue`

    for each line of the queue, head to tail:
        skip a line whose day is paid in full (its status 0.980 or more)
        a convoy line of transports: to the front where the country's transports x 2 are fewer than it needs
        a military line whose first brigade is land, while the minister's desperation is over 0.6:
            days = its status 0 ? -1 : (duration - progress) x 1000 / status
            to the front where days / 1000 < 120

Each move is a command putting the line at the front, posted in turn, so lines moved in one call end at the front in the
reverse of their order. Building lines and escort lines are never moved. *Hardcoded*: 0.980, 2, 0.6, 120.

## 5. The daily sliders

**At the end of every country's day**, for every country nobody plays that owns land and is not the rebels - the two
days a new game runs before its first hour included - **`BalanceProductionSliders(ai, country, 0, lendLease, consumer,
production, supply, reinforce, upgrade, hasReinforceBonus)`** (`FINDINGS-ministers.md` B.2): the level 0, the AI's; each
of the five figures **that share's need over the country's whole IC**, `max(0, (float)need / (float)TotalIC)` in single
precision, `production.md`'s needs; **the lend-lease figure always 0**, the lend-lease share having no need; and
`hasReinforceBonus` where the country's reinforcement bonus modifier is above 0. Where the country gives lend-lease to
anyone, **`BalanceLendLeaseSliders(ai, country, tags, values)`** then follows. A Lua error is reported and skips the rest
of that country's day, the lend-lease call with it. What they post counts from the next day's shares.

`confirmed` **The supply need is handed over in its 15 binary places, not cut to thousandths** [exe]: the setting's need
is a 48.15 number taken as it stands (`FINDINGS-ministers.md` B.2; `upkeep.md`, section 8), and so is what a script's
`GetNeeded()` answers for the supply setting. Until 2026-11-19 ours cut it to thousandths first, which lost up to a
thousandth of IC - half a per cent of a two-IC country's supply share. *Measured* with `firstdays` against the start
saves: the AI's written `distribution` is the save's for 60 of 78 countries in Their Finest Hour (36 before) and 52 of 80 in
BlackICE (32 before); given the save's mud and frost in place of a new game's own weather, 73 of 78 and 67 of 80. So what
is left is mostly **a new game's weather**, which is drawn afresh and puts its mud elsewhere than the original's did:
mud raises the loss in the carriage the supply need carries (`supply.md`, section 11), and BlackICE's
`MUDDYNESSSUPPLYTAXMODIFIER` of 0.9 against the base game's 0.25 makes it count nearly four times as much there.

`confirmed` **It is that country's own day, before the later countries' days** [exe]: the countries' days run one after
another, the whole of one before the next (`tick.md`, section 3), and a trade route is exchanged in its `from`'s day - so
the supply need's `traded_for` and `traded_away` hold what the countries before it and its own day traded, and nothing of
the routes the later countries run with it. Read on the running game at the first midnight after a load: the Soviet
Union's need reads 0.814 supplies traded for, where the day ends at 145.633 (`reversing/findings/FINDINGS-liveread.md`,
section 141). *Ours:* it is weighed at the end of the country's economy in its day; the original's comes after the
country's later stretch too - its shares of leadership to its cabinet, its alignment and surrender, its convoys and the
manpower it has to spare - of which nothing was found to change what the weighing reads, the needs, the stockpile, the
dissent and the IC.

**A played country's own sliders** (built 2026-11-06, `frontend.md`, section 4.3): where production is the player's and
his slider automation is above nought, the same call is made for it at the end of its day with **his level, 1 to 3, and
his lend-lease share as it stands** - its fifteen binary places as a number, never below nought - and no
`BalanceLendLeaseSliders` after it [exe]. At level 0 nothing is called for it; with production handed over, the AI's call
at level 0. The production screen's arrow that raises the level sets the sliders by it at once in the original - here at
the next hour (`frontend.md`, *Ours*).

Their Finest Hour's script raises consumer goods for dissent, scales supplies by the stockpile, sets lend-lease to a
"preferred" share where the country gives any, and hands the six to **`CAI.FastNormalizeByPriority(level, ...)`**:

    order = the level's row of the priority table
    each share in order: its figure + 0.00005, at most what is left of the whole (the first at most 1);
        what is left goes down by it, never below nought
    answers the six, and what is left over

all in single precision, each step widened to double. **The helper's priority table is the engine's, hardcoded** - no
define reaches it - **but using the helper is the script's choice**: `BalanceProductionSliders` is Lua, and a mod may
normalise the shares itself in any order. The script says so ("if you mod that you cant use this function and have to
roll the commented out code above"), and the base game's file still carries that loop, commented out, over a
`PRIO_SETTINGS` table of its own. Their Finest Hour and BlackICE both call the helper, so for both the order is this
table. *Exe-hardcoded*: when the call is made and what it is handed. *Mod-able*: everything the script does with it.

| level | order |
| --- | --- |
| 0, the AI | consumer, lend-lease, supply, reinforce, production, upgrade |
| 1, Prioritize Production | lend-lease, consumer, supply, production, reinforce, upgrade |
| 2, Prioritize Upgrades | lend-lease, consumer, supply, upgrade, reinforce, production |
| 3, Prioritize Reinforcement | lend-lease, consumer, supply, reinforce, production, upgrade |
| 4, the script's own with a reinforcement bonus | lend-lease, reinforce, consumer, supply, production, upgrade |

It is why a share nothing needs reads 0.00006 in a save. The script then posts **`CChangeInvestmentCommand(tag,
lendLease, consumer, production, supply, reinforce, upgrade)`** where the six moved by more than a hundredth, made as the
leadership command is (section 3) - each over the float sum, **rounded half away from nought**, written as given. Sweden
in the 20 April save holds 2, 2, 11746, 9167, 105 and 11746, which is 0.00005 given to an empty share rounded to 2, where
truncating would give 1.

**`BalanceLendLeaseSliders`** sets each receiver's weight to its total IC by **`CChangeLendLeaseDistributionCommand`**
(`aid.md`, section 3).

## 6. A finished unit of a country nobody plays

Placed by the engine, not the script (`FINDINGS-ministers.md` B.6), **once a day in the country's hour `(number + 8) mod
24`**, going through its finished units in the order they finished. Every unit waiting draws its supplies and fuel each
day, every country's (`buildqueue.md`).

**One of a single brigade, ship or wing first tries to join a unit** of the country's own, lent to nobody:

- a wing joins an air unit of at most two wings - a carrier's air group only a unit based on a carrier with room, any
  other wing never a carrier's unit or an air group;
- a ship joins a fleet - a warship never a fleet of transports, a submarine only a fleet all of submarines;
- a brigade joins a land unit of at most three brigades that takes its role, at most one narrow brigade (width under 1),
  and with three wide only one of no width - counting what this day's placing has sent already.

A brigade's role: 0 a headquarters (radio strength above 1); 5 police, garrison or militia; 6 mountain; 7 marine; 8
paratroops; 3 armour (softness under 0.5); 1 support (width under 1); 4 fast (speed 6 or more); 2 otherwise. **A land
unit's role** (`CArmy::ClassifyRole`, `FINDINGS-ministers.md` B.6 and the record): 9 with no brigades; 0 with a
headquarters; else 3 where its brigades' average softness is under 0.5, then 5, 6, 7 or 8 where a brigade of that kind is
in it, then 4 where their average speed is 6 or more, 1 where half or more are narrow, 2 otherwise. A unit's role 0 takes
nothing; 1, 2 and 9 anything at a factor of 1; 3 takes 3 at a half and 4 at three times; 4 takes 4 at a half and 3 at
three times; 5 to 8 only their own role.

The score is `brigades x 10 x factor` less the unit's plan's want of that type (the AI's unit plans, not built: nought);
**the lowest under 100000 wins, the first of equals**, where the unit stands in a province the unit may be placed in - in
practice the acting capital's area (`buildqueue.md`, section 7).

**Otherwise, and for anything larger**, it goes to the province of the acting capital's area with the strictly highest
`LOCAL_IC/1000 + 2 x whole forts + 2 x whole coastal forts`, +10 for the acting capital itself, from -1, where it may be
placed - a fleet needs a naval base, an air unit an air base. **The area is walked in its own order**, the supply
network's (`supply.md`, section 2), so of equals the first found wins: Germany's 214. Infanterie-Division, waiting in the
July 1941 save, goes to Danzig, its land fort of 5 tying Berlin's 10 as the capital, Danzig being found first - as in all
three runs (`confirmed` on the listing, `reversing/findings/FINDINGS-liveread.md`, section 97). Ours had walked the area
outwards from the capital, which put Berlin first. **A carrier's air group goes only to the province of the first fleet
with a carrier that has none aboard, and never to an airfield**: with none it waits. **Once the pass has posted anything,
a group looks at the first fleet alone** - the original's flag of a posting is set by a join or a placing and cleared by
nothing between its deployments - so a group that comes after any posting waits unless the country's first fleet has an
empty carrier. Britain's 3rd, 4th, 5th and 10th CAG wait the whole of 16 to 18 July 1941, where ours had put them on
London's airfield and so crowded it that Oman's dive bombers left for another (`confirmed` on the listing and the
original's saves, `reversing/findings/FINDINGS-liveread.md`, section 135). A unit nothing suits waits for the next day. *Hardcoded*: every figure here.

## 7. What the scripts reach

Section by section of `FINDINGS-ministers.md`: the tech minister's methods A.6, the production script's units and
constructions C.7, its provinces and buildings D.1. The ones a reimplementation could get wrong:

- `minister:CanResearch(tech)`: a real technology, allowed now, **and not queued already**.
- `status:GetYear(tech, level)`: `start_year` for level 1 or less, `first_offset` for 2, `first_offset + (level - 2) x
  additional_offset` after - the country not asked.
- `CTechnologyDataBase.GetTechnologies()`: the null technology first, then each file of `technologies/` in the byte
  order of its name, each in file order - the order the script's random picks are made over.
- `GetTechnology(name)`, `GetBuilding(name)`, `GetSubUnit(name)`: exact, case and all; an unknown name is nil.
- `CConstruction:GetCost()`: one unit of the line's IC a day, **not** times its Size; `GetUsedIC()` the sum of it over the
  whole queue; `GetICPart(category)` the share times **all** the IC, whatever it was paid.
- `HasExtraManpowerLeft()`: the spare manpower, made each day - manpower, plus half a year's, less a quarter of build
  manpower for every brigade on the map on top of what it lacks, less each reserve line's men once (C.5) - above nought.
  **And as a saved game is entered**, which no save holds (`savegame.md`, section 2): until 2026-11-16 a loaded game's
  first day read nought, so Yugoslavia's minister in July 1941, with 2.768 to spare, built buildings and not units
  (`reversing/findings/FINDINGS-liveread.md`, section 108).
- `CountMaxUnitsStillBuildable(type)`: 1,000,000 for a type with no `minimum_of_type` or `max_percentage_of_type`.
- `GetNumOfPorts()`: the ports **in the acting capital's area** only.
- `CProvinceBuilding:GetMax()`, `HasBuilding()`: the level built, not the level working.
- `GetCurrentConstructionLevel(building)`: the size of the first line for it there in **the owner's** queue.
- `IsFrontProvince(false)`: owned, and touching land an enemy holds by land or by a strait.
- `GetTotalCoreBuildingLevels(index)`: made each day - the levels built in owned cores, and **a whole level for every line
  of the queue**, whatever its size.
- `GetRandomUnderGroundTarget()`: **not random** - the province at `757147 mod` the candidates' summed weights (D.5).
- `GetEscorts()`: the escorts on no convoy; `GetTotalNeededConvoyTransports()`: those on convoys and those wanted.

**The orders.** `CConstructUnitCommand(tag, brigades, province, count, reserve, CNullTag(), CID())` - one line of `count`
units one after another, built at the country's own levels, the men of all of them taken at once; **the province is
thrown away**; refused only for no brigades - not for want of men, technology or a type's cap, which the script asks
itself. A count of 0 is a line that takes no men and still delivers one unit. `CConstructBuildingCommand(tag, building,
province, count)` is the province view's order (`buildinglines.md`), its `IsValid` the same test, which the script asks
first; `CConstructConvoyCommand(tag, escort, count)` the convoy order (`convoylines.md`).

## 8. Against the saves

`OpenHOI3.SaveCheck headless` (`headless.md`) writes each great power's research queue, its shares and its summed
research progress. On 2026-10-16:

- **Research**: the eight majors' summed progress on 11 March 1936 beside the Ireland game's save of that day, every major
  the original's AI - Germany 12.10 against 12.20, the United States 9.20 against 9.02, Italy 4.73 against 4.46, China
  2.01 against 2.10; England, France and the Soviet Union ahead, 8.51, 5.71 and 6.10 against 6.62, 3.26 and 4.40 -
  countries the original starts with more technology (`resources.md`, section 5), so their levels cost it more. *Measured*.
- **The leadership command's rounding**: England's shares in the 20 April save are the script's (0.2, 0.08, 0.03, 0.69)
  through it to the 32768th (`LuaMinisterTests`).
- **A run to 1937**: no Lua error, every country holding its capital researching and building, 107 more units placed by
  the AI's placing and none left waiting.

## 9. Kept as the original does them

Do not tidy:

- The AI's shares are rounded half away from nought and written as given; the player's truncate.
- An AI country loses every technology of several levels from its queue the moment a level finishes.
- The production minister runs every day, but its desperation is made again on three days a month, the 29th among them.
- The lend-lease figure handed to the script is always 0.
- Lines moved to the front in one call end in reverse order; a land line paid nothing counts as under 120 days.
- The AI joins brigades only to units in the acting capital's area.
- `GetRandomUnderGroundTarget` is a fixed pick, and a province already given an underground line stays a candidate.
- `IsLand`, `IsAir` and `IsNaval` of a line look at its first brigade only.
- A count of 0 delivers a unit.
- *The script's*: `Process_Tech` sets a global `lbInfra`, which holds for every country the state runs after it.

## Ours

- **`math.random` is a generator of the state's own**, the C runtime's `rand` seeded 1 as a new thread's is, as the
  trading's states already have one (`lua.md`). The original's is the thread's, and which thread runs which country is
  its worker pool's choice, so its draws are not the same from one run to the next; here a game is replayable from its
  seed and its commands.
- **The ministers' scripts run side by side** (2026-11-18), the tech, production, foreign, politics and intelligence
  ministers' alike, on the eight Lua states the trading runs on (`trade.md`, section 9; `CAiWorkers`), as the original's
  AI pass runs its agents on worker threads. An hour's countries of one minister go each to a state of its own - the
  country's number over 24, of eight (`CAiWorkers.ByDay`), since a minister's countries of one hour are 24 apart in
  number - and in the countries' order there. A run's draws on the game's generator are on one of its own, seeded from
  the game's seed, the hour, the country and which minister (`IAiScripts.Draw`); what each run posts reaches the outbox,
  and what follows it on the game's own state - the foreign minister's proposal, its pruning and its walk of prepared
  wars - runs, in the countries' order once all have run. **This changed every AI country's draws once**, where until
  then each minister ran its countries in turn on the one state and drew on the AI's generator. Two runs of a game still
  agree hour for hour, which is what is kept: the original's threads take countries as they come free, so its
  `lbInfra` above holds for whichever country its thread runs next, and here for the next on that state. In turn on
  the one state, as before, while a feed of the original's draws is set (`docs/replay.md`, section 9). Under BlackICE,
  ten days of 1936 took the five ministers 296 ms a day where they took 435; what is left is, hour by hour, the one
  heaviest country's run.
- **A country's number** for the hours is its place among the countries plus one, as the other ministers'.
- **The counts by type are made as the minister's hour begins**, from the queue and the units as they stand - the
  original's are made in the same place and kept until the next day, which comes to the same since nothing the script
  posts runs within its call.
- **Commands posted in the day's pass run before the next hour**, so the stop a finished level posts is in place before
  any minister's next hour; where the original runs it beside its AI pass was not read.
- **The AI's unit plans are not built**, so a unit's want of a type is nought when a finished unit chooses whom to join.

## Not established

- Whether the session runs posted commands in the order posted (the reverse order of moved lines rests on it).
- The order the units' loader numbers the types in, which is the order of `GetSubUnitList`; built as the files' order.
- The order of a country's controlled provinces at a new game; built as the provinces' order.
- What the seven notification channels the production minister joins carry.
