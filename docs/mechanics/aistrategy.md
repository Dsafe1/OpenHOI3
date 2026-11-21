# The AI strategy

Stage 5's fourth piece: **what each country's AI makes of every other** - whom it fears, courts, antagonises,
protects, takes as a rival or wants as a puppet, which provinces it means to take and to hold, and its personality.
The install's Lua asks it constantly - `CAIStrategy:GetFriendliness`, `GetAntagonism`, `GetThreat`,
`GetProtectionism` and `IsMilitarist`, in diplomacy, trade, licences, politics and five countries' own files - and
until this piece answered nought, which is why no AI offered a non-aggression pact, a guarantee or an alliance
(`foreignminister.md`, section 9).

Read out of the original in `reversing/findings/FINDINGS-aistrategy.md`; the ten weighted lists and their adders are
`reversing/findings/FINDINGS-triggereval.md` section 17, the walk of a condition section 12, the prepared wars
`reversing/findings/FINDINGS-foreignminister.md`.

**Mod-able or fixed.** Every weight, cap and threshold below is **exe-hardcoded**: no define reaches any of it. What a
mod reaches is the input - the `add_ai_strategy` effect, and the victory conditions of `common/faction_aims.txt`, which
the strategy walks (section 4). The base game uses no `add_ai_strategy`, so a new game's strategies are the engine's
arithmetic and the faction aims.

Figures are thousandths throughout, as the original keeps them; "rounded" is a single-precision number rounded to the
nearest, half to even, as the original turns a weight into a whole one.

## 1. When a strategy is made

A strategy is **made again from nothing** every time, never added to:

1. **As a new game is made**, for every country in their order, **before** the weather's hours and the two days a new
   game runs (`tick.md`, section 6). A strategy starts with no personality, so a country later in the order than
   another is still "not military" to it.
2. **Each day, before a country's daily pass**: in a running game, where ten whole days have passed since its last
   making; and, in any game, where it has never been made (a country that owned nothing until now).
3. **At once, in a running game**: for both countries of each pair a war newly sets against each other, and each pair
   a leaving country stops fighting; for a country joining a faction and each of its neighbours; for each attacker of a
   war that a peace ends. (And in the original when `add_ai_strategy` fires, and on a change of government it keeps in
   the history - section 12.)

**A strategy is not made** for the rebels, for a country that owns no province, or for one whose acting capital is in
no area of the supply network (`supply.md`, section 2).

## 2. In order

1. **The personality** (section 3).
2. The loaded strategies merged in - `add_ai_strategy`'s (section 10) and a scenario's, every weight added to the same
   key; neither is built, and nothing in the base game gives either.
3. **The walk of the victory conditions** (section 4) - **not for a played country**.
4. The lists (sections 5 to 9), each country's in the countries' order, then each scaled (section 9.3):
   `conquer_prov`, `defend_prov`, `threat`, `antagonize` and `rival`, `befriend` and `vassal`, `protect`.
5. **The prepared wars checked** (section 9.4).

**Each list is kept sorted, highest first**: adding to a key already there adds to its weight and moves it below any
equal; a total under 1 removes it; a new key with nothing to add is left out; a country never lists itself. **After each
add the list loses its lowest entries until it is within its cap**: `antagonize` 7, `befriend` 7 (14 for a
`diplomacy` personality), **`protect` 6 (12 for `diplomacy`)**, `rival` 4, `vassal` 10, `conquer_prov` 40 (60 for
`military`), `defend_prov` 1000, `threat` none. So the order the lists are filled in matters: what the walk added can be pushed out by what comes
after.

## 3. The personality

Where `add_ai_strategy` gave one it is that, and is then **worked out again and overwritten** (only a `static` strategy
keeps it, and nothing here makes one). For the ideologies the country's form of government accepts (`politics.md`,
section 1), each with a share of one over their number (a single-precision number, then thousandths) - twice that for the
ruling party's ideology - and its group's place on the alignment triangle, x and y:

| score | from a group at (x, y) |
| --- | --- |
| `military` | y / 2; where y > 0, plus (y + \|x\|) x 0.5 for x >= 0, or (y - x) x 0.33 for x < 0 |
| `capital` | -x for x < 0; (x + y) / 2 for x >= 0 and y < 0; otherwise nought |
| `diplomacy` | (200 - \|x\| in whole units) / 2 - y / 2 |

each times its share. The three sums' total, at least 1, decides **in this order**: `military` where its sum is at
least 0.4 of it, `diplomacy` where its is, `capital` where its is, and `balanced` otherwise. On Their Finest Hour's
triangle an all-fascist government is `military`, an all-democratic one `diplomacy`, and a mixed one mostly `balanced`;
the start save has 51, 27, 23 and no `capital`.

## 4. The walk of the victory conditions

For a country in a faction, every victory condition **chosen** for its faction (`factions.md`, section 8) is walked with
a weight of **200**, in a scope of the country alone; a country nobody plays only - the player's faction aims never move
his own lists. The walk goes down the condition: a block gives its weight to each child, `OR` shares it among its
children (at least 1 each), `NOT` turns what follows round, and a scope walks its children where it resolves. **A
region walks its children once for each of its provinces**, and `any_owned_province`, `any_neighbor_province`,
`any_neighbor_country` and `any_core` once for each of theirs, as each asks. The leaves that report:

- **`faction`**: not under a `NOT`, where the walking country is in the faction named and the condition does **not**
  hold in the scope - `conquer_prov` of the scope's province where it has one, else **`antagonize` of the scope's
  country**;
- **`controls` and `owns`**: not under a `NOT`, where the scope is the walking country and the condition does not
  hold - `conquer_prov` of the province named;
- **`owned_by`**, in a province: where the tag is the walking country and the condition does not hold, or under a
  `NOT` where the tag is another and it holds - `conquer_prov` of the province;
- **`exists`**: only under a `NOT`, where the condition holds - `antagonize` of the country named;
- **`war_with`** and **`undeclared_war_with`**: not under a `NOT`, where the scope or the tag is the walking country -
  `antagonize` of the other, **whether the condition holds or not**;
- **`alliance_with`**: where the scope or the tag is the walking country - `befriend` of the other by the weight and
  `antagonize` by minus it, the other way round under a `NOT`, whether it holds or not;
- **`relation`**: where its `who` or the scope is the walking country - `befriend` of the other, `antagonize` under a
  `NOT`, whether it holds or not;
- **`vassal_of`** the walking country, not under a `NOT` and not holding: `vassal` of the scope's country, with
  `antagonize` by the weight and `befriend` by minus it - the other way round for a `diplomacy` walker.

Their Finest Hour's aims are `<province or region> = { controller = { faction = <own> } }`: a `controller` scope has no
province, so **each such province held by a country outside the faction antagonises that country by 200**. That is
Britain's start - Romania, Greece, the United States, Bulgaria, Norway and Belgium, the `allied_*` aims' countries -
and the Soviet Union's, whose `comintern_east_germany` region puts Germany far above the rest. `has_building` and a
unit type also report in the original, into lists the military AI keeps; they are reported as unbuilt (section 12).

## 5. The provinces

**`conquer_prov`**: every land province, in the map's order, that somebody owns and the country does not hold, where the
country is `military` or at war with its owner, and its owner is the country or the province's area of the supply
network touches an area the country holds, and the province has a strategic resource: **50**. **Nothing the country
owns is ever added**, though the rule names it: the adder every rule goes through, the walk's included,
`CAIStrategy::AddConquerProv`, refuses a province the country owns (`reversing/findings/FINDINGS-laterstart.md`, section
14). So a country's own land held by an enemy is never on its list - (measured) Norway's Vemork and Poland's 2096 at both
1941 starts, China's 5420 at 1939, the Soviet Union's three at 1941.12.11, none on the saves' lists, where ours had them
until 2026-11-15; the 1936 start has no such case. `docs/enhanced.md` lists the dead half.

**`defend_prov`**: the capital, 200, where the country holds it; then each province the country holds, 50 for a strategic
resource and 20 for an air base built, where that is anything. **The adder takes no province the country does not hold**
(`CAIStrategy::AddDefendProv` tests the province's controller), so a government in exile has no capital on its list: at
the Day of Infamy France, Denmark, Norway, Belgium and the Netherlands, as the bookmark's start save has them (**measured**,
`Ireland1941_12_11_00.hoi3`). In the order of the country's list of the provinces it controls, which is the order
it took them (`supply.md`, section 2): at a new game of 1936 the provinces' numbers, at a later start the land the
history gave it after 1936 at the end.

## 6. `threat`

For the country C and every other country T that owns a province, is not C's puppet and is not in C's faction: C's
**strength** is its land brigades plus one, halved, and T's likewise; **T is passed over** where C has more than three
times its provinces and T under half its strength. Otherwise the weight is:

- **100** where T has a casus belli it could use on C (`war.md`, section 2);
- for T in a faction other than C's, by the faction's group: **fascism 400** for its leader (its first member), 200
  for another member; **communism 200** for its leader, 100 for another; **democracy** only where C is in a faction too,
  **300** where C's is fascist and 200 otherwise;
- where C owns one province and T more: 50 for each of T's provinces, up to four;
- **times 1.5** where T is `military` and nobody plays it;
- **where T borders C** (T's provinces beside land C both owns and holds - `CCountryFacts`'s neighbours): 25 x C's
  **border share** with T + 100, times 1.5 where the area of C's acting capital touches land T holds, and 1.5 again where
  C's own capital - not the acting one - lies beside a province T owns over anything but an impassable border. The border
  share is the part of the edges out of C's provinces that lead into provinces another country both owns and holds which
  lead into T's;

then times the strength ratio T over C, **at most 4**. A T that does not border C counts only where it is within C's
**reach** - the provinces C owns times 28 km, at most 1200 km, between the two acting capitals as the map measures
distance - and has both more provinces and more strength than C, and then by `(reach - distance) / reach`. **More than
1** is added, rounded.

## 7. `antagonize` and `rival`

For every T as in `threat` (strength here is brigades plus one, not halved):

1. C's `conquer_prov` weight over T's provinces. **Where T is somebody's puppet**, that goes to its topmost overlord
   instead - where it is anything and the overlord is no historical friend of C's, or C has a casus belli on it, or it
   is antagonised already - and T is done with.
2. That, times 6 over T's provinces (at most 6), at most 800.
3. For a `military` C bordered by T: 100 x the border share + 25, times 1.25 where C's acting capital's area touches T
   and 1.25 again where C's capital lies beside T's land.
4. **+50** where C has a casus belli on T; where C is T's own puppet and the power (section 7.1) is under 2,
   **300 x (2.1 - power)**.
5. Nought where T is a historical friend of C's, not antagonised already, and C has no casus belli on it.
6. Not allied and of the same ruling ideology: **-200** for a `military` C, -400 for `diplomacy`, -300 otherwise;
   allied: **-400**.
7. Of another ideology group: **+50**, and **+150** more where C's acting capital's area touches T, or T borders a
   `military` C.
8. T in a faction: **+400** where C is in one too, **-400** where C is in none.
9. Where all that is above nought and T has fewer provinces than C: + 100 x (1 - their ratio), times 0.75 for a
   `balanced` C, 0.5 for `diplomacy` and `capital`, whole for `military`.
10. **At least 1** is added, rounded.

So Germany's start list is France and six of the smallest faction members of the Allies and the Comintern, and Denmark,
Sweden and Ireland each antagonise the same seven Central American countries.

### 7.1 `rival`

**Only where T is evenly matched**: its power - the province ratio and the strength ratio T over C averaged - between
0.34 and 3. Then 100 where T borders C; otherwise, within C's reach, **(reach - distance) / 2** - halved, not divided by
the reach (section 11). Where that is anything: +50 at war with T, +50 with a casus belli on it, +100 where both are
`capital`; plus 25; then times the power where it is under 1, or over it where not, the power held between 0.34 and 3 -
so a rival is likeliest between equals. **At least 1** is added, rounded.

## 8. `befriend` and `vassal`

For every T that owns a province and is nobody's puppet or C's own:

- **small**: T borders C and owns fewer provinces than C's times 1.5, rounded half to even.
- Where T borders C and is not a clean historical friend (one that is not antagonised and on which C has no casus
  belli), and owns one of C's `conquer_prov`, it is **passed over** - unless it is C's puppet, or C is `diplomacy` and T
  small.
- Each country X in C's `threat` that owns a province, is not T, borders T and threatens C at least as much as T does:
  **X's threat times (T's provinces over X's + T's strength over X's) / 2**, at most 4 - T as a counterweight to C's
  threats.
- **+100** for the same ruling ideology, +50 for the same group, **-100** otherwise.
- Where small: for `diplomacy`, **`vassal` of T by 10 for each province C has more**; +10 a province where T is C's puppet.
- `diplomacy`: + half T's own threat.
- C in a faction: **+600** where T is in it, -200 where T is in another.
- A clean historical friend: +400.
- **At least 1** is added, rounded.

## 9. `protect`, the scaling and the prepared wars

### 9.1 `protect`

Only for a C of ten provinces or more. For every T that owns a province, whose power - T's brigades over C's (C's at
least 1, no plus one here) and T's provinces over C's, averaged - is under 0.66 and **that C borders** (C's provinces
beside T's land, the other way round from `threat`): +25 for a casus belli on it, +25 where it has a fifth of C's
provinces or fewer; where that is anything, +10 for a one-province T; added where at least 1, **six at most, twelve for a
`diplomacy` C** - not `befriend`'s seven and fourteen. So 60 at most - the start save's are 60, 50 and 25.

### 9.2 Historical friends

(`confirmed`, `reversing/findings/FINDINGS-aistrategy.md`, G.4a.) **The history's `historical_friend = TAG` appends the
tag to the country's list**, with no test of one already held, and a save keeps the list as `historical_friends = { ... }`,
written only where it is not empty, straight before `mobilize` (`savegame.md`, section 15). Only the strategy reads it:
the antagonism's two terms (section 8, steps 2 and 8) and the befriend builder's pass-over and +400 (section 9). **No
file of Their Finest Hour or BlackICE gives a country any**, and the reference saves hold none, so every term is inert
there; a mod's are read since 2026-11-21 (`CCountry.HistoricalFriends`).

### 9.3 The scaling

After its making, each of `threat`, `antagonize`, `rival`, `befriend`, `vassal` and `protect` whose first weight is above
400 is scaled so that it is 400: the others by the same ratio, truncated to thousandths, then rounded. No list in the
start save tops above 400.

### 9.4 The prepared wars checked

Each prepared war (`foreignminister.md`, section 5) is kept where its target owns a province or is in exile, is not C's
puppet and is not in C's faction, and - with no decision tied to it - C could declare war on it at all, the
declare-war row's gates with no goal (`war.md`, section 5), or - with one - **the decision's `potential`** holds for C.
Otherwise it is dropped.

## 10. `add_ai_strategy`

In the original the effect's block is a strategy of its own - `personality` and the ten lists, each `<list> = { id =
<tag or province> value = <n> }`. Firing it **adds every weight to the country's event strategy**, and a personality
where it names one, then makes the strategy again, which merges the event strategy in (section 2). **Not built**: the
block is read and its keys accepted, and the effect does nothing and is reported as unbuilt, as before; the base game and
BlackICE fire none.

## 11. Kept as the original does them

1. **A played country's lists leave out the victory conditions**, and are still made - the Lua reads a player's when an
   AI's offer is put to him.
2. **The rival's distance is halved and not divided by the reach** - where the threat's is. A rival 1,000 km inside a
   1,200 km reach scores 525.
3. **A neighbour threatens however small; a country further off only where it is bigger in provinces and in strength.**
4. **`protect` asks whether C borders T; `threat` and `antagonize` whether T borders C** - with an occupied border they
   differ.
5. **A new game's strategies are made before its two days**, and are what its first screen holds for the next ten.
6. **The personality is worked out over the government's accepted ideologies**, not the parties' standing: a change of
   ruling party moves it only by the doubled share.
7. **A prepared war is dropped as a strategy is made wherever the declaration could not be made that hour** - for want
   of diplomatic influence, or with neutrality above the ceiling for war - and a script that still wants it prepares it
   anew, with a new hour: so a target the readiness test waits three months on may be prepared afresh every ten days.
8. **`war_with`, `alliance_with` and `relation` in a victory condition report whether they hold or not**: an aim of
   being at war with a country antagonises it in peace and in war alike.

## 12. Ours, and not built

- **The ten days are asked once a day**, before a country's daily pass. The original asks them also at every diplomatic
  change - a trade, an influence, an agreement, a change of hands - so its ten-day making can fall within a day; here
  it falls at its start.
- **The border share is made when a strategy is**, from the map as it stands; the original's is made at the day's start.
- **The daily making comes before every country's day, not before each in turn**: the original makes a country's
  strategy between the day of the country before it and its own.
- **A faction's new member's neighbours are taken in the countries' order**; the original's list is in the order its
  walk of the provinces met them, which matters only to a personality not yet made.
- **A change of government does not make the strategy at once**: the original does where the change is kept in the
  history, and which of its callers do was not read; here it waits for the ten days.
- **Not built**: military access, the production shares and the brigades the AI aims for - the military AI's, read but
  not needed yet (`FINDINGS-aistrategy.md`, sections C and I); `add_ai_strategy` (section 10); a scenario's
  `ai_hard_strategy` and missions, there being one start (`CLAUDE.md`, *Settled decisions*); `static` and
  `consolidate`. A save's military access, shares, brigades and the two flags are kept as it holds them and written back
  (`savegame.md`, section 16), and nothing here acts on them; and in the walk `has_building` and a unit type, which are reported where met. Historical friends are
  read since 2026-11-21 (9.2).

## 13. Checked against the start save

`OpenHOI3.SaveCheck aistrategy <install> <start-date savegame> [tag | explain=<tag>:<tag>] [examples]` makes a new game as
a player gets it and sets every country's strategy beside the save's: the personality, and every list's entries in order
with their weights; given a tag, that country's lists both ways; `explain=` prints the figures one country's terms of
another are made of.

**Measured, 2026-10-16**, against `Germany1936_01_01_00.hoi3`: **the personality and all eight lists agree entry for
entry, in order and weight, for all 78 countries with a strategy** - the save's other 23 blocks are empty, the rebels and
countries that do not exist on the start date. Germany's leave the walk out, the save's player being Germany. Two things
were found by the check rather than by the reading, and then read: the region scope, without which the Soviet Union's,
Mongolia's and Tannu Tuva's `antagonize` lacked Germany, and `protect`'s cap, which the fact base had at seven and
fourteen.

**At the later starts, 2026-11-15** (`docs/mechanics/bookmarks.md`): the personality and all eight lists agree for **all
73 countries at both 1941 dates**, and at 1939.9.1 for 74 to 75 of 75, China the one left. The last two of 1941 were
found where the AI did not look: `protect` of Belgium - France's 25 where the save has 50, Britain's missing - was a
history war goal, Germany's `conquer` on Belgium, filed though Belgium was in exile by then; it made `has_cb = { type =
conquer }` hold in Belgium's scope, so `puppet`, France's and Britain's one usable casus belli on it, did not. The
original's replay never adds a goal whose `is_valid` fails at the date (`war.md`, section 3). And `conquer_prov` lost
the occupied countries' own land (section 5).

**With the foreign minister** (`OpenHOI3.SaveCheck foreignai`, 180 days): no Lua error; fifteen influences, three
alignments and eleven licences, and still no non-aggression pact, guarantee or alliance offered - which is what the
original did too: its April save of the Germany game and its March save of the Ireland game hold no pact of those kinds
made since the start, and every alignment and influence they hold is one ours makes too (Switzerland and Sweden to
Germany, Turkey to the Soviet Union; Germany influencing Finland, Britain South Africa and Canada), Britain's South
Africa on the very day; ours also has Germany influencing Belgium and Britain New Zealand.
