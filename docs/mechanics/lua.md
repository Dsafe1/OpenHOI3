# Lua: the install's scripts

The original's AI is written in Lua - `script/` in an install, a mod's own over it - and the game
asks it questions: what relations a new game gives, which laws a country wants, what the AI makes of
an offer. This is how those scripts are run here: on **the original's own interpreter, Lua 5.1.4,
built from its source** - the maintainer's choice on 2026-10-12, over a C# interpreter of our own
and over MoonSharp - with the classes the original hands them, built one at a time on the
simulation. The first slice was what a new game's relations need (`diplomacy.md`, section 2); the
second, on 2026-10-13, what the AI answers an offer with (section 6).

Sources: `reversing/findings/FINDINGS-luahost.md` (the state, the files, the two calls of a new game,
the methods, `==`, the x87; read 2026-10-12), `reversing/findings/FINDINGS-diplostate.md`, A;
`reversing/findings/FINDINGS-luaapi.md` (the offer's call, what the scripts reach of the API and what
each part answers; read 2026-10-13), `reversing/findings/FINDINGS-diplomacy.md` and
`reversing/findings/FINDINGS-survivors.md` (which offers ask which function). The interpreter's
source and what was changed in it: `native/lua/readme.md`.

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
2. the game's classes (section 4), **each a global by its name** holding its static functions and its
   constants - `CCurrentGameState.GetFaction("allies")`, `CGoodsPool._MONEY_`, `SpyMission.SPYMISSION_TECH`;
3. **a searcher at `package.loaders[2]`**, ahead of Lua's own, which for `require(name)` looks for
   `script/<name>.lua`, then `script/country/<name>.lua`, then `common/<name>.lua`, each through every
   layer of the install - TFH's before the base game's, and a mod's before both when there are mods -
   naming the chunk by its bare path;
4. **`script/autoexec.lua` run**, from the highest layer that has it.

`math.randomseed` is never called. Every failure - a file that does not load, a Lua error, a call that
fails - is reported as `LUA Error: <message>` and nothing else happens, as the original only logs it.
A failure in `autoexec.lua` ends it there: what it had already required stays, what came after is not
loaded.

**Of everything the 58 files the base game's `autoexec.lua` loads read as they are loaded, the only
engine global is `SpyMission`**, an enum read at the top of `ai_intelligence_minister.lua`
(`FINDINGS-luaapi.md`, section 2). With it, the whole of `autoexec.lua` runs. `confirmed`.

## 4. The classes

An object is handed to a script as a userdata whose methods are called with `:`.

- **Each push is a new userdata**, as luabind makes one: a country handed over twice is two values.
  A tag is moreover a **fresh copy** each time, so a script holding one holds its own.
- **`==` between two of them is one operator for every class**, as in the original, where every
  instance shares one metatable: the left object's class's `==` answers it if it has one, else the
  right one's, else it is the Lua error `No such operator defined`. So **`==` between two countries,
  even one country handed over twice, is an error**, as in the original; between two values of one
  userdata it is true without asking anything, as Lua does it. An `==` given an object of a class it
  does not take is the error `No matching overload found`. `<` and `<=` go the same way. Against a
  number, `==` is plain false and `<` an error, as Lua 5.1 compares nothing else.
- **A method not built is no method of the object**: calling it is a Lua error naming it.
- **A method that reads what the simulation does not hold answers nought or false and reports it**,
  once a session for each thing missing (`OpenHOI3.SaveCheck aiscore` lists them).

Built so far, on the simulation. A `CFixedPoint` is section 5; an iterator is section 4.1.

| class | built | answers |
| --- | --- | --- |
| `CCountry` | `GetCountryTag()` | its tag |
| | `AIGetTradeRoutes()`, `GetTradedAwaySansAlliedSupply()`, `GetTradedForSansAlliedSupply()`, `HasDiplomatEnroute(tag)`, `NeedConvoyToTradeWith(tag)`, `GetTransports()`, `GetGlobalModifier()`, `GetTechnologyStatus()` | `trade.md`, section 9: an iterator over its routes, running or not, the other countries in their order; the day's trade pools; a diplomat on his way there; whether trade needs a convoy; its free transports; its modifier and its technology for the two classes below |
| | `GetRelation(tag)` | its `CDiplomacyStatus` towards that country |
| | `IsAtWar()`, `Exists()` | at war with anyone; owns a province |
| | `GetGovernment()`, `GetFaction()`, `HasFaction()` | its government; its faction - **the null faction** for none; a faction that is not the null one |
| | `GetRulingIdeology()` | its ideology (`CIdeology`) |
| | `GetActingCapitalLocation()` | its capital's province - the two differ only for a government in exile |
| | `GetPool()` | its capital province's goods (`CGoodsPool`) |
| | `GetDailyBalance(g)`, `GetDailyExpense(g)`, `AICalculateExpense(g)` | section 4.2 |
| | `GetDiplomaticDistance(tag)` | the straight distance between the two capitals - four times the pixels between their centres, rounded down - as whole thousandths |
| | `GetNeutrality()`, `GetEffectiveNeutrality()`, `GetSurrenderLevel()` | the two neutralities; section 4.2 |
| | `GetMaxIC()`, `GetTotalIC()` | **the IC of its provinces before any bonus**, whatever the name says; the IC it has to spend |
| | `GetNumberOfOwnedProvinces()`, `GetNumberOfControlledProvinces()` | the two counts |
| | `GetOverlord()`, `IsPuppet()` | its overlord's tag - **the null tag, `---`**, for none; whether it has one |
| | `GetStrategy()` | its `CAIStrategy` |
| | `GetCurrentAtWarWith()`, `GetNeighbours()`, `GetDiplomacy()` | iterators: the countries at war with it, its neighbours, one status for every country (4.1) |
| | `IsEnemy(tag)` | not itself, and at war with it or either is `REB` |
| | `IsFriend(tag, b)` | itself; either is the other's overlord; one overlord over both; one faction; an alliance; fighting a war together and not enemies; with `b`, military access towards it - this country may pass through its land |
| | `IsGovernmentInExile()` | **false - not built**, reported |
| | `IsLandSafeToJoin(faction)` | false for the null faction; true where the faction's leader is at peace; **otherwise false**, whatever the land - the original's |
| | `IsMajor()` | `major = yes` in its file, or `GetMaxIC()` over 100 |
| | `IsNonExileNeighbour(tag)` | a country touching the land this one **controls**, owned by somebody and neither owned nor controlled by this one, and not in exile |
| `CCountryTag` | `GetCountry()`, `IsValid()` | the country; not the null tag |
| | `IsReal()` | a tag of the game's countries and not the null one (`trade.md`, section 9) |
| | `==`, `tostring` | the same tag; its three letters |
| `CDiplomacyStatus` | `GetValue()`, `GetThreat()`, `GetTarget()` | relation, threat; the other country's tag |
| | `HasAlliance()`, `HasWar()`, `IsGuaranteed()`, `IsGuaranting()`, `IsFightingWarTogether()` | an alliance between the pair; the pair at war; **the other guarantees the holder**; the holder guarantees the other; both on one side of a war |
| | `HasFriendlyAgreement()` | an alliance, military access, either the other's overlord, or a guarantee either way |
| | `HasMilitaryAccess()` | the holder may pass through the other's land (`agreements.md`, section 5) |
| | `AllowDebts()` | whether the holder may run a debt with the other (`aid.md`); nothing sets it yet |
| | `HasTruce()` | **false - not built**, reported |
| `CFaction` | `==`, `IsValid()` | the same faction; not the null faction |
| | `GetTag()`, `GetFactionLeader()`, `GetNumberOfMembers()` | its name as a `CString` - `axis`; its first member's tag, the null tag for none; how many it has |
| `CGovernment` | `==` | the same government |
| `CIdeology` | `GetGroup()`, `GetKey()` | its group (`CIdeologyGroup`); its name |
| `CIdeologyGroup` | `==`, `GetKey()`, `GetFaction()` | the same group; its name; its faction, nil for none |
| `CProvince` | `GetContinent()`, `GetController()`, `GetOwner()`, `GetProvinceID()` | its continent; the tags; its number |
| `CContinent` | `==`, `GetTag()` | the same name; its name |
| `CString` | `tostring`, `==`, `GetString()` | its text |
| `CGoodsPool` | `Get(g)`, `GetFloat(g)` | one good, as a `CFixedPoint`; as a float |
| `CAIStrategy` | `GetFriendliness(tag)`, `GetAntagonism(tag)`, `GetThreat(tag)` | **0 - not built** (the `add_ai_strategy` effect is unbuilt and the base game declares none), reported |
| | `GetCountry()`, `GetCountryTag()` | whose it is |
| `CAI` | `GetCountry()`, `GetRelation(a, b)`, `GetNumberOfOwnedProvinces(tag)`, `GetCurrentDate()` | the country it is the AI of; `a`'s status towards `b`; that country's count; the date |
| | `GetCountryAlignmentDistance(a, b)` | section 4.3 |
| | `CanTradeFreeResources(a, b)` | both have the faction rule `free_resource_gifts` (`trade.md`, section 8) |
| | `AlreadyTradingDisabledResource(route)`, `HasTradeGoneStale(route)`, `GetSpamPenalty(tag)`, `PostAction(action)` | `trade.md`, sections 2 and 9: a good of the route traded on a pair's route not running; failing for a fortnight; ten a day to 24 days past the last diplomat's arrival, for a played country only; the offer posted as the screen posts one, dated when it was made |
| `CLicenceTechnologyAction` | `CLicenceTechnologyAction(buyer, seller)`, `GetSubunit()`, `SetSubunit(type)`, `GetSerial()`, `SetSerial(n)`, `GetParalell()`, `SetParallel(n)`, `GetMoney()`, `SetMoney(fp)`, `SetValue(b)`, `IsValid()`, `IsSelectable()`, `GetAIAcceptance()` | `aid.md`, section 4; the getter `GetParalell` and setter `SetParallel` are the game's spellings. `DiploScore_LicenceTechnology(ai, buyer, seller, seller, action)` is handed the licence itself |
| `CSubUnitDefinition` | `GetBuildCostIC()`, `GetBuildCostMP()`, `GetBuildTime()`, `IsCapitalShip()`, `GetKey()` | a unit type's own figures, without a country's technology |
| `CSubUnitDataBase` | `GetSubUnit(name)` | the type by its key, or nil |
| `CTradeAction` | `CTradeAction(actor, recipient)`, `SetTrading(fp, g)`, `GetTrading(g, tag)`, `GetRoute()`, `SetRoute(route)`, `SetValue(b)`, `IsValid()`, `IsSelectable()`, `GetAIAcceptance()` | `trade.md`, section 9; asking the score re-enters the Lua for `DiploScore_OfferTrade` |
| `CTradeRoute` | `GetFrom()`, `GetTo()`, `GetTradedFromOf(g)`, `GetTradedToOf(g)`, `IsInactive()` | its two ends; what each hands over a day; whether it ran today |
| `CGoodsValues` | `vMoney`, `vFuel`, `vCrudeOil`, `vMetal`, `vEnergy`, `vRareMaterials`, `vSupplies` | a block of a route's terms, single precision, read with `.` |
| `CModifier`, `CTechnologyStatus` | `GetValue(_MODIFIER_GLOBAL_IC_)`; `GetIcModifier()` | the country's global IC, other kinds 0 reported; its technology's IC |
| `CEU3Date` | `GetYear()`, `GetMonthOfYear()` | the year; the month from 0 |
| `CResourceValues` | `CResourceValues()`, `GetResourceValues(country, g)` | six fields, read with `.`: section 4.2 |
| `CCurrentGameState` | `GetFaction(name)`, `GetFactions()` | the faction of that name, case and all, the null faction for none; an iterator over the factions in `common/ideologies.txt`'s order |
| | `GetProvince(id)`, `GetCurrentDate()`, `IsPlayer(tag)` | the province; the date; whether a person plays it |
| | `GetCountries()` | an iterator over every country, in their order |
| `CCountryDataBase` | `GetTag(name)` | the tag of those letters, the null tag where there is no such country |
| constants | `CGoodsPool` | `_SUPPLIES_` 0, `_FUEL_` 1, `_MONEY_` 2, `_CRUDE_OIL_` 3, `_METAL_` 4, `_ENERGY_` 5, `_RARE_MATERIALS_` 6, `_GC_NUMOF_` 7 |
| | `SpyMission` | `SPYMISSION_COUNTER_ESPIONAGE` 0 to `SPYMISSION_COVERT_OPS` 8, `SPYMISSION_MAX` 9 |
| | `CAIStrategy`, `CDiplomaticAction`, `CRule`, `CAI`, `CSpyPresence` | their enums as the original registers them (`FINDINGS-luaapi.md`) |

**`CAIIntel(us, them)` is not built**: what it knows comes out of espionage, which is unbuilt here,
and its perceived strength rolls the AI's own generator once for every unit. Making one is the Lua
error `CAIIntel is not built`.

### 4.1 Iterators

A method the original registers with its iterator policy answers a function for a generic `for`. It
walks its container in the container's order: `GetDiplomacy()` one status for **every country by its
place in the country list, the country itself included**; the lists in the order their countries
are declared (`CCountryLists`); the factions in their file's order. **A loop over an array stops at
the first element that is null**, Lua's `for` ending at the first nil - the original's, which
nothing here yet has a null element to show.

### 4.2 Goods, the day and surrender

`GetDailyBalance(g)` is what came in less what went out: home production (and convoyed in, but not
for supplies or fuel) + sent back + repaid to it + conversion made + traded for, less sent to -
repaid away + used + conversion used + convoyed out + traded away - **the day's debt repaid taken off
the spending**, as the original writes it; the debts move money alone (`aid.md`, section 1).
`GetDailyExpense(g)` is traded away - repaid away + convoyed out + conversion used + sent to + used.
The original has tribute's columns in each too, which are not built: nought, reported.

`GetResourceValues(country, g)` fills `vDailyExpense` (below), `vDailyHome`, `vConvoyedIn`, `vPool`,
`vDailyIncome` (the income side of the balance) and `vDailyBalance`, each thousandths over 1000 as a
single-precision float. `vDailyExpense` is, for metal, energy and rare materials, `(tech + gic) * m +
m` - `tech` the technology's `ic_modifier` and `gic` the `global_ic` modifier, each over 1000 as a
float, `m` the base IC - times 2 for energy and 0.5 for rare materials, every step rounded to a float;
nought for the other goods; and **never less than the day's spending** as `GetDailyExpense` counts it.

`GetSurrenderLevel()` is the surrender progress (`war.md`, section 12), 0 to 1000 thousandths: 1000 for
a country controlling no province, else the occupied share of its owned cores' victory points over its
national unity.

### 4.3 The distance on the triangle

`CAI:GetCountryAlignmentDistance(a, b)`, thousandths, as the original works it in single precision:

    s = dx*dx / 1000 + dy*dy / 1000      each cut toward nought
    f = (float)((float)s / 1000)
    r = (float)sqrt(f)
    answer = floor((float)((float)(r + 0.0005) * 1000))

`dx`, `dy` the two countries' places on the triangle, thousandths.

## 5. `CFixedPoint`

A figure the original keeps in thousandths reaches a script as a `CFixedPoint`, never as a number.

- **`Get()` is a single-precision float**: the thousandths over 1000, rounded to the nearest float.
  So `0.1` reads `0.100000001490116`. `GetTruncated()` is the thousandths over 1000, toward nought.
- Two of them: `+`, `-` on the thousandths; `*` is `a * b / 1000`, toward nought; `/` is
  `a * 1000 / b`, toward nought, and **-1 thousandth where `b` is nought**, no error. `==`, `<`,
  `<=` compare the thousandths. Each answers a new `CFixedPoint`.
- **With a number**, the number is first a float and then thousandths, `fx(n) = floor((float)((float)(n
  + 0.0005) * 1000))`. With the object on the left - `fp + n`, `fp - n`, `fp * n`, `fp / n` - the
  answer is a `CFixedPoint`, as for two. **With the number on the left the answer is a number** -
  the result's thousandths over 1000 as a float - **and `n - fp` and `n / fp` are worked the other
  way round**: `n - fp` is `fp - n` and `n / fp` is `fp / n`, the original's.
- `fp == 0.5` is false and `fp < 0.5` an error (section 4).
- `tostring` of one is not built: how the original writes one was not read.

## 6. The calls a game makes

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

**The AI's answer to an offer** (`IAiScripts.DiploScore`) is `DiploScore_<kind>(ai, actor,
recipient, observer)`: `ai` a `CAI`, the AI of the **recipient**; the three tags fresh copies. Which
offer asks which function, and which tag is the observer, is each offer's own and comes with the
agreements; fourteen do (`FINDINGS-diplomacy.md`), five of them with a fifth argument - the offer, or
a war goal - and three more functions declare a fifth that is never passed, so it is nil. **The
answer is a number turned into an integer as section 1 says; where the call fails - an error, no
such function - it is 0**, reported: the offer is refused. A result that is not a number is 0 too,
reported; in the original it escapes as an uncaught C++ exception. The original answers 100 without
asking when the player offers under the `yesmen` cheat; there is no console here.

**An AI answering an offer asks it** for alliance, NAP and both kinds of transit rights, each hour
(`agreements.md`, sections 2 and 3), and the request dialog asks it for the word it shows.

**A trade is answered by `DiploScore_OfferTrade(ai, actor, recipient, observer, action, tradedFrom,
tradedTo)`** (`trade.md`, section 9): the observer is the recipient, the action a `CTradeAction`
holding the terms, and the two `CGoodsValues` each block's goods. **A country's own trading is the
globals `ProposeTrades(ai, tag)` and `EvalutateExistingTrades(ai, tag)`** - the original's spelling -
asked each hour of a country nobody plays, one in six and then one in ten (`IAiScripts.RunTrade`);
what they decide comes back as offers posted through `CAI:PostAction`, dated as the action was made.
A failure in either is reported and leaves that country's trading for the hour.

`OpenHOI3.SaveCheck aiscore` asks the
eleven functions that take nothing but tags, for every pair of countries. On 2026-10-13 a new game of
the base game had 78 countries owning land, and every one of the eleven answered all 6,006 pairs
without an error **but `DiploScore_PeaceAction`, which failed at `CAIIntel` every time**; with `CAIIntel`
built the same day (section 7) all eleven answer every pair. What the scripts meet that is not built: the
AI's strategy, governments in exile, espionage and tribute.

## 7. `CAIIntel`

`CAIIntel(us, them)` (`reversing/findings/FINDINGS-war.md` C.6) is what one country knows of another's
strength; only `DiploScore_PeaceAction` makes one. **Espionage is not built, so nobody has spies anywhere**:
what spies would add is nought, reported. As it is made:

- **the factor** is our spies in them over the most, ten - `level * 1000 / 10000` - less their
  `counter_intelligence` modifier, in thousandths, over 1000 as a float; so with no spies it is their
  counter-intelligence turned round, and **a country whose laws give it -0.25 is known at 0.25**;
- **the uncertainty** is `(float)(1 - factor + 0.2)`, held to 0.2 and 1.

`GetFactor()` answers the factor; `HasNoIntel()` whether we have fewer than one spy - always;
`GetTheirFactor()` their own spies' level over the most - nought. **`CalculateOurMilitaryStrength()`** is
every brigade's strength of every unit we have, over 1000, a float. **`CalculateTheirPercievedMilitaryStrengh()`**
(the original's spelling), in thousandths unless said:

    bonus  = SPY_INTEL_MISSION_BONUS (0.5) x our military mission's share in them / 1000   - nought
    f      = floor((float)((float)((float)(bonus / 1000) + factor) + 0.0005) x 1000)
    K      = floor(100000.5f)                                        = 100000
    chance = f x (K x SPY_UNIT_DETECTION_CHANCE (0.5) / 1000) / 1000 / 1000     per cent
    land   = K x our offmap_land_intel / 1000 / 1000;  sea likewise with offmap_naval_intel

then **for each of their units, in the order they were made, a draw of the game's generator first**; the unit
is seen where we know its province at 2 or more, or where the draw's remainder by 100 is under `land` or
`sea` - by whether its province is land - plus `chance`; its brigades' strength goes to seen or hidden.
The answer is `(seen + max(hidden + k x scale / 1000, 0)) / 1000` as a float, where `scale =
floor((float)((float)(uncertainty + 1) + 0.0005) x 1000) x hidden / 1000` and `k = floor((float)((float)((float)(u
- 0.5) x uncertainty) + 0.0005) x 1000)`, u one more draw over 2^31. **Every answer takes one draw for each
of their units and one more**, so the AI's answer to a peace moves the game's generator. The draw is never
negative, so the remainder is the plain 0 to 99 - the fact base's "about half of all units are seen
whatever the odds", from a signed draw, was corrected by this reading.

## Ours

- **The state lives as long as a session**, where the original's lives as long as the process and
  carries a script's globals from one game into the next. Nothing in the base game relies on that.
- **Lua's arithmetic is in full double precision.** The original's main thread ran its Lua under the
  x87 set to 24 bits by Direct3D 9 (`FINDINGS-luahost.md`, section 6), so a script's fractions there
  were rounded to single precision at each step. A new game's relations are whole numbers and exact
  either way; what the AI's fractional arithmetic does is not established. The engine's own floats -
  `Get()`, the alignment distance, the resource values - are single precision here as there.
- **Only the classes, methods and globals above are built.** The original makes every one of its
  registered classes a global; here a class is a global once something of it is built.
- **An iterator walks a copy of its container** taken when the loop starts; the original walks the
  container itself. Nothing in the scripts changes a container while walking it.
- **The factions' order** is their file's; the original keeps them in a list of its own, not read.
- **There is no country for the null tag.** The original keeps a placeholder country, `---`, as its
  number 0: `GetCountry()` on the null tag answers it, and `GetDiplomacy()` begins with a status
  towards it. Here the first is nil and the second has no such entry; the scripts test `IsValid()`
  before either matters.
- **The lists of neighbours and enemies are worked out when asked**, as `CCountryLists` does
  everywhere, where the original keeps them and rebuilds them once a day. It costs about a third of
  a millisecond a call.
- **Built for 64-bit Windows only**, with the Visual Studio C++ tools.

## Not established

- What the original does after a non-number answer, which escapes as a C++ exception.
- Whether the AI's worker threads ran their Lua at 24 bits too.
- Which `rand` the original's `math.random` is, unseeded; nothing calls it yet.
- The order of the original's lists of neighbours and of countries at war: here, declaration order.
