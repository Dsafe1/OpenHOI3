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
  it is absent from the home figure.
- **Straits join.** A crossing listed in `adjacencies.csv` connects two provinces for this
  purpose: Japan's islands, Denmark's and Sicily all count as joined, and their figures
  match only if they do. (measured)
- **`GLOBAL_RESOURCES`** is the country's modifier of that name - its laws and ministers.
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
- **A country that does not hold its capital produces nothing at home**, not even the fixed
  amount. (measured: the countries that do not exist yet in 1936 all read zero.)
- **Money is home production too**, and has its own formula: `stockpile.md`, section 4.

*Ours:* "joined to the capital" is a walk from the capital through adjacent land provinces
the country controls. The original decides it by its supply network, which this is standing
in for. They agree for all but three of the countries that produce anything at the start; where
they will differ is when a
network is cut by something other than who controls the ground.

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

- *Laws* - only 367 of 707, and **the cause is the original's AI script, not the files.**
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
- *Government seats* - 779 of 780 for countries that exist. The same script has an
  `OfficeManagement` function that picks ministers, which is the likely cause of the one
  that differs and of the next item; `inferred`, as that function was not followed through.

**Does not agree, and is not explained:**

- *Technology levels* - 28511 of 28987. In the other 476 the save is higher than the
  history files say, usually by one: militia and cavalry equipment, agriculture, supply
  production. Something grants them and it is not the history.
- *Home production* for three countries: Canada's and Australia's metal, where the save
  has less than ours, and Honduras's energy.
- *A country with no provinces has a full government in the save*, where the history files
  give it none - probably the AI's office management, as above. This project leaves the
  seats empty.

**Cannot be checked:** IC itself, which the original does not save. What it saves is what
industry *used*, which is never more than there was - and after `base_values` was added,
no country uses more than this project gives it.

## What is not established, in one place

- how the original decides a province is **joined to the capital**;
- where the **476 extra technology levels** come from;
- **when and how often the AI runs** before the first hour, and why it runs for the
  player's country;
- what makes current production **step back up**, and how fast.

How a stockpile is filled at the start was on this list. It has been measured since:
`stockpile.md`, section 2.
