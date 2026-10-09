# Supply: who draws from where

How supplies and fuel get from a country's stockpile to a unit in the field. Written as rules
this project can implement. No addresses and no offsets. Each rule says how sure it is -
`confirmed`, `likely`, `inferred` in the fact base's vocabulary - or **measured**, meaning
checked on 2026-10-09 against three savegames of one unmodded game played as Germany: 1
January, 8 January and 20 April 1936. Where this project chooses its own behaviour the
paragraph is marked **Ours**.

Sources: `reversing/findings/FINDINGS-supplynetwork.md` (the network, read for the first piece),
`reversing/findings/FINDINGS-supplypass.md` (the pass, the unit's draw, what a unit carries and
what a new game does, read for the second), `reversing/findings/FINDINGS-supply.md` (how the
distance is spread) and `reversing/findings/FINDINGS-distribute.md` (where supplies are made).

**Supply is cut in three, and two pieces are built.**

| | | |
| --- | --- | --- |
| 1 | **the network**: which depot every province draws on, how far it is from it, and what a province can pass and what it takes | **built**, sections 2 to 8 |
| 2 | **the daily pass**: supplies and fuel going out from the depot province by province, and a unit drawing on the province it stands in | **built**, sections 11 to 17 |
| 3 | **the sea and the screen**: convoys to a depot across the sea, and the supply map mode | not built |

**A unit draws on the province it stands in**, and no longer on its country's stockpile. Until
the third piece the sea is stood in for: a depot that is no capital is fed from its holder's
stockpile as if a convoy with no limit ran to it (section 15).

Code: `lib/OpenHOI3.Sim/Supply` - `CSupplyNetworks` makes a `CSupplyNetwork` of `CSupplyArea`
and `CSupplyGroup`, `CSupplyRelations` says who is on whose side, `CSupplyFigures` has what a
province passes and takes. `CSupplyDay` measures a day, `CSupplyOrder` orders its provinces,
`CSupplyNeed` counts what units ask for and `CSupplyPass` carries; `CSupplyDraw` is a unit's
draw and `CSupplyCarry` what it brings; `CSupplyMaking` puts a day's supplies where the
factories are, `CSupplyBySea` stands in for convoys and `CSupplyStart` fills a new game's
provinces. `CDiplomacyReader` in `lib/OpenHOI3.Content/History` reads who is whose puppet.

---

## 1. In short

The land is cut into **areas**: blocks of provinces one country holds that touch one another.
Areas that touch and are **on one side** - a country's with its puppets' and its master's -
make a **group**, and a group has one **depot**: the capital, where it has one, and otherwise
its best port. Every province of the group draws on it. Land that is in no group with a depot -
no capital, no port - draws on a friend's depot where it lies beside a friend's land, and on
nothing where it does not. And every province is some **distance** from its depot, by the size
of the provinces on the way and the state of their roads.

## 2. Areas

`confirmed`, and **measured**.

**A province is in an area when somebody controls it and it has more than one level of
infrastructure.** Not the level it has left under damage: the most it can have. Exactly one
level is not enough.

- The sea is in none, and neither is land nobody holds.
- **It is who controls a province that counts, not who owns it.** Land taken from an enemy is in
  its taker's area from the hour it is taken.
- **Measured:** of the 4,400 provinces a save marks as out of supply, 4,383 are these - the
  sea, unowned land and land with one level or none - with none left over the other way.

**An area is as much of that as touches.** From a province, every province across one of its
edges that the same country controls and that counts is in the same area, and so on from
those. **Every edge joins**: a crossing over a strait does, and so does an edge nothing may
march over. So an island joined to the mainland by a crossing is one area with it.

- A province with too little infrastructure is not in the area **and cuts it**: land behind a
  belt of such provinces is an area of its own.
- The areas are found country by country in the order of the countries, each from its
  provinces in the order of their numbers, and an area's provinces are kept in the order the
  walk finds them: a province, then at once everything reached through its first edge, then
  its second. That order is what settles a tie in section 4.

**A port** is a province of the area that has an owner, a port onto the sea - which only a
province whose naval base stands on the sea has (`fleetsandair.md`, section 2) - and a naval
base above level 0. Two things are kept about an area:

| | |
| --- | --- |
| **landlocked** | not one of its provinces is a port |
| **may hold a depot** | it has a port, **or** it is the area its controller's capital is in and one of its provinces has industry |

**Areas beside it.** An area knows the areas it touches - across any edge of any of its
provinces - in the order it first meets them, going through its provinces in order and each
one's edges in order.

*Ours:* the original keeps the areas up to date as each province changes hands - taking the
province out of one, splitting what it leaves in two where it must, merging two of the new
holder's where the province joins them. This makes them all again, which comes to the same
areas; what could differ is the order of an area's provinces after a merge, and with it a tie.

## 3. Sides and groups

`confirmed`. The maintainer's account, 2026-10-09, before any of it was read: "puppets are part
of their masters network, unless they are not directly reachable (overseas or no direct
country with master, even if that country is an ally). Allies keep their own networks, but ofc
supply the allied units on their territory." The reading agrees with every word of it.

**On one side** are: a country and itself; a puppet and its master; two puppets of one master.
Never the rebels. **Not** two members of a faction, and not two allies.

**May use another's ground**: a country on one side with it, or one fighting a war beside it -
both among the attackers of some war, or both among its defenders - that is not at war with it.

**A group** is started from every area that is in none yet, country by country and area by
area in the order they were found, and takes in at once every area beside it that belongs,
and every area beside those. An area with no group yet **belongs** when, with *first* the
country that holds **the area the group was started from**:

- its holder is on one side with *first*; **or**
- it is landlocked, it is not the area its holder's capital is in, and its holder may use
  *first*'s ground.

So:

- **A puppet's land that touches its master's is one group with it** - Manchukuo with Japan's
  Korea, Mongolia with the Soviet Union.
- **A puppet the master does not touch is a group of its own** - the Philippines.
- **Allies stay apart**, each with its own depot, however long their border.
- The second rule is for a pocket: a block of an ally's land with no port, away from its own
  capital, is taken into the group it lies beside.

**Who is whose puppet** is read from `history/diplomacy`: a `vassal` entry makes its `second`
the puppet of its `first` from `start_date` up to, and not on, `end_date`. An `alliance`
entry is kept the same way, for section 5. Guarantees, pacts and relations are read past.
*Ours:* nothing makes or frees a puppet during a game yet, and nothing reads the dates again
after the start - Mengjiang becomes Japan's on 12 May 1936 in the files and stays nobody's here.

## 4. A group's depot

`confirmed` as read, and **watched in the original by the maintainer** in three cases that
turn on three of its terms (section 9); in the saves it shows only in where the supplies lie.

**A group of one area that may hold no depot has none.** Every other group - more than one
area, or one that may - scores every province of every area of it and takes the best. Of two
provinces with one score the earlier stays: the group's areas in the order they joined, each
area's provinces in the area's order.

A province's score, with infrastructure counted as 1000 for ten levels and a naval base as
1000 a level, and whole-number division:

    home  = the area the capital of the province's area's OWNER is in
            (the owner of the area's first province)
    near  = every province within two edges of this one, each once - which, for a province
            with any neighbour, includes the province itself; and sea provinces

    score = its most infrastructure
          + its naval base's level                 where its area is not home
          + for each province of near that is in an area of this group:
                that province's most infrastructure
              + THIS province's naval base's level  where that province's area is not the
                                                    area its own owner's capital is in
    score = score / (how many provinces are near + 1)

    + 2000         where the province is its controller's capital
    + 1000000      more, where that country is nobody's puppet

- **The capital of a country that is nobody's puppet wins outright.** Nothing else comes near
  a million.
- **A puppet's capital is worth two levels of port and no more.** Manchukuo and Japan's Korea
  are one group with no free country's capital in it, and its depot is the port of Hamhung,
  not Hsinking.
- **Away from such a capital the naval base decides**: a level of port is worth as much as the
  most infrastructure a province can have, and it is counted once for the province and once
  more for every province near it. So the depot of land across the sea is its largest port,
  or the port with the most land around it.
- A province's own infrastructure counts twice, once for itself and once as one of the
  provinces near it. It is how the original's list is made, and it is kept.

*Ours:* the original adds ten million more for the capital of **the first member of its
faction**. It is not built: the Sim knows a country's faction by name and not who came first,
and it cannot change which province wins while a group holds only one capital of a country
that is nobody's puppet - which is all the joining rule of section 3 allows.

## 5. A province's depot

`confirmed`, and **measured**.

A province with no owner, or in no area, **draws on nothing**. Any other:

- **where its area's group has a depot** - and has more than one area, or the area itself may
  hold a depot - draws on **that depot**;
- otherwise asks for one, as below, of the country that holds it.

**A province that draws on nothing counts its days out of supply**, which is what a save
writes as `out_of_supply_days`. *Ours:* the days are not counted yet; nothing reads them.

**Asking.** A country is asked about a province *for* a country, itself at first; the areas
already asked about are remembered, and an area is never asked about twice. With the area
the province is in, in this order, the first answer that is a province being the answer:

1. Where the area may hold no depot: each area beside it, in order, whose holder is a
   **friend** of the country asked for - that holder is asked about that area's first
   province, for itself.
2. Where the country asked for is a puppet: each area beside it that its master holds, the
   same way.
3. Where the area is the one the capital of the country asked for is in: **that capital**.
4. Where the country asked for is the country asked, and the area has a port: the province of
   the area with the most infrastructure in it and in the provinces across its edges that are
   in the area.
5. Each area beside it held by the country asked for, by a puppet of it, or by a puppet of its
   master.
6. Each area beside it held by a friend, as in 1.
7. Where the country asked for is the country asked: the area's first port. Otherwise nothing.

**A friend** is wider than "on one side": the same faction, an alliance, and a country
fighting a war beside this one that is not at war with it all count.

What that comes to:

- **The land around a capital draws on the capital** even with no industry and no port - step 3.
- **A block with no port, away from the capital, beside a friend's land draws on the friend's
  depot** - step 1 or 6. This is the other half of "allies keep their own networks": they do,
  and an ally's cut-off troops are let into them.
- **A block with no port beside nobody's draws on nothing.** **Measured:** the 17 owned
  provinces with roads enough that a save still marks as out of supply are all of this kind,
  and this gives every one - the three of the Canal Zone and the two of Panama it cuts off
  from Panama City, six islands with no naval base, six blocks on the mainland.
- Step 4 is nearly dead: an area with a port may hold a depot, so its group gave it one. It is
  reached only for an area asked about through another's asking, and may then name a province
  that is not that area's own depot. It is built as read. `likely` a slip of the original's.

## 6. How far

`confirmed`. A depot is a province that draws on itself, and it is 0 from itself. Every other
province that draws on it is as far as the nearest province beside it that draws on the same
depot, **and its own step further**:

    step = roads x size

    roads = 1   where its infrastructure is over a half
            2   over a quarter, up to a half
            3   a quarter and under
    size  = 1   where its bounding box's width and height together are under 30 pixels
            2   under 60
            3   under 90
            4   90 and over

The infrastructure is what the province has the use of - what is built, times one plus what
applies to the province and to its holder, held between a hundredth and one - and not the
most it can have. So a step is between 1 and 12, and **the cost is the province's that is
entered, whichever way it is entered from**.

- The distance is by the cheapest way, not the shortest: a province whose distance improves
  is looked at again until nothing does.
- Every edge counts, as in section 2. Only provinces that draw on the same depot are crossed.
- A province that draws on a depot no way leads to is **100000** away. None is, in any of
  the three saves.
- How many provinces the cheapest way goes through is kept beside the distance.

The daily pass goes through the provinces furthest first and lets supplies go only from a
province to one strictly further out: sections 11 and 12.

## 7. What a province can pass

`confirmed`; **measured** as far as section 9 says.

**A capital and every province that touches it pass without limit.** "Its controller's
capital, or a province with an edge to it" - the original works it out every day and a save
writes it as `capital=yes`. **Measured:** the same 402 provinces on 1 and 8 January and the
same 400 on 20 April.

Any other province, in a day:

    level    = its infrastructure in levels, 10 at the full        from the infrastructure it has
                                                                    the use of, as in section 6
    worth    = level x level                                        never less than 1
    worth    = worth x INFRA_THROUGHPUT_IMPACT                      4
    worth    = worth x OWNED_AND_CONTROLLED_THROUGHPUT_CAP_BONUS    2, where its owner holds it
    capacity = worth x (1 + its holder's supply_throughput modifier
                          + its holder's technology's supply_throughput)

Ten levels on a country's own ground pass 800 a day and five pass 200; five on occupied
ground pass 100.

## 8. What a load loses

`confirmed`, and **measured**: the save has fuel asked for along a line of provinces out of
Moscow as 5.250, 5.200, 5.150, 5.100, 5.050, 5.000.

**It is an amount and not a share**: so many units of supplies, and as many of fuel, whatever
the size of the load. A province asks for it on top of what it wants and it is taken off what
arrives (section 12). On arriving in a province, a load loses

    tax  = SUPPLY_TAX                                               0.1
    tax  = tax / 2                                                  where its owner holds it
    tax  = tax x (1 + mud + partisans)
    tax  = tax + its holder's technology's supply_transfer_cost     negative in the files
    never less than nothing

    mud       = muddyness x MUDDYNESSSUPPLYTAXMODIFIER (0.25)       not on frozen ground
    partisans = PARTISAN_EFFECT_ON_SUPPLY_TAX (0.1) x the province's local_partisan_support
                x (1 - suppression, held between 0 and 1)           only where its holder is
                                                                    at war with its owner

*Ours:* suppression is not built and counts as none, so the partisans take their whole share.
There is no weather yet, so the mud is nothing.

## 9. Checked against savegames

`OpenHOI3.SaveCheck supply <install> <savegame>` makes the network from the install with the
save's own owners, controllers and capitals, and sets it against what the save says. A save
does not write a province's depot; it writes three things the network decides.

| | 1 January | 8 January | 20 April |
| --- | --- | --- | --- |
| provinces the save marks out of supply, and ours that draw on nothing | 4400 and the same 4400 | the same | the same |
| provinces the save marks `capital`, and ours that are a capital or beside one | 402 and the same 402 | the same | 400 and the same 400 |
| provinces that passed more in the day than section 7 lets them | none of 2561 | none of 2528 | none of 2363 |
| convoys carrying supplies that do not run between land on the sender's capital and land on another depot | none of 261 | none of 232 | none of 109 |
| networks of more than one province where the most supplies lie at our depot | 104 of 113 | 107 of 115 | 110 of 121 |

The first two rows are sections 2, 3 and 5 province for province. The third is a bound and
not a match: nothing in these saves is at its limit. The fourth says no convoy runs inside
one of our networks - which a wrong grouping would make happen. **The fifth is all a save
has on which province a group's depot is**: where it is not the depot, the most supplies lie
at another port of the same network or where the garrison stands, a little ahead of the depot.

**And the maintainer watched the original pick a depot, on 2026-10-09**, which is the one
thing the saves could not show. Asked whether Manchukuo's depot at Hamhung matched what he
sees, he released countries out of Japan's Korea and looked:

> At the start Manchuko exists and is supplied from Hamhung. If I release only North Korea
> the depot changes to Pusan for Manchuko and North Korea. If I also release South Korea it
> switches them all to their own captial, even though Japan still has a port in Dailan.
> HOWEVER after letting the game run a few days the puppets and Dailan all switch to
> Pyongyang.

`OpenHOI3.SaveCheck supply <install> <savegame> release=PRK:JAP release=KOR:JAP` does the
same here - a country is given every province it has a core on that its master owns and
holds, and made its puppet - and section 4 gives **each of the three settled states he saw**:

| | he saw | ours |
| --- | --- | --- |
| as the game starts | Manchukuo on Hamhung | Manchukuo and Japan's Korea on Hamhung |
| North Korea released | Manchukuo and North Korea on Pusan | Manchukuo, North Korea and what is left of Japan's Korea on Pusan |
| both released, some days on | the puppets and Dalian on P'yongyang | Manchukuo, both Koreas and Dalian on P'yongyang |

That is three of the score's terms seen at work: Hamhung stops being the depot the day it is
in its own country's home, **because a port at home counts for nothing**; Pusan, four levels
of port on land that is not Japan's home, **beats two puppets' capitals**; and with no such
port left **a capital wins**, the best built of three.

**The days in between are not ours.** With `:free` after a release the country is given its
land and left nobody's puppet, and then each Korea draws on its own capital and Manchukuo
and Dalian on Hsinking - what he saw "even though Japan still has a port in Dalian". So in
the original the areas are grouped again when the land changes hands, **before** the new
country is its master's puppet, and not again until something else calls for it some days
later. `inferred`: it fits what he saw and it was not read. **It also foretold something he
had not said**: that with North Korea alone released the move to Pusan would not come at
once either. Asked, he answered "Only after a few days." *Ours:* the network is made whole
whenever it is asked for, so here a released puppet is in its master's group at once -
**kept so on purpose**, the maintainer's decision of the same day: `docs/compatibility.md`,
entry 9.

On the first day there are 260 areas in 248 groups and 234 depots. Germany's land draws on
Berlin and East Prussia's on Königsberg; the Soviet Union's and Mongolia's on Moscow;
Manchukuo's and Japan's Korea on Hamhung, the best port of a group with no capital of a free
country in it; the Philippines on Manila.

## 10. What the areas changed elsewhere

Three rules written before this asked "is this province joined to the capital?" and each
answered with a guess. **They now ask section 2**, through one routine:

- **Home production** (`resources.md`): a province's resources reach the stockpile on their
  own when it is in the capital's area. **Measured:** Australia's and Canada's metal, which
  were 1.1 and 5.5 a day over the save's, now agree - 78 of 78 countries, from 76. Both have
  mines behind land with one level of infrastructure.
- **`overseas`** (`modifiers.md`, section 2): a province is overseas when it is in another
  area than its controller's capital and on another continent. Nothing in the start-date
  check moved.
- **Placing a unit** (`buildqueue.md`, section 7): a finished unit goes in the capital's
  area, which is land the country *holds* - the guess had been land it owns and holds.

## 11. A day's pass: when, and in what order

`confirmed`. **Once a day, before any country's own day.** So a day runs: supplies are carried;
then, country by country, each unit loses its rotated men and draws (`upkeep.md`, sections 2
and 3) and the country makes its supplies and fuel; and what was made is carried tomorrow.

Before the pass every country's counts of the day start from nothing: what its ground's units
asked for, what left its stockpile for units, what it made away from its capital, what came
back, and what it paid and was paid for friends' units.

**Measured once, before the pass**, for every province of land somebody holds:

- what it can pass (section 7) and what a load loses on arriving (section 8);
- **what its units ask for**: the supplies and the fuel of `upkeep.md`, section 1, added up over
  the land units standing in it whose owner holds it or is not at war with its holder - neither
  being the rebels - over the wings based there, and over the fleets whose port it is,
  **wherever they are**. Fuel is counted whether or not anything moves. An air group aboard a
  carrier is counted nowhere.

**The provinces are taken furthest from their depots first.** Provinces equally far come in
no chosen order: in the order the original's sort happens to leave them in, which decides who
takes first from a province with too little. So the sort is done here as the original's is
done, step for step, on the provinces by number, the sea and number 0 with them. It is the
sort of the original's C runtime: a run of eight or fewer has its last picked out again and
again; a longer one is split about the middle one of its first, middle and last.

## 12. The pass

`confirmed`, and **measured** (section 17). A province holds supplies for `SUPPLYPOOL_DAYS` of
what its units ask for: 15 in Their Finest Hour. "Nearer" below is always a province across one
of this one's edges that **draws on the same depot and is strictly nearer it**. Supplies and
fuel are worked side by side, the same way but for one thing, and never mixed.

**First**, every province's figures of today become yesterday's and today's start from nothing:
what was asked of it, and what passed through it.

**Then, the furthest first**, each province. A capital only notes what it holds: a stockpile is
where supplies come from. Any other:

    need   = what its units ask for                             section 11
             taken for 1 where it is under that and the province holds under 5
    want   = 15 x need - what it holds                          never under nothing,
                                                                never over 2 x need
    asked  = want + everything the provinces behind it asked of it
    nothing asked: the province is done

    asked  = asked + the loss                                   section 8

1. **It takes goods** from each nearer province in the order of its edges, until it has what
   it asked for:

       offered = what that province held WHEN THE PASS LAST CAME TO IT
                 never more than it can pass in a day, never more than it holds now
       where yesterday that province passed less than was asked of it:
           offered = offered x (asked / what was asked of it yesterday)     at most all

   What is taken counts as asked of that province and as passed by it, and arrives **less the
   loss**.
2. **What it still wants is asked** of the nearer provinces in equal shares, each as far as it
   has room to pass more today.
3. **What is left after that** is asked of them one after another, each up to all it can pass
   in a day.

Steps 2 and 3 move a demand and no goods. Then it notes what it holds.

Four things in that are worth saying twice:

- **Goods move one province a day.** A province is visited before the one it takes from, so
  what it is offered is what that one held yesterday. Nothing but a capital hands on the same
  day what it was given.
- **Demand moves the whole way in a day.** By the time the pass reaches a depot's neighbours
  every want in the network has been passed in to them.
- **A province asks for two days' worth at most**, so one that is empty is fifteen days in
  filling, and a province nothing was asked of today is not noted, and offers tomorrow what it
  held when it last was.
- **The original's oddities are kept**, because who goes short on a full road is decided by
  nothing else: for fuel the test "was more asked of it than it passed" looks at today where
  for supplies it looks at yesterday; a share cut by one province's room stays cut for the
  provinces after it; and a province already asked for more than it can pass throws the
  difference back on the asker, who then asks it of the next.

**Last, the nearest first**, each province that is no depot's capital. One empty of both, and
one holding less of both than was asked of it, keeps what it has. Otherwise

    spare = what it holds - (what was asked of it today + 15 x what its units ask for)

and **all of it goes to the first nearer province in the order of its edges**, with no limit.
The nearer were visited first, so **what is spare moves one province a day** too. At a depot
that is no capital it goes home over the sea (section 15).

**A capital that draws on another's depot is drained by this**: its stockpile is "spare" beyond
its own garrison's fifteen days, and goes inward a province a day. **Measured:** Manchukuo's
capital, which draws on Japan's Hamhung, holds 7.8 supplies two days in, where a stockpile
starts with ninety days of what all its units use.

## 13. What a unit draws

`confirmed`. Once a day, after its men are rotated (`upkeep.md`, section 3).

**Where.** An army on the province it stands in. A wing and a fleet on the province they are
based in, wherever they are; an air group aboard a carrier on its fleet's port. A unit that is
nowhere yet - finished and not placed - on its country's capital.

**Whose depot.** Where the depot that province draws on is held by a country that is **no
friend** of the unit's (section 5), the unit is given nothing. Where it is held by a friend,
the unit's country **pays the friend** for what the unit asks: out of its stockpile into the
friend's,

    price = 1 + 0.25 x (1 - what the province holds / (15 x what its units ask for))

of supplies and of fuel apart - so a quarter over on an empty province and a little under on a
full one. What is paid is the friend's to supply: it counts as asked for by the friend's units.

**How much.** The province holds what it holds:

    where it holds less than its units ask for:
        the unit's share = what it holds x (what the unit asks / what all its units ask)
    given     = the least of that and what the unit asks
    received  = given / asked                                    kept by the unit until it next draws

A capital counts no need of its own, so a unit in one is given what it asks while there is any.

- What a unit asks is counted for **the country that holds the ground**, friend's payment
  aside: it is that country's supply share that has to make it (`upkeep.md`, section 8).
- **Fuel is given the same way but burnt only by a unit that is moving, and by a fleet that is
  not in its own port.** A unit standing still is told how much it would have had and the
  province keeps it. So a province holds fifteen days of fuel for the tanks standing in it.
- A fleet ordered to another port is seen to out of its country's capital on the way.

*Ours:* a unit on loan to another country, which the original bills to the lender, is not
built. The original's arcade mode, where every unit draws on its country's stockpile and none
of this runs, is not built either.

## 14. What a unit brings

`confirmed`. **A land unit entering a land province takes fifteen days of what it uses with
it**, supplies and fuel, out of the province it is leaving - as much of that as the province
has. Coming off the sea, or out of nowhere - a unit just placed - it takes them out of its
country's stockpile; but a unit from nowhere on ground it owns and another holds takes from
that ground's depot, and on anybody else's it comes with nothing.

That, and not the network, is what feeds an army that has walked beyond its supply lines: it
eats what it carried, for a fortnight, and then it starves.

*Ours:* units are not carried by ships or aircraft yet, so nothing is put ashore or dropped.
A dropped unit brings `PARASUPPLYPOOL_DAYS` in the original.

## 15. Where supplies are made, and the sea

**They are made where the factories are.** `confirmed`, and **measured**. A day's supplies
(`production.md`, section 4) and a day's fuel (`ic.md`, section 4) are shared out among the
provinces the country owns, holds and has a core on in its capital's area (section 2):

    share = the province's IC x (1 + its LOCAL_IC) / the IC of all of them     cut to a thousandth
    part  = share x what was made

into each province's own pool. The capital's part, and what the cutting leaves over, is in the
stockpile at once; **the rest reaches it by the last step of the pass, a province a day**. What
was made away from the capital counts as made and as gone.

**It is the land joined to the capital that shares, and land cut off from it gets nothing.**
A province in another area than the capital's is not on the list: its industry still counts
towards its country's IC, and the supplies that IC pays for are made in the provinces that are
left. This is the other way about from resources, which reach the stockpile on their own from
the capital's area and lie where they are dug in land cut off from it (`resources.md`).

**Measured:** Canada made 29.923 supplies and 9.000 fuel on the day of the save, and every
province with industry holds 1.137 and 0.342 for each level - and 3.441 and 1.035 where it has
three, which is 0.115 of each and not three twenty-sixths. Germany made 283.840 and 110.000:
Rostock, two levels of industry and no unit, holds 4.257 and 1.650, which is 0.015 of each -
two of **133**, where Germany holds 139 levels. The six left out are Königsberg's, beyond the
Polish corridor; and Königsberg's pool has 12.100 energy lying in it, which no province joined
to Berlin has. A week on, 582 provinces with no industry hold supplies nothing was asked of:
parcels on their way in.

**The sea.** *Ours, until the third piece:* a depot that is no capital - Königsberg, Hamhung,
every colony's port - is sent what was asked of it that day out of its holder's stockpile,
after the pass, and what it holds spare goes back to that stockpile, both with no ships and no
limit. In the original convoys carry it: into ports and not to the depot, several to a
network, as much as the ports, the ships and the room in the area allow
(`reversing/findings/FINDINGS-convoys.md`). So a country here cannot be cut off from its land
across the sea, and a small port feeds as large an army as a large one.

## 16. A new game

`confirmed`, and **measured**. The original does three things before a game's first hour:

1. **a day**, on provinces that hold nothing: every unit outside a capital goes hungry, and
   the day's supplies and fuel are made at the factories;
2. **the filling**: the pass of section 12 run as many times as the furthest province is far
   from its depot - never counted as more than 500 - **and twice more**, every unit drawing
   after each, and **each time every stockpile that is its own depot is put back as it was**.
   So the provinces are filled for nothing. A unit all of that has left with nothing is then
   given its fifteen days out of its stockpile (section 14);
3. **another day**.

Two things follow from the order, and both show in a save:

- **What the first day made away from the capitals is lost.** It is on its way in while the
  stockpiles are being put back. **Measured:** Canada's provinces hold 689.310 fuel two days
  in - the 581.580 it began with, 96.930 the filling put in Halifax, two days' refining at
  9.000, less 7.200, which is one day's refining outside Ottawa.
- **A stockpile that draws on another's depot is not put back**, and is drained during the
  filling as on any day (section 12).

*Ours:* **the filling is done, when a new game is made, and the two days are not** - a game
here starts on its first hour with nothing spent, as it has since `stockpile.md`, section 3.
It takes about a second and a half: 251 passes on the first day of 1936, where the furthest
province is 249 from its depot.

## 17. The pass, checked against a savegame

`OpenHOI3.SaveCheck supplypass <install> <start-date savegame>` makes the units as the save
has them, gives each country the save's laws, ministers and technology, the save's mud, and a
share of IC that makes the supplies the save says it made; then does what section 16 says the
original does - a day, the filling, a day - and sets every province's supplies and fuel beside
the save's: what lies there, what was asked of it today and yesterday, what passed through it
today and yesterday.

| | |
| --- | --- |
| networks where every province agrees in every figure | **124 of 234** - Sweden's 210 provinces, Turkey's 195, Norway's 108, the Philippines' 85, Persia's 84, Saudi Arabia's 79, Siam's 61, South Africa's 42 |
| Canada's | everything but what its guessed share of IC makes: a line of twenty provinces from Ottawa to Halifax, each asked 0.050 more than the one behind it and each holding one day's load in passing |
| provinces where the supplies lying there are something, here or in the save | 627 of 3666 the same to the thousandth, 1129 within a fiftieth |
| the same for fuel | 530 of 694 |
| Germany's stockpile, followed on | 8496 on the fifth day and 8743 on the eighth; the later saves have 8493 and 8758 |

**What differs is what goes in and not the pass**: where a whole country's units, laws and
production are the save's, its whole network agrees. Three things are not the save's here.

- **Leaders.** The original has given most units one, and a leader changes what a unit uses
  (`upkeep.md`, section 1). There are none here, so Germany's provinces are a few hundredths
  out everywhere.
- **Units on the move.** The original's AI has some: fuel is asked for in 145 provinces where
  nothing here burns any.
- **The share of IC the supplies were made with.** The AI moves it after the day is run, so
  it is guessed from what the save says was made, and for 25 of 101 countries no share makes
  exactly that.

Run it after touching `lib/OpenHOI3.Sim/Supply`. `network=<depot's number>` after the save
prints one network province by province, `country=<tag>` one country's day, and `days=<n>`
with it follows the country on.

## Not built

- **Convoys and the supply map mode**: the third piece. The sea is stood in for (section 15).
- **The days out of supply**, and the message a province's first day out of supply raises.
- **A unit on loan**, **arcade mode**, and **units carried by ships and aircraft** (sections 13
  and 14).
- **Leaders**, which change what a unit uses.
- **Suppression** and **the faction leader's capital** (sections 8 and 4).
- **A puppet made or freed during a game**, and an alliance likewise.

## Not established

- ~~**Which province a group's depot is**, beyond where the supplies lie.~~ The maintainer
  watched three cases in the original and the score gives all three (section 9). What that
  leaves unseen: the million for a free country's capital against a very large port, and the
  faction leader's ten million.
- **When the original groups its areas again.** After a release it does so before the new
  country is a puppet, and then not for some days; what calls for it then was not read.
- **The order of an area's provinces after the original has merged or split it**, which could
  settle a tie in the score differently from making the areas afresh.
- **What else makes the original group the areas again** besides a province changing hands:
  fifteen places call for it and one was read.
- **How a depot across the sea is filled.** A save's convoys end at ports, several to one
  network, and not at the depot. The last step of the pass would then carry what a port holds
  spare inward to the depot; the convoys themselves were not read for this.
- **Whether the fuel's different test in the pass is meant** (section 12). It is what the
  original does.
- **A second pool of the original's** that the pass and the unit's draw both add to, and
  nothing read explained.
- **A second flag on a port** that one of the original's port tests skips a province for; it
  is behind `blockaded` (`modifiers.md`) and not behind anything here.
