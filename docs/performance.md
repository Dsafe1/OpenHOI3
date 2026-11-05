# Performance: where the time goes, and whether C++ would help

Ours, not the original's. Written 2026-11-05 at the maintainer's word, as a study of rewriting this project in C++
instead of C# to make it faster - a step he would take only once the game is finished, so that the two versions can be
set side by side. Nothing here is decided; this is the reasoning to start that decision from, and the habits that keep it
cheap in the meantime.

## 1. The answer, in short

**A whole rewrite would buy little for what it costs, because most of an hour is not spent where C++ helps.** A partial
port - the simulation alone, behind the replay's hashes - is worth keeping open, and is to be decided on a profile of the
finished game, not now. Section 6 says what would decide it.

## 2. Where an hour's time goes

As measured, with `OpenHOI3.SaveCheck profile` and the headless run, both in Release:

- **An ordinary hour about 15 to 24 ms; an hour that turns a day about 90 ms**, and the map stops for that one once a
  game day, because the simulation runs on the frame's thread (readme, *Loose ends*).
- **A game year of the headless run about 190 to 280 seconds**, as more of the AI was built (`docs/mechanics/headless.md`).
- **Of thirty days of 1936, the unit agents 10.8 s and the AI's trading in Lua 7.7 s** - about seventy per cent of the
  hour, which is why a replay, which skips them, runs 224 s where its recording ran 584 s (`docs/replay.md`, section 1).
- **The Lua is already C.** It is the original's interpreter, Lua 5.1.4, built from `native/lua` into
  `openhoi3_lua.dll`. A rewrite of the C# around it changes only what crosses between the two: the boxed `float` handed
  over for each figure, the strings, the fields.
- **The runtime was tried on 2026-11-04 and is not the lever**: .NET 9 and 10, Server GC, the GC's budgets and the JIT's
  tiering all came out within the noise. **Allocation was**: the route finder's scratch arrays kept (`CRouteScratch`) and
  `CProvince.Edges` made an array took thirty days from 24.5 s to 18.9 s, the state's hash the same every midnight.
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
- the day's pass spread over the hours, or run side by side as the hour's trading already is (`CTradeWorkers`);
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
