# Undergrounds

A resistance network a country keeps in land its enemy holds: a building in the province, whose level is its
**strength**, which grows by the day, may be found by the garrison, and is spent on **partisans** or on **spreading**
to the province beside it. A government in exile can raise every ready one at once. Read out of the original on
2026-09-30 and 2026-10-14 (`reversing/findings/FINDINGS-occupation.md`, sections 2 and E; the dissolution
`reversing/findings/FINDINGS-surrender.md`, section D.2). `confirmed` unless marked.

This is the fourth of the four pieces occupation and revolt were cut into (`occupation.md`). It was built first without
the way an underground is ordered, by the decision of 2026-10-15 ("Undergrounds now, queue later"); **it is
ordered through production as any building is since the building lines that day** (`buildinglines.md`): the
production screen's `underground` button, enabled where the country may train one, and a line of it placed from the
deploy menu where the map shows it may go, green by the strength it would start with.

## 1. The building

**The underground is whichever building carries `local_underground`** - in both installs `underground = { ...
local_underground = 1 max_level = 1 repair = no ... }` (`common/buildings.txt`). A province holds it as any building:

- **its most level** says there is one, 1.0 at most;
- **its working level is the underground's strength**, from nought to 1.0;
- **its owner**, the country whose network it is;
- **its action**: 0 raise partisans, 1 spread, 2 build up strength. 2 until somebody sets it, and **never reset** - a
  new underground in a province takes up the last action set there.

`local_underground` puts the strength into the province's modifier, where nothing reads it. It is not repaired (`repair
= no`) and not shown as a building of the province.

## 2. A finished underground

What an underground built through production gets when it is finished, or when its line is placed on the map later:

1. its most level raised by one, to the building's most;
2. **its strength raised by its initial strength**, at most its most level;
3. the builder its owner;
4. `BUILDING_DONE` "$NAME$ has been built in $PROV$." to the player **only where he holds the province** - the
   occupier; the builder hears nothing.

**Its initial strength** (`UNDERGOUND_INITIAL_STRENGTH`, sic, 0.4; `MAX_UNDERGROUND_DISTANCE`, 1000):

- **an exile building in a province it owns**: `UNDERGOUND_INITIAL_STRENGTH`, flat;
- otherwise from **the nearest air base** of the builder - its own, then each ally's in turn - strictly nearer than
  `MAX_UNDERGROUND_DISTANCE`, the first of equals, by the original's straight line between provinces (in the units an
  aircraft's range is in, `fleetsandair.md`, section 3):

        r        = distance x 1000 x 1000 / MAX_UNDERGROUND_DISTANCE        thousandths of the reach, truncated
        strength = (1000 - r) x UNDERGOUND_INITIAL_STRENGTH / 1000          truncated

  0.4 at a base, nothing at the edge; **with no base in reach, nothing**.

**Where one may be built**, by a country at war that is not in exile (the production screen's button): a province
with none; **whose holder is at war with the builder**; whose owner is the builder's friend, military access counted;
and - unless the builder is a major or its provinces' IC, summed and unscaled, is above 100 - **whose owner is the
builder**: a minor of 100 base IC or less builds only in its own occupied land [exe: the original compares that figure,
kept in whole IC (`CCountry.BaseIc` here), with 100 - Germany's 197, Bulgaria's 26 read on the running game,
2026-11-16, `reversing/findings/FINDINGS-liveread.md`, section 108]. This took the figure as thousandths until then, 0.1 IC, so
every minor with industry built in its allies' land, and Bulgaria's production minister in July 1941 built an
underground on its first day where the original's found nowhere to build one.

## 3. The day

**In each country's day, just before the roll for a rising** (`rebels.md`, section 3), for each province of the
country's undergrounds - every province whose underground is its, in the order of their numbers - in turn:

1. **It grows**, every day:

        factor   = the province's local_partisan_support x 10 / 1000 + 1000
        gain     = factor x UNDERGROUND_STRENGTH_GAIN / 1000                truncated
        strength = min(strength + gain, its most level)

   0.01 a day in Their Finest Hour where nothing supports it; a policy's support of 19 nearly trebles it.
2. **It may be found**: one draw, `draw % 1000` below `round(suppression x UNDERGROUND_DETECT_CHANCE / 1000 /
   1000)` - rounded half to even - and it is **found**. `UNDERGROUND_DETECT_CHANCE` is 2, so a suppression below 0.25
   never finds one and three full garrison brigades, 0.6, find it one day in a thousand. A found underground does
   nothing else that day and is taken away; **the player is told**: `OURUNDERGROUNDREMOVED` "Our underground base in
   $PROV$ was destroyed" where it was his, else `UNDERGROUNDREMOVED` "We destroyed an underground base in $PROV$" where
   he holds the province.
3. **It is not needed**: otherwise, where the province's **owner holds it again**, it is taken away, telling nobody.
4. **Its action**, otherwise:
   - **0** - where it may raise partisans (below), the province is noted;
   - **1** - where it may spread (below), **it spreads at once**;
   - **2** - nothing.

**After the walk**, partisans rise from each province noted, in turn; then each underground found or not needed is
taken away: both levels nought, its owner none, the province's modifier made again.

**It may raise partisans** where it is the country's and its strength is **at least `SPAWN_PARTISAN_LIMIT`** (0.5);
**it may spread** where it is at least `SPAWN_UNDERGROUND_LIMIT` (1.0). Neither asks for an exile. So one built at a
base starts at 0.4, first raises partisans on its tenth day and pays its 0.5, and then every fifty days; on action 1
it must be full - sixty days from 0.4 - and pays all of it.

## 4. Partisans

From an underground, for its owner:

1. **Where they appear**: one draw picks one of the province's ways out; that neighbour, where it is in a real area
   (`rebels.md`, section 4) and **has the same owner** as the underground's province. Otherwise the first way out that
   passes both, and otherwise **the underground's own province**. Where that is in no real area, nothing - and nothing
   paid.
2. **The type** that province chooses with the underground's owner in the scope, **triggered-only types allowed** -
   which is how Their Finest Hour's `organized_partisans` and BlackICE's rise (`rebels.md`, section 4).
3. **A rising for the owner**: of `UNDERGROUND_SPAWN_STRENGTH` (0.2) brigades - a strength used as a count, so
   nought, which the rising lifts to one (`rebels.md`, section 5); in the faction of that type whose country is the
   owner, or a new one; **the army the owner's**; and the owner told `PARTISANS`.
4. **The underground pays `SPAWN_PARTISAN_LIMIT`**, never below nought.

## 5. Spreading

One draw picks one of the province's ways out. **That neighbour is taken only where it has no underground and the same
owner**; where not, **nothing happens and nothing is paid** - the next day draws again. Taken:

- its most level raised by one, its strength by `UNDERGROUND_SPAWN_STRENGTH` (0.2), the source's owner its owner, and
  it is first walked the next day;
- **then** the source pays `SPAWN_UNDERGROUND_LIMIT`, never below nought.

Nothing asks who holds the neighbour: a spread into land its owner holds is taken away by the next day's walk,
silently, and the 1.0 is lost.

## 6. Orders, and Organized Revolt

| command | valid where | does |
| --- | --- | --- |
| change the underground's mission | a province | its action set: 0, 1 or 2 |
| spawn partisans | the province's underground is its owner's and may raise partisans | partisans from it, **at once** |
| spawn an underground | it may spread | it spreads, **at once** |
| Organized Revolt | the country is in exile | partisans from each ready one of its own land |

The province view gives the first three (`provinceview.md`, section 5).

**Organized Revolt** - the politics screen's `revolt`, "Organized Revolt!", shown only for a government in exile - may
be pressed where one of its undergrounds in a province it **owns** is **strictly above** `SPAWN_PARTISAN_LIMIT`.
Pressed, it raises partisans from each underground in a province it owns strictly above the limit, in turn, each asked
again whether it may raise them. **Undergrounds in an ally's land are not counted.** Its tooltip:

- `WE_NOT_GOVERNMENT_IN_EXILE` "We are not a government in exile" for a country that is not;
- otherwise, over its undergrounds in provinces it owns (TOTAL) and those strictly above the limit (VAL):
  `UNDERGROUNDS_NOT_PREPARED` "None of our $TOTAL$ underground resistance cells are prepared yet" with none ready, else
  `UNDERGROUNDS_READY_TO_RISE` "$VAL$ of our $TOTAL$ underground resistance cells are prepared to rise up".

## 7. A change of holder

**When a province changes holder**, its underground is **dissolved** where it has one and its owner is the new holder
or **not an enemy of it**: both levels nought, its owner none (`war.md`, section 11). So land retaken by the owner's
side loses its underground at once.

## 8. On the map

**The revolt map mode** (`revolt.md`, section 7) paints a province with an underground red, (b, 0.3b, 0.3b), over
whatever its revolt risk would make it, where the province is not greyed.

## 9. Against the saves

No reference save holds an underground, so `OpenHOI3.SaveCheck underground <install> [occupier] [occupied]
[days] [seed]` is a trial: the occupier at war with the occupied and holding ten of its provinces away from its
capital, an underground of the occupied finished in each of the first three - to spread, to raise partisans and to
build up - and the days run.

On 2026-10-15, Germany and Poland for ninety days: the three start at 0.33, 0.32 and 0.33, from Poland's nearest air
base, and grow 0.01 a day. Braslaw raises partisans on its eighteenth day, paying 0.5 - **in Zarasai beside it, which
they hand back to Poland, and Zarasai's own underground is dissolved with it**, the province being its owner's again -
and again fifty days later. Szarkowszczyzna, building up, is full on the sixty-ninth day and stays so. Braslaw could not
have spread: its two Polish neighbours have undergrounds.

## What looks wrong and is built so

- A failed spread does nothing; a spread may land in its owner's own land and be taken away the next day.
- Organized Revolt counts an underground ready only above the limit, while the day and the buttons raise partisans at
  it; only Organized Revolt needs an exile, and it raises only those in the exile's own land.
- `UNDERGROUND_SPAWN_STRENGTH` is a count of brigades and a spread's strength.
- `BUILDING_DONE` goes to the occupier.
- The action is never reset.
- An underground no longer needed goes silently, and it is the province's owner that is asked.
- Growth comes before it may be found and before it is not needed.
- Being found is rounded half to even.
- `UNDERGROUND_PARTISAN_STRENGTH` is read and used for nothing.

## Ours

- **`--underground=TAG:province`** finishes one at once, as a finished line would, and the trial does the same - for
  trying one out without the days a line takes.
- **A country's undergrounds are walked in the order of their provinces' numbers**, which is the order the original
  rebuilds its list in each day; one a spread makes is first walked the next day.
- **A country's air bases are its provinces with an air base, in their numbers' order**, and its allies' after them
  in the order they are allied; the original keeps a list of each in the order they came.
- **The console's `alwayspartisants`**, which lets an underground raise partisans whatever its strength, is not built.

## Not established

- The original's own icon of an underground on the map (*inferred*: the action's frame for the player's, three more
  for anyone else's).
- `CCountry::GetRandomUnderGroundTarget`, which the AI's production minister builds them with.
- The AI's management of its undergrounds, which asks a unit's power and an unread test of an area.
