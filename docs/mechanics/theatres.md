# Fronts and theatres

Stage 5's fifth piece, and **the military AI's first**: what the AI's theatre layer draws. Every block of a country's
land has its **fronts**, the runs of its provinces that touch another holder's land. Out of them each country's AI
makes its **theatres**, regions of its land each with a front to watch, merged and kept from one making to the next;
each theatre has a **headquarters**, a theatre-level unit it adopts or makes. The fronts are also what the foreign
minister's readiness for a prepared war counts (`foreignminister.md`, section 7), which until now waited for them: Danzig
and the Marco Polo Bridge among them. Nothing moves a unit yet: the next piece, the AI's unit agents, works inside the
theatres.

Read out of the original in `reversing/findings/FINDINGS-theatres.md`; the owner areas the fronts belong to are the supply
network's (`supply.md`, section 2, `reversing/findings/FINDINGS-supplynetwork.md`), and the strategy the theatres hang off
is `aistrategy.md`.

**Mod-able or fixed.** Every figure below is **exe-hardcoded** - the size test, the merge at thirty provinces, the half
overlap, the 150 behind the front and the headquarters' multipliers, the priority's weights, and the table of section
4.3, which names countries by tag. What a mod reaches is the map, infrastructure, the `hq_brigade` unit type (taken by
its key), the `unit_start_experience` modifier, and the texts `NAMED_THEATHRE`, `THEATRE_NAME` and `REGIMENT_NAME`.

Distances on the map are `16 x (dx*dx + dy*dy)` between two provinces' centres, dx taken the short way round the map,
and its integer square root where a distance is wanted - four times the map distance, the game's kilometres. "An edge"
is one of a province's edges in their order (`movement.md`, section 2); **an impassable edge** is one of kind 3.

## 1. When theatres are made

1. **A new game makes every country's theatres in its first hour** - the one hour of the two days a new game runs
   (`tick.md`, section 6) - country by country in their order, each from nothing. So their ids run up the countries.
2. **Every hour, after the country's units**, for each country taking part:
   - where its theatres are **marked**: in a running game, a country with theatres has them **refreshed** (section 8);
     otherwise - a new game, or a country with none - they are made from nothing;
   - otherwise they are **looked after** (section 9), with the country's own hour of the day - its number mod 24 - doing
     the most.
3. **Marked** are: every country as the game is made; the old and new holder of a province that changes hands, and
   every owner of a province beside it; a country whose strategy is made again after its ten days (`aistrategy.md`,
   section 1); and one whose prepared war on a neighbour is dropped (`aistrategy.md`, 9.4).
4. **Made from nothing at once** for a country nobody plays, in a running game, wherever its strategy is made again at
   once: a war joined or left, a faction joined, a peace (`aistrategy.md`, section 1); for a played country there, only
   refreshed.

## 2. An area's fronts

An **area** is the supply network's block of one holder's land (`supply.md`, section 2). Its **fronts** are made with
it, from its provinces in their order: a province **on a land border** - by **any** edge, an impassable one included, a
land province with another holder - and in no front yet starts a front, which takes it and then, edge by edge, each
neighbour of the same area on a land border and in no front yet, each with its own neighbours before the next. So a
front's provinces are in that walk's order.

(The original keeps a second list of fronts by the sea, coastal provinces by their sea's regions. Nothing this piece or
the readiness test reaches reads it; it is not built.)

## 3. A country's fronts of an area

For a country, an area's fronts are cut again by **whom each province faces**:

1. For each front and each of its provinces P in order: over P's edges **not impassable**, each land neighbour held by
   another than P's holder gives P its **enemy** - the first met, replaced by a later one where the later is front-worthy
   (section 4) or the one held is not. Where the neighbour's holder is not the country and is **at war** with it, or
   either is the rebels, P is **hot**, and P's edges are looked at no further. A province on a land border only by an
   impassable edge faces nobody.
2. P joins the country's fronts already made that are **hot with it, or face its enemy**, and that P **touches** (an
   edge not impassable to a province of them). None: a new front of P, its enemy and its heat, at the end. One: P goes on
   its end. Several: each, from the last found back, is put on the end of the first found and taken away, the first hot
   if either was; then P goes on the first's end.
3. Each hot front is given the area it **faces**: the area other than its own that the most of its provinces' edges
   (not impassable) reach, the first of equals; then each hot front, in order, is put on the end of the first other hot
   front facing the same area, and taken away.

## 4. A front-worthy enemy

Whether a country - the **other** - is an enemy the country draws fronts against, in order:

1. itself - no; a subject - the answer for its overlord;
2. **a war candidate** - an enemy; or not a friend (`aistrategy.md`'s friend, without military access) and either at war
   with a country of the other's faction or the target of a prepared war - **yes**;
3. a friend (with military access) - no; the country a subject whose overlord is allied to the other - no; the target of
   a prepared war - yes;
4. **the country in no faction**:
   - Lithuania: Switzerland and Germany no, the Soviet Union yes; Japan: CHC, CHI, CGX, CSX, KOR, MAN, MEN, SIK, CXB, CYN
     yes;
   - the country at war with a country in a faction - no;
   - the other in a faction other than the democracies' - yes;
   - the other Japan or Italy and no subject - yes;
   - otherwise yes where `floor((sqrt((d*d/1000 + d*d/1000) / 1000.0f) + 0.0005) * 1000) >= 180000`, d being the
     country's alignment x less the other's;
5. **the country in a faction**: the other in another faction - yes; then by the country's tag, no to anyone in its own
   faction, and yes to: the Soviet Union - LIT, ROM, POL, SPA, SPR, ETH, SWE, HUN, BUL, TUR, YUG, SLO, CZE, AUS, MAN,
   JAP; Germany - HOL, BEL, LUX, POL, LAT; France - Italy where in another faction, Belgium and Luxembourg; Belgium -
   Luxembourg; Britain - Italy; Japan - as in 4; **France's Belgium and Luxembourg and Belgium's Luxembourg** only while
   the other is not the country's subject, Germany owns land and is outside the country's faction, and Germany is **not**
   at war with the other. Anyone else - no.

## 5. Theatres made from nothing

For each of the country's areas, in the network's order, with a **leftover** of its provinces and a list of the theatres
made for it:

### 5.1 A front of its own

Each of the country's fronts of the area (section 3) facing a front-worthy enemy other than the rebels: **home** is
1000 x the provinces of each distinct area its provinces lie in, **spill** the same for each distinct area beside them,
by edges not impassable, **held by its enemy**. Where spill is **8000 or more**, or `spill x 1000 / home` is **250 or
more** (-1 where home is 0), the front is a new theatre's: the theatre takes the next theatre number, the front (5.6) and
its heat, and the front's provinces leave the leftover and the area's fronts.

### 5.2 Four seats

For **Germany**, Paris (2613) where fewer than two theatres were made for the area, then Warszawa (1928) where exactly one
was; for **the United States**, San Fransisco (3658), then Washington D.C. (4530). Each only where the province is in
this area, held by the country and not a front province (`aistrategy.md`'s, without units); a new theatre, not hot, the
province its **key province** and one of its provinces, and gone from the leftover. (In Their Finest Hour only the
United States' two are ever met on the start date.)

### 5.3 The whole area

Where the leftover is still the whole area, one theatre for it, with all its provinces.

### 5.4 The rest

Otherwise, while something is left and the last pass placed something:

- **nearest**: each leftover province goes to the made theatre whose nearest province - among those of its fronts that
  are hot or face a front-worthy enemy, and its key provinces - is nearest it, the first of equals, **only where it
  touches** one of that theatre's provinces;

then, the same way, **touching**: each leftover province goes to the first made theatre it touches.

### 5.5 The fronts left

Each of the area's fronts that has no theatre, in order: its first province stays with it; every other province leaves
whatever theatre it is in, and where that is not the first's theatre, goes to a new front facing the same enemy, kept
per theatre. The front, with what is left, is the first province's theatre's. Then **in the order of the theatres'
numbers**, each such front is given to its theatre; one whose provinces no theatre took is dropped.

So a theatre's own provinces are those on no front of it, and each front holds its own.

### 5.6 How a front is given

Its provinces are put back in a **chain**: while any is left, the start is the left one with fewest neighbours among the
front's provinces (by edges not impassable, in its own area) - the first under two taken at once, else the first with
fewest - and from it a walk, edge by edge, onto each of the front's provinces in the same area not yet put back, each
with its own before the next. Each of them is the theatre's, and the front is its.

## 6. Merged

Each theatre's **areas** are those its provinces lie in, and **the largest** the one with most provinces, the first of
equals. Then, while any theatre is made, the one with fewest provinces, **S**, the first of equals, looks at every
other, **O**, as a **candidate** where

- S has fewer than **30** provinces;
- or both have the same largest area, S faces a front-worthy enemy, and **O faces exactly those** - as many, each found -
  or **their fronts meet**: an end province of one of O's front-worthy fronts touches an end province of one of S's.

(A played country at war passes the first test too once it has made more theatres than it held.) The candidate with
the least **1000 x distance + 1000 x (S's and O's provinces)** wins, distance being the integer square root of the least
squared distance between their provinces. **No candidate: the merging is over.** Otherwise S's fronts go to the winner
as they are, the winner hot if one was, then S's own provinces, and S is gone.

## 7. Set beside the old

Each new theatre N, in order, is set beside the country's old theatres: the **overlap** with an old one is `shared
provinces x 1000 / the old one's provinces`, and the first old one to reach **500** is taken. Taken, it takes N's fronts,
provinces, key provinces and areas, **keeping its own number and headquarters**, and N is dropped; not, N stands. Each
old theatre left over is dropped, and **its headquarters disbanded**. A theatre's key provinces it no longer holds are
dropped. Then the theatres are looked after (section 9) as in the country's own hour, and the friends are told (8.1).

## 8. Refreshed

In a running game, each province the country holds that is in none of its theatres is placed: nowhere outside an area;
in an area of one province, the theatre whose headquarters commands a land unit of the country's standing there; else
the theatre most of its neighbours (by edges not impassable, the country's) are in, the first of equals; else the
theatre with the nearest province. Each theatre's provinces the country no longer holds leave it. Then each theatre's
fronts are made again: for each of its areas, the country's fronts of it (section 3), each keeping only the theatre's
provinces, given to it (5.6), the theatre hot if one is. Then the theatres are looked after as in the country's hour,
and the friends are told.

### 8.1 The friends told

At the end of a build (section 7) and of a refresh, where the country holds at least one province: every other country
it fights a war beside and is not at war with - the rebels never - that holds a province itself has each of its agents
bound to a theatre headquarters marked **area dirty** (agents.md, section 8) where any of the country's theatres is
connected to that agent's theatre - the two share an area, or the country's headquarters can reach the province theirs
stands in by land (the reach test of agents.md, section 6.4). That agent's next hour sets its area again, and so is a
full pass. So one province changing hands sets off a full pass for every friend near: on 17 July 1941, 12:00, Barce
passing from Italy to Britain gave Bulgaria's divisions in Serbia new places in the line and its 1-va Armija a new
command province, in all three runs of the original (`reversing/findings/FINDINGS-liveread.md`, section 83). The
original's list of those countries (`non_hostile_countries`) is made once a day; **ours** asks it as it stands.

## 9. Looked after, every hour

For each theatre in order:

1. **A headquarters**, where it has none: the first of the country's units, in the order they were made, that is a
   theatre-level land unit standing in one of the theatre's provinces and no other theatre's headquarters becomes it;
   failing that, **a new one** (section 10) where a province can take it.
2. **Its priority**, in the country's hour or while it is under 1: `P = sum over the distinct front-worthy enemies its
   fronts face of their IC x 1000`, + 10000 where its areas include the acting capital's, + 1000 x (its provinces / 30),
   + 1000 x what its headquarters' plan wants more of, where positive; at war with one of them, x 2 where the acting
   capital's area is in it, else x 1.5. Then **every** theatre's priority is `P x 1000 / the sum` - or, where the sum is
   not above nought (every hour but the country's own), `1000 / the theatres` each.
3. **In the country's hour**: for a country nobody plays, or before the game is running, each theatre-level unit that
   heads no theatre is **disbanded**; for a played country in a running game, each is given a theatre of its own.

## 10. A new headquarters

**Where.** Over the theatre's provinces in order, those with effective infrastructure (`supply.md`) of **at least
0.200**, none of whose neighbours (by any edge) holds a land unit, or a fleet carrying land units, of a country at war
with it, and with no land battle: the first such is the fallback, and each is scored `|150 - the mean distance to the theatre's front provinces|`,
each front province counted once per front and the mean cut to whole km - **x4** where it borders a front province,
**x2** where it borders another holder's land, **/3** where it is its holder's capital, **x100** where it is itself on a
front. The least wins, the first of equals. No such province: no headquarters this hour.

**What.** A theatre-level army with the next unit number, and one `hq_brigade` with the number after; the unit counter
moves on by two. The brigade's home is the province, its experience `unit_start_experience / 5` (at most 100), its
strength **a fifth of its most** and its highest that; its figures from the country's technology, its name by
`REGIMENT_NAME`. The army stands in the province and is named `NAMED_THEATHRE` with the name of the theatre's province of
most `victory points x 10000 + 5000 for a naval base + its IC` (the first of equals) - or, where that name is taken,
`THEATRE_NAME` numbered as a division is; it is given the best leader where the country assigns them, and looks for a
battle. The theatre takes it as its headquarters. **Every unit of the country left an orphan** - the units under a
theatre-level unit that was removed - is put under it.

## 11. Kept as the original does them

- **The alignment test of 4.4 counts the x difference twice and never the y.**
- **A province on a land border only by an impassable edge is on a front facing nobody** (sections 2 and 3).
- **Heat ends a province's look at its edges**, so its enemy is the last met before a hot one.
- **Every hour but the country's own, every theatre's priority is the same** (9.2).
- **An old theatre that loses the overlap is dropped with its headquarters**, a headquarters from the history among them
  (7) - and a theatre-level unit no theatre takes is disbanded (9.3).
- **Orphans go under the first new headquarters, whichever theatre it is** (10).
- **A merge does not put the fronts back in a chain** (6).

## 12. Ours, and not built

- **Areas are the supply network's, made whole when a province changes hands** (`supply.md`; `compatibility.md`, entry
  9): the original keeps them and their fronts province by province, so in a running game an area's provinces, and with
  them its fronts, can be in another order than the original's.
- **"The first of equals" by the order things were made** where the original goes by memory address: the area a hot
  front faces (3.3), a theatre's areas and its largest (6), and the theatre most of a province's neighbours are in (8).
- **The theatres are looked after as a step of the hour after every country's units**; the original does each country's
  right after its own units.
- **Not built**: the fronts by the sea (section 2); the AI's unit agents, which the original tells of every theatre made,
  kept or dropped and which put the history's other top-level units under a theatre - the next piece; and what the
  original does for a played country at war in section 6.

## 13. Checked against the start save

`OpenHOI3.SaveCheck theatres <install> <start-date savegame> [tag]` makes a new game as a player gets it and sets every
country's theatres beside the save's `area_theatre` blocks: each theatre's number, its fronts - their provinces in order,
enemy, heat - its own provinces in order, its key province and its headquarters; the headquarters made, by name, place
and brigade; and the game's next theatre number. Given a tag, that country's both ways.
