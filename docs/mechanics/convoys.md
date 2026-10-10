# Convoys: supplies out across the sea, resources home

How a country's stockpile reaches land the sea cuts off from its capital, and how what that land
yields comes back. Written as rules this project can implement. No addresses and no offsets.
Each rule says how sure it is - `confirmed`, `likely`, `inferred` in the fact base's vocabulary -
or **measured**, meaning set on 2026-10-10 against the maintainer's start-date savegame of an
unmodded game played as Germany. Where this project chooses its own behaviour the paragraph is
marked **Ours**.

Sources: `reversing/findings/FINDINGS-supplyconvoys.md` (who makes a convoy, its route, the
sharing out of ships, a new game, cut-off production; read for this) and
`reversing/findings/FINDINGS-convoys.md` (the delivery, the efficiency, the escorts wanted).

This is the third piece of supply; `supply.md` has the network and the daily pass it feeds, and
`supplymap.md` the map mode.

Code: `lib/OpenHOI3.Sim/Convoys` - `CConvoy` is one run; `CConvoyRoute` finds its way with
`CPathHeap`; `CConvoyRules` has what it wants and whether it can sail; `CConvoyMaker` makes and
removes one; `CConvoyDelivery` is a sailing; `CSupplyConvoys` and `CResourceConvoys` make and
unmake the two kinds; `CConvoyShips` shares the transports and escorts out; `CConvoyDay` is the
day. `CResources.GatherCutOff` in `lib/OpenHOI3.Sim/Economy` puts cut-off yield in its harbour.

---

## 1. In short

- **A convoy is not a unit.** It is a run between two harbours with a count of transports, a
  count of escorts, a way through the sea and a list of the goods it carries. `confirmed`
- There are two kinds here: a **supply convoy** carries supplies and fuel from the land a
  country's capital stands on to a harbour across the sea; a **resource convoy** carries crude
  oil, metal, energy and rare materials from land across the sea to the stockpile. `confirmed`
- **Nobody has to make them.** Every country has three switches, all on in a new game, by which
  its supply convoys are made and unmade for it, its resource convoys are, and its transports
  and escorts are shared out among them each day. `confirmed`; that the three bytes are the
  save's `auto_supply_convoy`, `auto_resource_convoy` and `auto_maintain_convoy` is `inferred`.
- A country has **free transports and escorts**, which are on no convoy, and gets its first ones
  for nothing when a game is made (section 3).

## 2. What a convoy wants

**Transports.** With `n` the provinces of its way, both harbours counted:

    length  = n x CONVOY_PATH_LENGTH_MULT              economy; 0.33
    harbour = the naval base at its end, in levels, / 10
    wanted  = whole part of ( harbour x length + 1 )

and for a trade convoy `length` is multiplied by `CONVOY_TRADE_WEIGHT_MULT` first. So a convoy
never wants fewer than one, and a long way to a large harbour wants many: Holland's to the East
Indies have sixteen. `confirmed`; the original reads the base's **most** level, and here a
building has one level.

**How much it carries** is `transports / wanted`, and it is **not held to one**: a convoy with
ships to spare carries more of what it loads. `confirmed`

**Escorts.** None while its owner is at peace. At war: half its transports and one, three more
while it has fewer transports than it wants, four more with above ten, and for a convoy a raid
sank a ship of five more for three days, three for a week and one for a fortnight. `confirmed`

**What a harbour can land in a day**: `NAVAL_BASE_EFFICIENCY` (military; 6) for each level of
its naval base, times one and its holder's `naval_base_efficiency`, from modifier and
technology. `confirmed`

## 3. Making one, and what a new game gives away

A convoy is made between two harbours for supplies and fuel or for the four resources, is given
its way (section 4), and goes to the end of its country's list.

- **In play it starts with no ship at all** and waits for the sharing out. `confirmed`
- **While a game is being made it starts with the transports it wants and one escort, out of no
  pool.** `confirmed`: the original keeps a flag for "a new game is being made" and tests it
  there.
- **And each country is then given free transports: one for every two of its base IC** - the IC
  of its provinces with no law, minister or technology on it - where it has none. `confirmed`
  that it is half of one figure and given only where the country has none; that the figure is
  the base IC is **measured**: 19 of 19 countries (Germany 72 of 144, the USA 128 of 256).
- **Free escorts come out as the number of its convoys**, for a country at peace: each convoy
  was made with one and the sharing out takes it back. **Measured**: 20 of 20.

## 4. The way

A best-first search from the harbour it loads in to the harbour it unloads in. `confirmed`

- From a province every edge is tried in order, but not one marked impassable. The province
  across has to be the far harbour, or sea that is no lake - and not one of the five straits and
  canals while it is shut to whoever holds the loading harbour.
- **A province is settled the first time a step reaches it**; a cheaper way found later does not
  replace the first. So the way is not always the shortest.
- A step costs the edge's distance. It costs **eight times** that out of a province with land
  beside it that an enemy of the owner holds - thirteen and twice its level where the province
  has a naval base, and five and twice the level more for an air base. And **five times** dearer
  again out of water where a raid fought a convoy in the last five days.
- What is looked at next is whatever has the least of `cost so far + 1.25 x the straight line
  still to go`, in whole pixels between the centres, the short way round the map.
- **Those are kept in a binary heap**, and of two with one weight the heap's own order decides
  which comes out first: a new entry changes places with the one above while it weighs
  **strictly less**; when the least is taken, the last entry is put on top and sinks, the left
  of the two below being looked at where it weighs **no more** than the right and coming up only
  where it weighs strictly less than the one sinking. An implementation has to be that heap.

**Measured**: the ways of all 261 convoys of the save, province for province - Holland's to the
East Indies through 79 provinces among them.

What attacks a convoy, and marks the water, is `navalmissions.md`, section 8.

## 5. Whether it can sail

A convoy does not sail without two provinces of way and a transport. And not unless: `confirmed`

- both its ends are in somebody's hands;
- its ends are in two different blocks of land (`supply.md`, section 2) - which is asked only of
  one that carries neither supplies nor fuel;
- each end is its owner's or a puppet's of its owner;
- every sea province of its way may be entered by its owner and still has water all the way
  back to the harbour it loads in.

A way that fails the last is looked for again the same day (section 10).

## 6. A sailing

Once a day, for a convoy that can sail. With `S` where it loads and `E` where it unloads:
`confirmed`

1. **What it loads from.** Where `S` is in the block of land its holder's capital stands on:
   the holder's stockpile. Anywhere else: **what lies in the harbour** (section 8).
2. Loading from a puppet's stockpile, the master's convoy leaves 1000 of every good behind.
3. Everything is multiplied by how much the convoy carries (section 2).
4. Loaded at home, no more of any good than `E` can land in a day.
5. Only the goods it carries.
6. **Supplies and fuel only as far as the land they go to has room**: fifteen days
   (`SUPPLYPOOL_DAYS`) of what was asked today of the **harbours** of every block of land that
   shares `E`'s depot, less what lies in those harbours. Where `E` is a harbour of the loading
   country's own home land and no depot, what `E` can land instead.
7. **Where it lands.** Loaded at home: in `E`'s pool, to be carried inland by the daily pass.
   Loaded across the sea: in the stockpile of whoever holds `E`.

The load is counted as convoyed out by the loader where the two ends are in different blocks,
as convoyed in by whoever holds `E`, and as passed through `E` that day.

**The day after a raid sank a ship of it** a convoy looks for its way afresh (section 4), which
the dearer water then turns away from where it was attacked. `confirmed`. *Ours:* the look is
taken whether or not the convoy sailed that day; the original has it at the end of the sailing,
and which of the ways out of that reach it was not read.

**So a depot across the sea is not filled directly.** The convoys land in the harbours; what a
harbour holds over its own days goes one province inward a day by the pass's third phase
(`supply.md`, section 12) until it reaches the depot, and from there outward again.

## 7. Supply convoys: where they run

For each block of a country's land that is not the one its capital stands on: `confirmed`

    D     = the depot the block's first province draws on - or, where another country holds
            that, the block's first harbour
    need  = the more of the supplies and the fuel asked of D today
            - what each harbour of the block that a supply convoy of the country's ends in
              can land
    while need > 0, or - at peace - while no convoy was found ending at D itself:
        the harbour of the block with no supply convoy yet that scores highest gets one:
            D scores 100000, any other its number of provinces from the depot
        it sails from the home harbour nearest it (below)
        need -= what the new harbour can land

- **At peace every harbour of the block ends up with a convoy**, the depot first and then the
  furthest from it: nothing in the loop ever says a convoy now runs to the depot. That is why a
  save's convoys end at harbours, several to a block. **Measured.**
- **At war only what the day's asking calls for.**
- A harbour as near the depot as the depot itself, and not it, is never chosen.

**The home harbour** is, of the harbours of the block the capital stands on, the one whose
water lies the shortest **straight line** from the water the far harbour opens onto - between
the two sea provinces, not by sea - and of two alike the first. It has to have water all the way
there.

**A block's harbours are in the order of their naval bases, the largest first**, and of two
alike in the order the block's provinces were found (`supply.md`, section 2). So "the first
harbour" is the largest. `confirmed`, and **measured**: with the harbours in the order they were
found 212 of the save's 261 convoys came out, and in this order all 261 - San Diego and not Los
Angeles for the Pacific, Alexandria and not Tel Aviv for Egypt's resources.

**Unmaking.** A supply convoy four days old or more is taken away, its ships going back to the
free ones, unless all of these hold: its end is the country's; something was asked today of the
depot it serves; it runs from the home block to another; and it ends at the depot, or the block
still needs more, or the block has less to spare than twice what its own harbour lands.
`confirmed`

## 8. Resource convoys, and where cut-off yield waits

**Where a day's yield goes** (`resources.md`, section 3, has what a province yields):
`confirmed`

- **home** - the province is in the block its holder's capital stands on, **or it draws on a
  depot that is**: into the stockpile, counted as produced at home;
- **cut off** - neither: it is put down in the province, and then everything waiting in the
  province is moved to the block's **loading harbour**: the harbour a resource convoy of the
  holder's already loads in there, and otherwise the block's first - its largest. A block with
  no harbour keeps it where it was made.

So by the end of each day a cut-off block's yield waits in one harbour, and a convoy takes all
of it at once (section 6).

**Making.** For each block that is not the home one and has a harbour: where anything waits in
its first harbour and no resource convoy of the country's loads in the block, one is made from
that harbour to the home harbour nearest it. And for each puppet whose capital's block no
convoy at all loads in and none of the master's land touches: one from that block's first
harbour to the master's nearest home harbour. `confirmed`; the puppet's part is `inferred`.

**Unmaking.** A resource convoy goes when both its ends come to be in one block, or the holder
of either end is no friend.

**It is everything in the province that moves, not the day's yield alone**, and a province's
pool is also the stockpile of a country whose capital the province is - whether or not that
country exists (`stockpile.md`, section 2). So the goods a country that does not exist was
given go to the harbour with the first day's yield and are shipped to whoever holds its
capital. `confirmed`, and **measured** on the start-date save, two days in: Jerusalem, Cairo,
Amman, Beirut and Damascus hold no resources where Baghdad - Iraq exists - holds its 450 of
crude oil and the rest, and Britain's crude oil is exactly 450.000 above what its own two days
account for. Reykjavik keeps Iceland's, because it is its own block's loading harbour and the
convoy sent there has nothing to load from the land and so no transport. **A country so
emptied goes on being charged** for consumer goods and supplies (`stockpile.md`, section 4).

It looks like an accident of the engine, and it was first built here with the yield kept
apart, which left those stockpiles alone. **The maintainer's word, 2026-10-10: follow the
original.** With one pool the harbours of the save come out to the thousandth (section 12);
kept apart, the money in every one of them was wrong.

## 9. Sharing the ships out

Once a day for a country, in the order of its list. Each convoy that has a way and can sail is
measured: what it wants (section 2), and for a resource convoy what the block it loads in yields
in a day. Then: `confirmed`

| | |
| --- | --- |
| **idle** | no way, cannot sail, or a resource convoy whose block yields none of the four |
| **first** | every supply convoy, **each put ahead of those before it**; a resource convoy worth 100 or more, ahead of all; one worth over 20, behind |
| **last** | a resource convoy worth over 1, ahead of the rest of the last; any other, behind |

A resource convoy is **worth** what its block yields in a day of money and the four resources,
a good counting double where that is more than a tenth of what the country takes in of it in a
day, and half where it is under a fiftieth.

In this order:

1. idle convoys give back every transport and every escort;
2. the last, from the back: a convoy gives back what it has over what it wants - or **all it
   has**, where the supply convoys together are short of more than the country has free, or the
   convoy itself has fewer than it wants;
3. the first, from the front: a convoy gives back what it has over what it wants, **and then
   all it has where it is left with fewer than it wants**;
4. the first, from the front, are filled from the free transports as far as those go;
5. the last, from the front, the same.

So a convoy is at strength or it is emptied and filled again in its turn - the supply convoy
made last is served first - and the poorest runs give their ships up whenever a supply convoy
goes short.

**Escorts.** Each convoy is due the escorts it wants times `owned / wanted` over the whole
country, at most all of them. One with more gives the rest back. **One with fewer is given
more only if a raid sank a ship of it within the last hundred days** - and such a convoy gives
none back - and a convoy with no transports gives its escorts back. So at peace every escort is
free, and a convoy never attacked gets none.

**Measured**: the transports and the escorts on each of the save's 261 convoys, and the free
ones of all 20 countries that have convoys.

*Ours:* what a country takes in of a good is what it produces at home, what convoys land and
what it is paid; the original counts trade and tribute as well.

## 10. A day

Twice in a country's day: `confirmed`

- **early, before its units draw**: every convoy that can sail does (section 6); one whose way
  is too short or broken has it looked for again; and one that has lost its business is taken
  away, its ships going back - a supply convoy whose far harbour is no longer the country's, a
  resource convoy whose loading harbour is neither the country's nor a puppet's, either with an
  end held by somebody at war with the country or by rebels;
- **late, after the day's production**: supply convoys are made and unmade (section 7),
  resource convoys are (section 8), and the ships are shared out (section 9) - each behind its
  switch.

The supply pass has run before either, so a harbour's convoy answers what was asked of it this
morning, and what it lands goes inland tomorrow.

*Ours:* the original runs a country's whole day before the next country's; here each step is
run for every country in turn (`tick.md`, section 3).

## 11. A new game

The original makes its first convoys **on the day it runs before filling the provinces**
(`supply.md`, section 16), manned for nothing, and on the second pass of the filling looks
again, hands out the free transports and shares the ships out. During the filling a supply
convoy sails on every pass and any other on the first only, and since the stockpiles are put
back each pass, the harbours across the sea are filled for nothing too. `confirmed`

*Ours:* a new game runs those two days whole, since 2026-10-10 (`tick.md`, section 6): the
convoys are made at the end of the first, manned for nothing, sail in the filling, and are
looked at again on its second pass. So a game starts with the capitals of countries that do
not exist emptied, as the original's does, and with the convoys the check below finds - which
runs the same first days the game does.

## 12. Checked against a savegame

`OpenHOI3.SaveCheck supplypass <install> <start-date savegame>` makes a game with the save's
units, laws and technology, runs a day, the filling and a day with convoys manned for nothing,
and sets the convoys beside the save's. On `Germany1936_01_01_00.hoi3`, 2026-10-10:

| | |
| --- | --- |
| convoys | 261 in the save, 261 here, **261 the same run with the same goods** |
| the way | **261 of 261 province for province** |
| transports on each | 261 of 261 |
| escorts with each | 261 of 261 |
| free transports | 20 of 20 countries |
| free escorts | 20 of 20 countries |
| money and resources lying in a province that is nobody's capital | **88 of 88 to the thousandth**: what waits in each harbour |
| the same in a capital somebody else holds | 28 of 49 to the thousandth, 49 within a fiftieth. What is left is a few hundredths a day in what seven countries that do not exist use; the AI changed the industrial policy of at least four of them after the last day was run, which this check does not put back |
| London's crude oil | to the thousandth, with the 450 that came from a country that does not exist |

`convoys=<tag>` after the save lists one country's differences in full.

What a start cannot show rests on the reading: the unmaking, a convoy emptied for being under
strength in play, escorts under attack, a puppet's resources.

## Not built

- **Attacks on convoys** are built since 2026-10-10 and are `navalmissions.md`, section 8: the
  raid, the escorts' defence, ships lost, and what follows here - the dearer water, the way
  looked for afresh, the escorts an attacked convoy wants and is given. Of them only a raid
  from the air is not built.
- **Trade and lend-lease convoys.** No trade is made, so no convoy serves one.
- **Building transports and escorts**: the production screen's two buttons, and the queue's
  third kind of line (`buildqueue.md`).
- **The convoy list of the production screen** and making, changing or cancelling a convoy by
  hand; the three switches are always on.
- **A harbour counted as nothing while its sea is shut to its owner**, which the original's
  ordering of a block's harbours allows for.

## Not established

- Which sea the original means by the one whose harbours count as nothing when its strait is
  shut.
- What the last call of the original's gathering of a day's production does for a subject, and
  the loop after it that moves money between countries.
- Whether the three switches are the three save keys in that order.
