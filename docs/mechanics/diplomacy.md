# Diplomacy: what two countries hold of each other

Relation, threat and neutrality - the figures every agreement, every war and the AI's opinion of an
offer are made of - how a new game sets them and how each moves. The first of diplomacy's pieces,
cut by the maintainer on 2026-10-12: this; the diplomacy screen; Lua; the agreements; war and peace;
factions. The alignment triangle threat is weighed on is `alignment.md`, brought into this piece for
it; the Lua a new game asks is `lua.md`.

Sources: `reversing/findings/FINDINGS-diplostate.md` (A the start, B threat, C relation and
neutrality, read 2026-10-12), `reversing/findings/FINDINGS-politics.md` (the daily pass, section 4),
`reversing/findings/FINDINGS-effectrun.md` (the effects), `reversing/findings/FINDINGS-triggerleaves.md`
and `FINDINGS-numtriggers.md` (the conditions). The start is checked against the maintainer's start
save pair for pair; the rest was read and set against nothing but saves of one game.

## 1. A pair

Every country holds one record towards every country, itself included, whether the other exists or
not. Two figures are held here:

- **relation**: thousandths, held between -200.000 and 200.000. It is meant to be the same both ways,
  and almost everything writes both sides; section 2 says where it is not.
- **threat**: thousandths, how much threat **the holder feels from the other** - the two directions
  are separate figures. Never below 0, never capped.

A save writes relation as whole points, cut toward zero, and threat to three decimals - which is why
a fraction of relation never survives a save in the original.

## 2. A new game

In this order, after the history and before the two days a new game runs (`tick.md`, section 6):

1. **Every relation is 0, every threat 0.**
2. **The history's `relation` entries** (`history/diplomacy`) set `first`'s relation towards `second`
   to `value`, clamped - **one side only**. An entry whose end date has passed sets that side back to
   0, not to what it was. Alliances, guarantees, pacts, puppets and wars at the start change no
   relation, threat or neutrality.
3. **The relations nobody gave**: for each country that owns a province, but REB, and for each country
   in the order they were declared - REB and itself included - the value is 25 where the other is REB
   or owns no province, and otherwise **what the install's Lua says**, `AI_GenerateNonHistoricalRelation
   (country, other)` in `script/ai_country.lua` (`lua.md`), in whole points. **Where the holder's
   relation towards the other is still exactly 0** it becomes that value times 1000, clamped - this
   side only.
4. **The month's pass before the first hour** (section 3) then makes every pair the same both ways,
   from the side of whichever country it reaches first.

The base game's Lua gives 25, +125 for an alliance (+75 more if both are at war), -200 for a war,
-100 for a truce, +10 for the same government, and +50 for the same faction or -25 otherwise - where
every country in no faction counts as one faction. So two unaligned countries start at 75 (85 of one
government), a country and itself at 85, a faction member and anyone outside it at 0 (10 of one
government), an alliance at 200 and a war at -125. **A guarantee changes nothing.**

That the history's relation survives step 4 is the original's order and nothing else: Germany comes
before Italy and the Soviet Union before China, so the month copies the history's side over the
Lua's. Kept so.

## 3. The month: relation's drift

Each month, for each country, for each country that owns a province: half of the pair's **drift**,
cut toward zero, is added to the relation and the result written to **both** sides, clamped. Each
pair is visited twice, once from each end, so a month moves it by about the drift. The drift, in
thousandths, in this order:

1. 250 where the pair has a puppet and its master (this sets, it does not add);
2. -100 where they are at war;
3. +50 where they are neighbours and neither is at war;
4. +80 where there is a guarantee;
5. +25 where one has military access through the other;
6. where the relation is above 100 and the drift so far is above 0, it becomes 0;
7. where neither is played by a person and one is the other's master, it becomes at least 2000 above
   what it was, and no less than 2000.

The terms that belong to one country of the pair - being its neighbour, its access - can differ
between the two visits. No define feeds any figure.

## 4. What else changes relation

Everything below writes **both** sides: it adds to the holder's relation, clamps, and copies the
result to the other.

| when | between | change | built |
| --- | --- | --- | --- |
| a land battle ends | each country on the winning side friendly to the province's owner, and not the owner, where the owner is an enemy of the losers' first country | +5 | yes |
| the `relation` effect | `who` and the scope's country | the script's | yes |
| a puppet made | master and puppet | +500, so 200 | with war and peace |
| `release_vassal` | the one releasing and the released | +500 | with war and peace |
| a government in exile liberated, the two in different factions | liberator and liberated | +500 | with war and peace |
| a war declared, new or widened | the two | -`WARDEC_WAR_DIPLOMACY_HIT` | with war and peace |
| a call to arms accepted | the ally and the enemy | -`WARDEC_WAR_DIPLOMACY_HIT` | with war and peace |
| a faction joined that brings its wars | the joiner and each new enemy | -`WARDEC_WAR_DIPLOMACY_HIT` | with factions |
| an alliance, a pact, access, an embargo, a guarantee | the two | their defines; `agreements.md`, section 5 | yes |
| a trade | the two | its defines | with the agreements' second half |

**There is no action that improves relations** in the original; `RELATION_INFLUENCE_COST` is read by
nothing. An influence or an alignment costs no relation: the two `DAYS_OF_*_RELATION_CHANGE` defines
are how many days one lasts.

## 5. Threat

**Nothing lowers threat over time.** It rises and falls only as the sources below say.

### 5.1 Spread to everyone

`amount`, `scale` (+1000 or -1000) and the feared country:

    base  = amount x (1000 + feared's THREAT_IMPACT) / 1000
    if the feared country is in no faction:  base = base x 1000 / 2000
    floor = base x 50 / 1000                                        ; base / 20
    for every country c but the feared one, the dead included:
        reach = min(28 x c's provinces, 4000)
        d     = the distance between the two acting capitals
        f     = reach > 0 ? max(1000 - d x 1000 / reach, 0) : 1001
        f     = 1000 where c is on the feared country's neighbour list
        t     = base x f / 1000
        t     = t x ideologyFactor(feared, c) / 1000
        t     = t x (1000 - c's THREAT_RESISTANCE) / 1000
        t     = max(t, floor)
        c's threat from the feared country += scale x t / 1000, floored at 0

Every division is a 64-bit one cut toward zero. `ideologyFactor` is the square of the distance on the
triangle from the feared country's position to c's group - its faction's group, or else the group
nearest c's own position - over `FACTION_THREAT_DIST`, times 1000, with no cap (`alignment.md`).

### 5.2 Spread over the map

From an `origin` province, over the ways between provinces:

    base and floor as above
    every province's distance = 99999;  no country is marked;  the feared country is marked
    origin's distance = 0;  a queue holds origin
    take the first of the queue; for each province next to it:
        d = its distance + the neighbour's size class (1 to 4)
        if the neighbour's distance <= d: next
        a = max(base - 130 x d, floor)
        c = the neighbour's controller
        if c is not marked, a > 0 and there is a controller:
            a = a x (1000 - c's THREAT_RESISTANCE) / 1000
            c's threat from the feared country += (a x scale / 1000) x ideologyFactor / 1000, floored at 0
            mark c
        the neighbour's distance = d;  if a > 0: add it to the end of the queue

A country is charged once, at the first of its provinces the walk reaches - the fewest steps, not
necessarily the shortest way. The floor keeps every amount above nothing, so the walk covers every
province it can reach, sea and all; a province with no controller is walked through and charged to
nobody, and `origin` is never charged. A country with a `THREAT_RESISTANCE` of 1 or more stops the walk
at its first province. This takes the floor before resistance where 5.1 takes it after. All the
original's, kept.

### 5.3 The sources

| what | how | amount | feared | built |
| --- | --- | --- | --- | --- |
| a unit put on the map | 5.1 | `REGULAR_CONSTRUCTION_THREAT_IMPACT`; a reserve's first brigade x (1000 + `RESERVES_PENALTY_SIZE`) / 1000 / 5 | its owner | yes |
| a battle started, of a kind the province had none of, where its controller is nobody or the owner the attacker's enemy | 5.1 | `LAND_`, `NAVAL_` or `AIR_COMBAT_THREAT_IMPACT` | the attacking unit's owner | yes |
| a strategic bombing started | 5.1 | `BOMBING_THREAT_IMPACT` | the bomber's owner | yes |
| convoy ships lost | 5.1 | (transports + escorts lost) x `THREAT_FROM_CONVOYS_MODIFIER` | the raider | yes |
| the `threat` effect, `who = all` | 5.1 | the value, its sign the scale's | the scope | yes |
| the `threat` effect, `who = TAG` | TAG's threat from the scope + the value, floored at 0, no modifier | | the scope | yes |
| the history's `threat` for a country, at a new game | 5.2 from its own acting capital | the figure | the country | yes |
| a spy's daily mission | 5.1 | `SPY_THREAT_INCREASE_ACTION` x level x the mission's share / 10 | the target | with espionage |
| mobilising / demobilising | 5.2 from its own acting capital | `MOBILIZE_THREAT_IMPACT`, +1000 / -1000 | the country | with politics |
| a war declared | 5.2 from the recipient's acting capital | `WARDEC_BELIGERENCY` | the declarer | with war and peace |
| a pact broken | 5.2 from the recipient's capital | `LEAVE_NAP_THREAT_COST` | **the recipient** | yes (`agreements.md`) |
| an embargo imposed / lifted | 5.2 from the recipient's acting capital | `EMBARGO_THREAT_COST`, +1000 / -1000 | **the recipient** | yes (`agreements.md`) |
| a war goal enforced at a surrender | 5.2 from the winner's acting capital | the casus belli's `threat` | the winner | with war and peace |
| an annexation | between the annexed and every other, both ways, -9999.000, floored at 0 | | | with war and peace |

### 5.4 The most-threatening country

Each country keeps the one it feels the most threat from: the countries in the order they were
declared, skipping itself, REB, any that owns no province, any it has an alliance with or is a puppet
or master of, and any in its own faction when it is in one; **the first is taken even at 0**, a later
one only with more - so of equals the earliest stays. It is worked out again each day before the
country's day, and whenever something diplomatic changes.

## 6. Neutrality

`neutrality` is the country's figure, thousandths, 50 until its history sets one. **Effective
neutrality** is neutrality less the threat it feels from its most-threatening country, held between
0 and 100.

- **Each day**: neutrality is held between 0 and 100, effective neutrality made, and then neutrality
  moves by the country's `neutrality_change` modifier (floored at 0, and not capped until the next
  day). In the base game only the spy mission's `spy_lower_neutrality` and the event modifier
  `prepare_for_war` carry it.
- **Each month**, but REB's: war exhaustion grows by its modifier, is capped by its maximum, and **at
  peace, all of it is paid back to neutrality at once** and set to 0 (`warexhaustion.md`, section 2;
  built since 2026-11-07).
- **The `neutrality` effect** adds its value, floored at 0, and makes effective neutrality again.
- No war, peace, puppet or annexation writes neutrality.

**The ceiling for war**, which the diplomacy screen shows (`diplomacyscreen.md`): a country may declare
war on another while its effective neutrality is at most

    the larger of the two threats between them, floored at 0
    + NEUT_REDUCTION_AT_CLAIMS where it may use the casus belli for its cores
    - NEUT_INCREASE_DIFF_CONTINENT where the two capitals are on different continents and they are not neighbours
    + its faction's neutrality
    floored at 0

## 7. Conditions and effects

| keyword | answers / does |
| --- | --- |
| `relation = { who = TAG value = N }` | the scope's relation with TAG is at least N; where the scope has no country, the controller of its province |
| `threat = { which = TAG value = N }` | the threat the scope feels from TAG is at least N |
| `is_threatend = N` | a country on the scope's neighbour list poses it a threat of at least N |
| `neutrality = N` | **effective** neutrality is at least N |
| `base_neutrality = N` | neutrality is at least N |
| `relation = { who value }` (effect) | section 4 |
| `threat = { who value }` (effect) | section 5.3 |
| `neutrality = N` (effect) | section 6 |

`relation`, `threat` and the all-organisation `popularity` take whole numbers: `threat = 50.5` is 50.

## Ours

- **The most-threatening country is worked out for every country each day**, where the original's
  daily update does it only for countries a flag marks (not read) and then after each diplomatic
  change. **REB fears nobody**: the start save has `---` for it where every other country has one,
  measured and not read.
- **A war is still given by hand** (`--war=`), so a war starts with none of war's relation or threat;
  the agreements' trade and aid, war and peace, factions, espionage and mobilising are later pieces and their
  sources above are reported as unbuilt where a script asks.

## Not established

- What reads a country's history `threat` after the new game's spread.
- The claims test inside the ceiling for war.
- The fractions of relation a save drops: here nothing is saved yet, so none are dropped.
