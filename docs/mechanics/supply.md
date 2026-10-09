# Supply: who draws from where

How supplies and fuel get from a country's stockpile to a unit in the field. Written as rules
this project can implement. No addresses and no offsets. Each rule says how sure it is -
`confirmed`, `likely`, `inferred` in the fact base's vocabulary - or **measured**, meaning
checked on 2026-10-09 against three savegames of one unmodded game played as Germany: 1
January, 8 January and 20 April 1936. Where this project chooses its own behaviour the
paragraph is marked **Ours**.

Sources: `reversing/findings/FINDINGS-supplynetwork.md` (all of it, read for this spec) and
`reversing/findings/FINDINGS-supply.md` (the daily pass, and how the distance is spread).

**Supply is cut in three, and this is the first piece.**

| | | |
| --- | --- | --- |
| 1 | **the network**: which depot every province draws on, how far it is from it, and what a province can pass and what it takes | **built**, sections 2 to 8 |
| 2 | **the daily pass**: supplies and fuel going out from the depot province by province, and a unit drawing on the province it stands in | not built |
| 3 | **the sea and the screen**: convoys to a depot across the sea, and the supply map mode | not built |

Until the second piece, **a unit still draws straight on its country's stockpile** wherever it
stands (`upkeep.md`, section 2). Nothing here changes what any unit gets. What the network
already does change is section 10.

Code: `lib/OpenHOI3.Sim/Supply` - `CSupplyNetworks` makes a `CSupplyNetwork` of `CSupplyArea`
and `CSupplyGroup`, `CSupplyRelations` says who is on whose side, `CSupplyFigures` has what a
province passes and takes. `CDiplomacyReader` in `lib/OpenHOI3.Content/History` reads who is
whose puppet.

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

The daily pass goes through the provinces farthest first and lets supplies go only from a
province to one strictly further out; that is the second piece.

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

`confirmed`. Crossing a province, a load loses

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

## Not built

- **The daily pass and the convoys**: the second and third pieces.
- **What a province's units ask for.** Read, for the second piece: the supplies and the fuel
  of `upkeep.md`, section 1, added up over the land units standing in the province whose owner
  is its holder or is not at war with it, over the wings based there, and over the fleets in
  port there. `confirmed`
- **The network is not made by the tick.** It is made when asked for and again when a
  province has changed hands. Nothing but the readout of a selected province and the save
  check asks yet.
- **The days out of supply**, and the message a province's first day out of supply raises.
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
  network, and not at the depot.
- **A second flag on a port** that one of the original's port tests skips a province for; it
  is behind `blockaded` (`modifiers.md`) and not behind anything here.
