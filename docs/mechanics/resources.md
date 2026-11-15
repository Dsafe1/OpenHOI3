# Resources: what provinces yield, and what a country produces at home

How the original works out a country's daily resource production, written as rules this
project can implement. No addresses and no offsets.

**Most of this was measured, not read.** The fact base knows where resources are kept and
how they move; the formula for home production it does not have. It was worked out on
2026-10-07 from a savegame the original wrote on 1 January 1936, with no mods, and is
marked **measured** below. Rules from the fact base are marked `confirmed`, `likely` or
`inferred` in its vocabulary, and this project's own choices are marked **Ours**.

Sources: `reversing/CLASSES.md` (*The country's goods pools*, and the province's pools
above it), `reversing/findings/FINDINGS-trade2.md` (section 3),
`reversing/findings/FINDINGS-convoys.md`, `reversing/findings/FINDINGS-ic.md`.

## 1. The goods

There are seven, in this order: **supplies, fuel, money, crude oil, metal, energy, rare
materials.** `confirmed` A quantity of all seven together is a *pool*, and nearly everything
below is one: a province's production, a stockpile, a country's daily totals.

Money is in the list and is not like the others. It is never traded as a good: it is the
price of the other six, recomputed whenever a trade changes. `confirmed`

## 2. What a province yields

- A province's history file gives it `metal`, `energy`, `rare_materials` and `crude_oil`,
  and those are what it **could** yield in a day. (measured: 700 provinces in Their Finest
  Hour yield something, and all of them agree with the savegame.)
- A province has two such pools, **what it could yield and what it yields now**. At the
  start they are equal for every province. `confirmed` for the pair; (measured) for their
  being equal at the start. What it yields now steps back up towards what it could after
  each midnight, which is how damage wears off. `confirmed`
- The same file gives the province its `manpower`, its `leadership` and its victory
  `points`. (measured, against all 14168 provinces.)
- **The national stockpile is the pool of the capital province.** Not a field of the
  country: whoever holds the capital's pool holds the stockpile. A government in exile is
  the exception and keeps its own. `confirmed`

## 3. What a country produces at home

Each day, for metal, energy, rare materials and crude oil:

    home = sum over the provinces joined to the capital of
               yield  *  (1 + GLOBAL_RESOURCES)  *  (1 + the technology bonus for that good)
         + what the base IC needs

(measured) The savegame's figure for every country is reproduced **to the thousandth for
251 of 254 country-and-good pairs** - all 78 for rare materials and all 20 for crude oil.
Germany: 134 metal in its provinces, laws worth +10%, two levels of steel production at 5%
each, gives 134 x 1.1 x 1.1 + 5 = 167.14, and the savegame says `167.140`.

Piece by piece:

- **Only provinces joined to the capital count.** A province's production is home
  production if it can be reached from the capital over land the country controls. East
  Prussia's energy is not in Germany's figure; nearly all of Britain's metal is not in
  Britain's. What is cut off waits in a port and is carried by convoy, which is a
  different pool. `confirmed` that cut-off production travels by convoy; (measured) that
  it is absent from the home figure. **Built 2026-10-10** (`convoys.md`, section 8): the
  yield of cut-off land is put in its block's largest harbour each day and a resource convoy
  brings it to the stockpile, where it counts as convoyed in.
- **And a province that draws on a depot in the capital's block counts as joined to it**,
  wherever it is. `confirmed`, read with the convoys: it is how land with too little
  infrastructure to be in any block still sends its yield home.
- **Straits join.** A crossing listed in `adjacencies.csv` connects two provinces for this
  purpose: Japan's islands, Denmark's and Sicily all count as joined, and their figures
  match only if they do. (measured)
- **`GLOBAL_RESOURCES`** is the country's modifier of that name - its laws and ministers.
- **`LOCAL_RESOURCES`** is the province's own modifier of that name, which `non_core` and
  the occupation policies set (`modifiers.md`, section 3). *Ours:* each province's yield
  is scaled by one plus it, never below nothing, before the country's factors. Where the
  original applies it has not been read, and nothing in the 1936 savegame shows it: no
  province joined to a capital there is without its owner's core.
- **The technology bonus is per good**, and multiplies rather than adds:
  `metal_production`, `energy_production` and `rares_production`, each worth 5% a level in
  Their Finest Hour. **Crude oil has none**; `refinery_efficiency` is about turning oil into
  fuel, not about producing it. (measured)
- **Each province is scaled on its own and truncated**, first by the laws and then by the
  technology, and the truncated results are summed. Scaling the sum instead is out by a few
  thousandths for a large country. (measured: the United States matches only this way.)
- **"What the base IC needs"** is a fixed amount every country gets whatever it holds, and
  no modifier touches it: **5 metal, 10 energy and 2.5 rare materials**, and no crude oil.
  (measured) That is exactly what five IC consume in a day -
  1 metal, 2 energy and half a rare material each - and `base_values` in
  `static_modifiers.txt` gives every country `ic = 5`. That the one follows from the other
  is `inferred`: nothing else in the files is five of anything, and a mod that changes
  `base_values` would settle it.
- **A country that owns no province and is in no exile produces nothing at home**, not even
  the fixed amount: it has no day of its own in which production is collected. (measured: the
  countries that do not exist yet in 1936 all read zero.) **One that owns land or is in exile
  makes the fixed amount whatever it holds** - `CCountry::CollectDailyProduction` adds it before
  any province - and a capital it does not hold leaves its provinces cut off, so that is all it
  makes at home. `confirmed` (`reversing/findings/FINDINGS-surrender.md`, C.5; `war.md`, section
  14); (measured) every government in exile's `home` in the 1941 start saves is exactly 5, 10
  and 2.5. Until 2026-11-15 this said a country not holding its capital made nothing.
- **Money is home production too**, and has its own formula: `stockpile.md`, section 4.

**"Joined to the capital" is "in the capital's area"**, and an area was read on 2026-10-09:
the capital and every province the country controls that touches it through such provinces,
**each with more than one level of infrastructure** (`supply.md`, section 2). `confirmed`,
and (measured): until then this was a walk through any land the country controls, which
agreed for all but three of the countries that produce anything at the start. With the
infrastructure in it two of the three agree - Australia's and Canada's metal, 1.1 and 5.5 a
day over the save's, from mines behind land with one level or none - and the home figure of
metal is right for 78 countries of 78. Honduras's energy was half a unit over until
2026-11-14: its armament minister is declared a laissez-faire capitalist and then a resource
industrialist, and the original takes the first a block gives for a post, where this took the
second and its 5% more resources (`politics.md`, section 7). Every figure of home production
is the save's since: 78 of 78 countries for each good, 20 of 20 for crude oil.

## 4. Technology levels

A country's history file gives it a level in each technology with the technology's name:
`steel_production = 2`. Each level is worth the technology's effect once more. (measured)

Technologies are declared in the files under `technologies/`, one block each. A block mixes
several things: plain numbers that are what a level is worth, the statistics it gives each
unit type, what it unlocks, and what researching it needs.

*Ours:* only the plain numbers are read, into a total kept **beside** the country's
modifier rather than in it - the original keeps technology apart, and the IC calculation
reads it separately. Everything else in a technology waits for units and research.

## 5. What the savegame does and does not confirm

The tool that runs these comparisons is `tools/OpenHOI3.SaveCheck`.

**Agrees completely:** every province's owner, controller, cores, building levels,
production, manpower, leadership and victory points; every country's capital.

**Agrees, with a reason for what does not:**

- *Laws* - only 367 of 707 (546 since 2026-11-02, the engine's own new-game pass brought in - `politics.md`, section
  13 - with the AI's script still left out of this check), and **the cause is the original's AI script, not the files.**
  Both layers of the install give Germany the same seven laws, and the save has five
  others. `script/ai_politics_minister.lua` accounts for every one of them: its `Laws`
  function sets education to the highest investment, training to basic, industry to
  consumer products while at peace, sends a democracy at peace back to a free press and an
  open society, and steps the conscription, economic, press and civil laws up by one for
  as long as the next is allowed. Germany and Finland have `specialist_training` instead,
  and each has its own `CallLaw_training_laws` in `script/country/` that says so.
  (measured)
- **So the AI has already run when the first savegame can be made, more than once, and
  for the player's country too.** Germany was the player, was saved at hour 0 with nothing
  touched, and had moved two steps up in both conscription and economy - and that function
  moves one step a call. (measured) The laws a player sees on the first screen are the
  script's, not the history's. The comparison above applies the save's laws to our
  countries first, for that reason.
- *Government seats* - 780 of 780 for countries that exist, and 230 of 230 for those that do not,
  since the original's filling of empty posts at a new game was read (`politics.md`, section 7).
  This said 779 and guessed at the script's `OfficeManagement`, which runs for nobody before the
  first hour.

**Does not agree, and is not explained:**

- *Technology levels* - ~~28511 of 28987~~ **28,987 of 28,987 since 2026-11-02**: the 476 the
  save holds above the history are the original's head start for a new game, every country
  buying levels with its core provinces' leadership (`research.md`, section 9;
  `reversing/findings/FINDINGS-starttech.md`). What follows is the search that came before the
  reading, kept for what it tried. In the 476 the save is higher than the
  history files say, usually by one: militia and cavalry equipment, agriculture, supply
  production. Something grants them and it is not the history.

  **Looked at on 2026-10-10** (`OpenHOI3.SaveCheck techgap`), and half answered.
  **Measured:** in 453 of the 476 the save's level is exactly **the highest level of that
  technology that was due before 1936** - `start_year` for level 1, `first_offset` and
  `additional_offset` for those above it (`research.md`, section 2) - and in the other 23
  it is part of the way there, all in small countries. So a country is brought *up to
  date* in some technologies as a game is entered: agriculture to 3 from nothing for the
  Soviet Union, the two artillery technologies to 3 for Britain, destroyers to 2.
  **Which technologies is not found.** Some 12,000 other levels are as far behind and are
  left alone - Germany gets 22 and is left behind in 36. They come in whole sets by kind
  of unit - Germany's are light tanks, armoured cars, anti-tank, anti-air, destroyers,
  submarines, militia, and the three that allow mountain troops, engineers and carrier
  air groups - and they are not the kinds the country has: Germany gets armoured cars and
  has none, and Britain's submarines are left alone where Germany's are not. Tried and not
  it: the levels a country's own units or orders hold (29 of 476), the unit types it has
  (192, with over 500 raised that the save leaves), every level its history file gives at
  any later date (66), and its own model files (47). **It is not research either**: no
  technology of any country has progress or leadership on it in the save, and all 1316
  abilities are the history's, where a level researched would have raised one. And it is
  done for the country the player has as for the rest. That much looks like the hand
  that changes the laws before the first screen (section 5); how it is done wants a
  reading, and whether the player's country should get it wants the maintainer's word,
  as the laws did (`docs/compatibility.md`, entry 6). **Both came on 2026-11-02**: the
  reading found the engine's own new-game pass, which gives every country its levels and
  moves its laws, and the maintainer's word was "Yes everyone" - entry 6 reversed with it.
- ~~*Home production* for three countries: Canada's and Australia's metal, where the save
  has less than ours, and Honduras's energy.~~ All three agree: the first two since the
  capital's area was read (section 3), Honduras's since 2026-11-14 (`politics.md`, section 7).
- ~~*A country with no provinces has a full government in the save*, where the history files
  give it none.~~ It is the original's filling of empty posts as a new game sets a country up,
  not the AI (`politics.md`, section 7): built, 230 of 230 posts of the 23 such countries the
  save's (`OpenHOI3.SaveCheck`'s start-date run).

**Cannot be checked:** IC itself, which the original does not save. What it saves is what
industry *used*, which is never more than there was - and after `base_values` was added,
no country uses more than this project gives it.

## What is not established, in one place

- ~~how the original decides a province is **joined to the capital**~~ - read, 2026-10-09:
  `supply.md`, section 2;
- ~~why **Honduras's energy** is half a unit a day over the save's~~ - a minister's first type
  for a post is his (section 3);
- where the **476 extra technology levels** come from: they bring a country up to what was
  due before the start year, in technologies chosen by a rule not found (above);
- ~~**when and how often the AI runs** before the first hour, and why it runs for the
  player's country~~ - the engine's own pass and the set-up's call of the Lua's `Laws` for every
  country (`politics.md`, sections 12 and 13), the Lua's changes run after the two days; what
  repeats that call is still not found;
- what makes current production **step back up**, and how fast.

How a stockpile is filled at the start was on this list. It has been measured since:
`stockpile.md`, section 2.
