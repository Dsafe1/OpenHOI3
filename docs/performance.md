# Performance: where the time goes, and whether C++ would help

Ours, not the original's. Written 2026-11-05 on request, as a study of rewriting this project in C++
instead of C# to make it faster - a step to be taken only once the game is finished, so that the two versions can be
set side by side. Nothing here is decided; this is the reasoning to start that decision from, and the habits that keep it
cheap in the meantime.

## 1. The answer, in short

**A whole rewrite would buy little for what it costs, because most of an hour is not spent where C++ helps.** A partial
port - the simulation alone, behind the replay's hashes - is worth keeping open, and is to be decided on a profile of the
finished game, not now. Section 6 says what would decide it.

## 2. Where an hour's time goes

As measured, with `OpenHOI3.SaveCheck profile` and the headless run, both in Release:

- **An ordinary hour about 15 to 24 ms; an hour that turns a day about 90 ms**, and the map stops for that one once a
  game day, because the simulation runs on the frame's thread (`docs/worklist.md`, *Loose ends*).
- **A game year of the headless run about 190 to 280 seconds**, as more of the AI was built (`docs/mechanics/headless.md`).
- **Of thirty days of 1936, the unit agents 10.8 s and the AI's trading in Lua 7.7 s** - about seventy per cent of the
  hour, which is why a replay, which skips them, runs 224 s where its recording ran 584 s (`docs/replay.md`, section 1).
- **The Lua is already C.** It is LuaJIT's interpreter, built from `native/luajit` into `openhoi3_lua.dll` - the
  original's Lua 5.1.4 until 2026-11-19, and behind a switch since (below). A rewrite of the C# around it changes only what crosses between the two: the boxed `float` handed
  over for each figure, the strings, the fields.
- **The runtime was tried on 2026-11-04 and is not the lever**: .NET 9 and 10, Server GC, the GC's budgets and the JIT's
  tiering all came out within the noise. **Allocation was**: the route finder's scratch arrays kept (`CRouteScratch`) and
  `CProvince.Edges` made an array took thirty days from 24.5 s to 18.9 s, the state's hash the same every midnight.
- **Measured on 2026-11-18, thirty days from 1 January 1936 with Germany played**: the base game about 500 to 540 ms a
  day, BlackICE about 1,300 - its ordinary hour 34 ms on the median and the hour that turns a day 275. Of BlackICE's
  day the unit agents take about 230 ms and the ministers' Lua about 220.
- **Then the same day, what the hours ask over and over was kept instead of found again**, each the state's hash the
  same every midnight, base game and BlackICE: a brigade's organisation, morale, officers and strength held as fields
  of its figures (`CSubUnitStats`), worked out again only when the figures change; the levels it holds added up only
  when one changes (`CSubUnitLevels`); its terrain adjusters found by key (`CAdjusters`); a name asked of a unit's
  brigades looked up once for the unit and not once a brigade; a country's facts kept until a province changes hands or
  a building is set, not for the hour (`CCountryFacts`); and the table of who stands where kept - a unit just made
  added to it, and the start of the hour's making it again turned into a check that it still agrees, made again only
  where it does not (`CGameState.Positions`), so it is made 32 times in ten days where it was 741. BlackICE's day came
  to about 1,215 ms, the base game's to about 485: the hourly organisation 30 per cent less, the march 22, the trading
  22, the daily upkeep 44.
- **And then the conditions of script**, which the triggered modifiers' daily test, the events, the decisions and the
  foreign minister all ask: a condition's keyword lowered once as it is read and not at every asking, and which class
  answers it found the first time and kept on it (`CTrigger.Answerer`), the chain of nine not walked again - the classes
  take a keyword or leave it by the keyword alone. The triggered modifiers' test, BlackICE's 1,798 entries for every
  country each day, took half its time; the day's rebuild of every country's modifier 45 per cent less. **It was not run
  side by side**, as was proposed: one country's test reads others' modifiers - a province's revolt risk reads its
  holder's - after their rebuild, and a condition writes the state (`check_variable` makes the variable it asks for), so
  side by side would need every read watched, for less than was had this way. A brigade's figures made again - every
  level an upgrade gives - copy their figures by key and reuse the brigade's own adjusters: the day's production 12 per
  cent less. Each the state's hash the same every midnight, base game and BlackICE, and the `conditions` and
  `scripttext` looks line for line.
- **The hour that turns a day was then made shorter by making its steps faster, and not by spreading them over the
  hours**, which would have moved what happens when, away from the original's order - by decision, 2026-11-18. What a
  day walked the whole map for, country by country, walks each country's own provinces (`CHeldProvinces`, kept until
  a province changes hands, its walk going on over the rest of the map as before where one changes hands on the way):
  home production 71 per cent less, the roll for a rising with it. The day's look at what countries know weighs a
  beside province's encryption only where a unit stands to be weighed against it, and a province's repair works out
  its rate only where something is short. The midnight hour's median about 215 ms to 201 under BlackICE, against an
  ordinary hour's 30; what is left in it is spread over many steps of a few milliseconds each, and the AI's.
- **The AI's scripts are the interpreter's time, not the crossing's** (measured the same day): of the ministers' time
  four fifths is native - the original's Lua running the install's scripts - and our side of the crossing, the pushes
  and the calls, about a twentieth. The collector is not it either: its pause at 1000 in place of 200 gave about 4 per
  cent, so it was left. A userdata a push stays, as luabind's: two pushes of one object are two values to a script.
- **So the interpreter was changed: LuaJIT's runs the scripts since 2026-11-19** (`native/luajit/readme.md`). Measured
  that day on thirty days of BlackICE with Germany played, the ministers' and the trading's Lua a day, the base game's
  in brackets: Lua 5.1.4 300 ms (101), the same built with profile-guided optimisation 246 (65), **LuaJIT's
  interpreter 201 (56)**, LuaJIT with its compiler on 223 (63) - the day 1,232, 1,066, 992 and 1,035 ms. A second round
  ran beside another session's work and is left out; an earlier one gave profile-guided optimisation 6 per cent and the
  base game's 13. The compiler loses to the interpreter because the scripts call into our classes all the time and
  every call ends what it is compiling; it is left off. Profile-guided optimisation was not taken up: its profile
  comes from a run on an install and fits one compiler's version alone. **As built, set beside 5.1.4 on the same build
  with `lua=5.1.4`, two rounds each run back to back**: BlackICE's Lua 254 and 259 ms a day against LuaJIT's 194 and 204,
  the day 1,075 and 1,102 against 1,005 and 1,064; the base game's Lua 70 against 57 and 59 (5.1.4's second round, 115,
  ran beside other work). So **about a fifth of the Lua, some 55 ms of BlackICE's day** - the first round's 300 for
  5.1.4 was high. **The price is the order of `pairs`**: within
  a new game's first days the ministers post the same commands in another order and the production minister picks other
  brigades among equals, so a game plays otherwise from the same seed - each interpreter the same every run, and every
  check against the start save's figures the same under both. Lua 5.1.4 stays behind a switch for the comparisons that
  want the original's order (`docs/mechanics/lua.md`, section 1; `docs/compatibility.md`, entry 30).
- **The walk of a country's units made one enumerator** (`CUnitWalk`) where it was two nested ones and a new stack a
  walk - the same units in the same order, as live as before: a unit's units are read only as the walk is asked for the
  next, so what a caller does to a unit as it meets it is what the walk then finds. Every walk gains, the AI's among them
  without its code touched: the unit agents about 7 per cent, the research minister 25, the officers 15, the hour's check
  of who stands where 20. A list of each country's units kept between walks was weighed and not built: the tree's lists
  are changed in some hundreds of places, and a kept list would be exact only for callers that change nothing as they
  walk.
- **The hour's weather runs its provinces side by side** (2026-11-19, `CWeatherPass`): a province's hour reads only its
  own figures, the clock, the map's size, its terrain and the defines, and writes only its own, so the order does not
  matter and the state's hash is the same every midnight, base game and BlackICE. The pass went from 1.28 ms an hour to
  0.58 under BlackICE - about 31 ms of the day to 14 - and a new game's 96 hours of weather with it. Eight cores give
  only twice: the pass is its provinces' memory being read, a few lines of it a province, not arithmetic; the
  arithmetic was already the original's integers in a tight loop. It is the first of the simulation's own steps run on
  more than one thread - the trading's Lua was until then the only one.
- **The frame, measured on 2026-11-19** with the client's `--perf` log (the fast build, Germany, a window of 1760 by
  990, another session's work on the machine): paused, 100 frames a second, 7.3 ms of each the rendering server's own
  CPU and under a millisecond of the GPU's; at the top speed Their Finest Hour 77 frames and 73 days a minute, BlackICE
  70 and 35. Two things held it, and both were changed that day:
  - **every mesh of the map's surface was drawn every frame** - 10,010 draw calls wherever the camera was, the effects
    placing the vertices so that Godot could not cull them. They are culled by our own test now (`CMapCulling`;
    `docs/mechanics/mapdraw.md`, section 6), the picture the same to the pixel: 1,617 draw calls at the start's view and
    2.5 ms;
  - **at the top speed the worker sat idle for most of each hour's cycle** - an hour started only after the frame that
    collected the last was drawn, so an hour of 14 ms took about 35. It runs hours back to back now, a twentieth of a
    second at a time (`docs/mechanics/tick.md`, section 2).

  Together: paused 144 frames a second, which the screen holds it to; at the top speed **Their Finest Hour 138 frames
  and 138 days a minute, BlackICE 138 frames and 48 days a minute**. BlackICE's top speed is the hour's own time now,
  its worker busy most of the minute.
- **What keeps the machine's other cores idle** (looked into on 2026-11-19, about 1.6 of 32 busy): the hours run one
  after another and their steps in the original's order, and the steps that already run side by side - the trading and
  the five ministers' scripts on `CAiWorkers` - each wait for their slowest country, a single Lua call. Running the five
  ministers as one batch would save at most 46 ms of a BlackICE day and 9 of a base game's, and was left alone. The one
  large piece that would spread is the countries' unit AI passes, which the original runs side by side; how, and what it
  would give - in a 1941 war 776 ms of a BlackICE day down to about 294 - is in `docs/enhanced.md`, *AI*, to be taken up
  once the AI's behaviour is settled.
- **A BlackICE game of July 1939, 2026-11-26** - a save the user sent with "the drop in performance in 1939", the
  engine's profiler showing `CTransferSubUnitCommand` at the top of its orders. Headless on it
  (`OpenHOI3.SaveCheck profile ... save=FILE`), beside a new game of the same mod five days in: **2.3 s a day against
  1.46**, with 3,007 divisions against 1,658, 20,117 brigades, ships and wings against 11,274 and 883 convoys against
  264 - the day grew as the world did, and no step by more than three times. A day's 2.3 s, by step: the unit AI 300
  ms (side by side already), the production minister's scripts 230, the day's economy country by country 220,
  the hourly organisation 215, the orders drained 170 (the AI's decisions 95 of it), the movement 140, the foreign
  minister 100, the officers' count 82, what countries know 100. A sample of the processor's time (`dotnet-trace`)
  names nothing above 6%: handing objects to the scripts 6%, the organisation's own loop 5.6%, conditions of script
  7% with what they call. Three things came of it:
  - **the transfers were the AI's air groups going round** - a mod's own air types joined and split again each
    regrouping, 180 transfers and 200 joins a day - the original's oversight, mended in an enhanced game by the fix
    `air_regroup_agrees` (`docs/mechanics/agents.md`, section 5.3). It was 2% of the day, not its weight;
  - **the day's reinforcement counted every base in the game once for each country** (`CBaseCrowding`'s walk of
    every unit, 100 times a day). It is counted now when a fleet or an air group is first asked about, so a
    country with neither counts nothing: the economy's step 230 ms to about 205. The state's hash after three days
    is the same part for part;
  - **the officers' count was tried as one walk** in place of each faction member walking every other's units, and
    gave nothing that could be measured - the walk itself is the cost - so it is as it was.
  **Left, and not small**: the organisation pass a country at a time side by side (about 7% of a day, each country's
  brigades its own - to be audited for what it shares); the officers' count made on a change and not each hour (3.5%);
  the foreign minister asking every decision's conditions for every country at its hour (4%); `UnitsInOrder` sorting
  its list at each of forty callers (2%); and the scripts' objects made anew at each push (6%). The profiler left
  running is itself about 1.5% of the processor's time.
- **Drawing is already C++** - it is Godot's renderer. The client's C# builds and sets nodes.

## 3. What C++ would buy, part by part

| part | its share | what C++ would give |
| --- | --- | --- |
| the AI's Lua - trading, the ministers | large | **nothing** for the interpreter; a thinner bridge perhaps a tenth to a third of the bridge's own share |
| the unit agents, routes, supply, battles | large | **about 1.3 to 2 times**, and most of that from laying the data out as arrays of plain values, which C# can do too |
| the day's pass - production, supply, convoys | the 90 ms hour | the same; **running it off the frame's thread** is worth more than the language |
| reading an install, the interface's text | start-up, or a frame | nothing that shows |
| the GC's pauses | already few | gone, but little is left of them |

**For a whole hour that comes to about 1.2 to 1.4 times, with the Lua as it is** - an estimate from what code of this kind
usually gains, not a measurement. Most of it is to be had without changing language:

- the simulation on a thread of its own, which every screen reading the state would have to allow for;
- the day's pass spread over the hours, or run side by side as the hour's trading and the ministers' scripts already are (`CAiWorkers`);
- structs, spans and kept scratch arrays in the hot loops, as `CRouteScratch` began;
- NativeAOT for the headless tools, which start and run without the JIT.

## 4. What a rewrite would cost

**The size**, counted on 2026-11-05:

| project | files | lines |
| --- | --- | --- |
| `lib/OpenHOI3.Sim` | 596 | 74,300 |
| `lib/OpenHOI3.Interface` | 170 | 20,868 |
| `src` | 147 | 26,130 |
| `lib/OpenHOI3.Content` | 119 | 16,164 |
| `lib/OpenHOI3.Lua` | 78 | 5,911 |
| `tests` | 214 | 47,622 |
| `tools` | 62 | 13,351 |

The tests (2306) and the savegame tool are what tell a port it is right, so they would have to come along or be driven
across the boundary.

**Godot in C++ is GDExtension**, through `godot-cpp`: the editor's quick rebuild of C# goes, and builds are slower. **A
hybrid avoids most of it** - the simulation a native library, the client, `Interface` and `Content` left in C# - at the
price of every screen reading the game state across a native boundary.

**Determinism leans on .NET's own behaviour in places a port would have to copy exactly**, counted in `lib/OpenHOI3.Sim`
on 2026-11-05:

- **12 `.Sort(` calls and 10 `OrderBy`.** .NET's `Sort` is an introsort and not stable, `OrderBy` is stable, and
  `std::sort` is neither of them. Wherever two equal entries can decide something, a port has to copy .NET's algorithm
  step for step - as `CStdSort` copies the original runtime's.
- **107 `Dictionary` and `HashSet` made.** Enumerating one follows the order things were added while nothing is removed,
  which .NET does not promise; `std::unordered_map` follows nothing of the kind. Any enumeration whose order decides
  something is a desync between the two versions.
- **96 `float` and `double`.** Mostly at the Lua's edge and in the air window's cone, kept in single precision as the
  original works it - deliberate already, and to be kept deliberate.

## 5. Why comparing the two later is cheap

The check a port needs is built:

- **`CHasher` is FNV-1a over whole numbers and text**, with no .NET hash in it, so a C++ simulation can give the same
  hash of the state each midnight (`docs/replay.md`, section 5).
- **A recording holds the AI's commands and draws**, so a C++ simulation can play a C# recording and be set beside it
  day by day, as `OpenHOI3.SaveCheck replaycheck` sets a game beside its replay - without its own AI, and without
  anything built on the C++ side but the rules.
- **`OpenHOI3.SaveCheck` sets the game beside the original's own saves**, which judges both versions alike.

So a port can be proven like for like, a part at a time, which is the reason to wait for a finished game: then there is
one fixed set of rules to port and recordings that hold for it.

## 6. What would decide it

1. **No whole rewrite.** If it is tried, it is the simulation alone, `lib/OpenHOI3.Sim`, behind the hashes of section 5,
   and the client, `Interface` and `Content` stay C#.
2. **A profile of the finished game first.** A port is worth it only if the C# simulation's own share - not the Lua's,
   not drawing - is still above about half an hour's time after the cheaper things of section 3. The guess on
   2026-11-05 is that it will not be.
3. **The cheaper things first**, since they are larger: the simulation off the frame's thread, which ends the stop once
   a day; the day's pass spread out; plain-value data in the agents and the route finder.

## 7. Habits that keep a port cheap meanwhile

These make the C# version sturdier too, so they are worth keeping whether or not a port comes:

- **No decision rests on the order a `Dictionary` or `HashSet` is enumerated in.** Sort, or keep a list beside it.
- **A sort whose equal entries can decide something says so** - stable, or the original's `CStdSort` where the original
  sorts with `std::sort`.
- **`CStateHash` covers every part of the state**, so a difference shows on the day it happens.
- **A recording or two is kept at each finished milestone.** They lapse when the rules change, so each serves that
  milestone's comparison alone, and are kept outside the repository.
