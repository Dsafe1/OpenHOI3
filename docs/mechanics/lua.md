# Lua: the install's scripts

The original's AI is written in Lua - `script/` in an install, a mod's own over it - and the game
asks it questions: what relations a new game gives, which laws a country wants, what the AI makes of
an offer. This is how those scripts are run here: on **the original's own interpreter, Lua 5.1.4,
built from its source** - the maintainer's choice on 2026-10-12, over a C# interpreter of our own
and over MoonSharp - with the classes the original hands them, built one at a time on the
simulation. The first slice is what a new game's relations need (`diplomacy.md`, section 2).

Sources: `reversing/findings/FINDINGS-luahost.md` (the state, the files, the two calls of a new game,
the methods, `==`, the x87; read 2026-10-12), `reversing/findings/FINDINGS-diplostate.md`, A. The
interpreter's source and what was changed in it: `native/lua/readme.md`.

## 1. The interpreter

Lua 5.1.4 - the version the original ships - numbers as doubles, built for 64-bit Windows from the
tarball's source. Two settings follow the original's 32-bit build where a 64-bit one would differ:

- **a number turns into an integer to the nearest, ties to even** (`lua_tointeger`, what
  `string.rep`, `string.sub` and the C API's integer reads use) - the original's build converts with
  the x87's `fistp`, and a 64-bit one would cut toward nought. `string.format('%d')` cuts in both,
  being a plain C cast.
- **the integer type is 32 bits wide.**

## 2. Calling in and out

Lua raises an error by a `longjmp`, and one through a .NET frame is undefined. So: **Lua code runs
only inside a protected call** made from C#, and **a function written in C# never raises a Lua error
itself** - it returns, and a small C trampoline built into the same library raises it. A C# function
that fails, or throws anything, is a Lua error the script can catch with `pcall`; one that fails
outside a `pcall` ends the call it is part of, which C# sees as an exception.

## 3. The state

As the original makes it (`FINDINGS-luahost.md`, section 1):

1. **all eight standard libraries** - base, package, table, io, os, string, math, debug - and **none
   of their globals removed or replaced**;
2. the game's classes (section 4);
3. **a searcher at `package.loaders[2]`**, ahead of Lua's own, which for `require(name)` looks for
   `script/<name>.lua`, then `script/country/<name>.lua`, then `common/<name>.lua`, each through every
   layer of the install - TFH's before the base game's, and a mod's before both when there are mods -
   naming the chunk by its bare path;
4. **`script/autoexec.lua` run**, from the highest layer that has it.

`math.randomseed` is never called. Every failure - a file that does not load, a Lua error, a call that
fails - is reported as `LUA Error: <message>` and nothing else happens, as the original only logs it.
A failure in `autoexec.lua` ends it there: what it had already required stays, what came after is not
loaded.

## 4. The classes

An object is handed to a script as a userdata whose methods are called with `:`. In the original
each class is also a global by its name, with its static functions and constants (`CCurrentGameState`,
`SpyMission`); **none is a global here yet**. Built so far, on the simulation:

| class | method | answers |
| --- | --- | --- |
| `CCountry` | `GetCountryTag()` | its tag |
| | `GetRelation(tag)` | its `CDiplomacyStatus` towards that country |
| | `IsAtWar()` | whether it is at war with anyone |
| | `GetGovernment()` | its form of government |
| | `GetFaction()` | its faction |
| `CCountryTag` | `GetCountry()` | the country |
| `CDiplomacyStatus` | `HasAlliance()` | an alliance between the pair |
| | `HasWar()` | the pair at war |
| | `HasTruce()` | the hundred days after a war between them - **always false here, no war ending yet** |
| `CGovernment`, `CFaction` | - | compared with `==` |

**`==` between two governments or two factions is true exactly when they are one**: the original
registers an `__eq` that compares their save tokens. Every country with no faction holds one shared
null faction, so two such countries' factions are equal; a country with no government holds the null
government. **A tag is a fresh copy each time it is handed over**, as the original pushes it. A method
not built is no method of the object: calling it is a Lua error naming it.

## 5. The calls a new game makes

**`AI_GenerateNonHistoricalRelation(country, tag)`**, for each pair a new game has given nothing
(`diplomacy.md`, section 2): `country` a `CCountry`, the other a `CCountryTag`. **The answer must be a
number** and is turned into an integer as section 1 says - 84.5 is 84, 85.5 is 86. **Where the function
is missing, raises an error, or answers anything but a number, the relation is 25** and the failure is
reported - the original sets 25 before the call and keeps it on a Lua error (a non-number is an
uncaught C++ exception there; here it is the same 25, reported).

**`Laws(country)`**, which the original calls next for every country, the player's included, is
**not called**: for the player's country by the maintainer's decision (`docs/compatibility.md`,
entry 6), and for the others because what it does - post a change of law - waits for the AI and
politics.

## Ours

- **The state lives as long as a session**, where the original's lives as long as the process and
  carries a script's globals from one game into the next. Nothing in the base game relies on that.
- **Lua's arithmetic is in full double precision.** The original's main thread ran its Lua under the
  x87 set to 24 bits by Direct3D 9 (`FINDINGS-luahost.md`, section 6), so a script's fractions there
  were rounded to single precision at each step. A new game's relations are whole numbers and exact
  either way; what the AI's fractional arithmetic does is not established.
- **Only the classes and methods above are built.** The base game's `autoexec.lua` stops at
  `ai_intelligence_minister.lua`, which reads the engine's `SpyMission` enum at its top; the modules
  `autoexec` requires after it are not loaded. `ai_country.lua` comes before it.
- **Built for 64-bit Windows only**, with the Visual Studio C++ tools.

## Not established

- What the original does after a non-number answer, which escapes as a C++ exception.
- Whether the AI's worker threads ran their Lua at 24 bits too.
- Which `rand` the original's `math.random` is, unseeded; nothing calls it yet.
