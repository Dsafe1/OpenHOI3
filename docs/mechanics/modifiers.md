# Modifiers: what they are, where they come from, and how they add up

How the original turns laws, ministers, buildings and the rest into the numbers every other
system reads, written as rules this project can implement. No addresses and no offsets.
Each rule says how sure it is - `confirmed`, `likely`, `inferred` in the fact base's
vocabulary - or **measured**, meaning counted here against the files of a Their Finest Hour
install on 2026-10-07. Where this project chooses its own behaviour the paragraph is marked
**Ours**.

Sources: `reversing/CLASSES.md` (*Modifiers*, *buildings.txt, key by key*),
`reversing/findings/FINDINGS-revolt.md` (section 3),
`reversing/findings/FINDINGS-leadership.md` (sections 7 to 9),
`reversing/findings/FINDINGS-countrymodifier.md` (all of section 4 to 6 here),
`reversing/findings/FINDINGS-occupation.md`
(section 1.4), `reversing/findings/FINDINGS-espionage.md`,
`reversing/findings/FINDINGS-startup.md` (section 7), `reversing/findings/FINDINGS-definitions.md`,
`reversing/findings/FINDINGS-modifierkinds.md` (the closed list of kinds, section 2 here).

## 1. What a modifier is

- **There is a fixed set of modifier kinds - 143 of them** - each with a key the script files
  use: `global_ic`, `local_ic`, `infrastructure`, `supply_throughput` and so on. `confirmed`:
  every one was read by name out of a running game, and on 2026-11-14 out of what makes them
  (section 2, *The kinds a block may name*). In Their Finest Hour they are 142 keys.
- **A modifier is one value per kind**, in thousandths, nearly all of them zero. `confirmed`
- **Many things are modifiers.** The compiler's own type information says laws, minister
  types, ideologies, leader traits, occupation policies and the entries of
  `static_modifiers.txt` are all the same kind of object as a province's or a country's
  modifier. `confirmed` So "a law gives +10% IC" is not a rule about laws: a law *is* a
  modifier, and it has `global_ic = 0.1` in it.
- **A country has one modifier and so does every province**, and each is the sum of the
  modifiers that currently apply to it. They are not adjusted as things happen: they are
  **cleared and rebuilt from their sources**. `confirmed` for the province, and for the
  country's rebuild existing and being called after anything that could change it.

That last rule is the one to hold on to. Nothing adds 2% to a country's IC and remembers to
take it away later. The source is there or it is not, and the rebuild finds out.

## 2. In the files

A block that defines a modifier-bearing thing has a few keys of its own and treats **every
other key as a modifier**, of the closed list below. `confirmed` for every file in the table
(`reversing/findings/FINDINGS-modifierkinds.md`, sections 2 and 3).

| file | what it declares | its own keys |
| --- | --- | --- |
| `common/buildings.txt` | buildings, one modifier each | see `ic.md`, section 1 |
| `common/laws.txt` | law groups, each holding laws | `cost`, `allow` |
| `common/minister_types.txt` | what a minister can be good at | `decay` |
| `common/static_modifiers.txt`, `event_modifiers.txt` | named modifiers the engine or an event applies | none |
| `common/occupation_policies.txt` | occupation policies | `allow` |
| `common/strategic_resources.txt` | what each strategic resource is worth to whoever has it | none |
| `common/ideologies.txt` | ideologies, and each faction's `modifier` | `color`; the faction's none |
| `common/triggered_modifiers.txt` | modifiers that apply while a condition holds | `potential`, `trigger` |
| `common/governments.txt` | governments | `duration`, `valid_for_new_country`, `election`, an ideology's or a post's name |
| `common/government_positions.txt` | the posts of a cabinet | `changeable` |

**`icon` is every modifier's own key** - a picture's frame - so every block of the table takes
it but a building's, a building being no modifier. `confirmed`

### The kinds a block may name

**A closed list, every kind made before the first file of modifiers is read**: `confirmed`

1. **The engine's 108**, 107 keys - `naval_intel_boost` is made twice, and the first is the one
   found. They are `CModifierKeys.Engine` in `lib/OpenHOI3.Sim/Modifiers`, in the original's
   order, which is the order section 8's table writes them in.
2. **The 32 mission efficiencies, by the order's own key** - `convoy_raid = 0.1`, not
   `convoy_raid_eff`, which is the kind's name and what is localised (section 8). The base
   game's event modifiers set `air_intercept`, `convoy_raid` and `naval_strike` so.
3. **One for each faction of `common/ideologies.txt`**: `suseptibility_` and its tag, in the
   original's spelling - `suseptibility_axis`, `_allies`, `_comintern` - which the minister
   types use. **Each is made once its faction's block is read**, so a faction's own `modifier`
   may not name its own kind nor a later faction's.

A key is matched **without case**, as the original's reader lower-cases what it looks up, and a
quoted key is never a kind. **A key that is none of them is logged and dropped** by every file of
the table but buildings, which **drop it in silence**. The base game, Their Finest Hour and
BlackICE use none (**measured** 2026-11-14 over every file of the table).

**Ours**: every such key is **reported** - buildings' too - and dropped, as the original drops it.
A file's report lists each as its block and the key; a triggered modifier's is an `UnknownKey`
problem of `OpenHOI3.SaveCheck script`. An ideology's and a post's kinds are read for that check
and not kept; nothing here was read to use them.

`static_modifiers.txt` says of itself that its names "can NOT be removed or changed, as the
code uses them": the engine looks each one up by name - the difficulty levels, `overseas`,
`nationalism`, `revolt_risk`, the spy missions. (measured, and `confirmed` for the ones read.)

**Laws.** Seven groups in Their Finest Hour: `civil_law`, `conscription_law`, `economic_law`,
`education_investment_law`, `industrial_policy_laws`, `press_laws`, `training_laws`. A
country has exactly one law from each. Its history file names it as `<group> = <law>` -
`economic_law = basic_mobilisation` - undated for the start and dated for changes. (measured)

**Ministers.** A country's ministers are declared in its own file under `common/countries/`,
in a `ministers` block keyed by number. Each has a name, an ideology, a loyalty, a picture,
a start date, and **one line per position he could hold, naming the minister type he would
be in it**: `head_of_state = power_hungry_demagogue`. The ten positions are in
`common/government_positions.txt`. The country's history file fills a position with
`<position> = <minister number>`. (measured)

So a minister contributes **the modifier of the type he has for the position he holds** -
the same man is a different modifier as head of state than as head of government.
`confirmed`; and one seated where he was declared no type for adds nothing.

**A minister type has one key of its own, `decay`**: a block naming technology categories,
each with how much the type adds to that category's monthly decay (section 4).

**Factions.** A country's history file puts it in one with `join_faction = <name>` and takes
it out with `leave_faction`. (measured) **Majors.** A country's own file under
`common/countries/` says `major = yes` for the seven of them. (measured)

## 3. A province's modifier

Rebuilt from nothing, adding in this order - read whole on 2026-10-14
(`reversing/findings/FINDINGS-occupation.md`, section A.7). `confirmed` throughout unless marked. Every static
modifier named is `static_modifiers.txt`'s, added whole and kept as a source of the province's modifier (section 8),
unless said; the scaled ones are kept as their scaled copies.

1. **Every building in the province:** its one modifier times the building's *current*
   level, fractions included, truncated - `effect × level / 1000`. **A building is not kept as a source.**
2. **`overseas`**, from `static_modifiers.txt`, when the province is **both** in a different
   area from its controller's capital **and** on a different continent. An area is a block
   of land in one country's hands, so a province that can be reached from the capital
   overland is never overseas however far away, and an island off the capital's own
   continent is not either. **What an area is exactly is `supply.md`, section 2**: land one
   country controls, touching, each province with more than one level of infrastructure -
   so a province with one level or none is in no area, and on another continent than the
   capital it is overseas even when it can be walked to. In Their Finest Hour it takes 75% off the province's manpower
   and 90% off its leadership.
   - **`blockaded`** is tested only for a province that is overseas: one with an owner, a
     port and a naval base above level 0 is spared; any other is blockaded when its area has
     no **open port** - a province with an owner, a port, a naval base above level 0 and no
     enemy fleet blockading it as its controller was last set (`FINDINGS-supplynetwork.md`,
     section 2). In Their Finest Hour it is an empty block.
3. **`non_core`**, when the province's **owner** has no core on it and is also its
   controller. An occupied province gets nothing here; step 7 is what applies to it. In
   Their Finest Hour it takes 75% off manpower, 80% off leadership and half off IC and
   resources.
4. *There is no fourth step: the third ends where the coast's begins.*
5. **One of three by the map**: `coastal` for land beside the sea, else `non_coastal` for
   other land, else `coastal_sea` for sea beside land; sea beside no land gets none.
6. **`no_adjacent_controlled`**, for a province with an owner none of whose neighbours,
   land or sea, has its controller - a province with no controller included.
7. **The occupation policy**: the policy its controller holds towards its owner, added
   whole, except where the owner controls it and where `REB` does - `occupation.md`, section
   3. This is the *entire* occupation penalty: there is no separate reduction to an
   occupied province's IC, resources, manpower or leadership. Where nobody has set one,
   `no_occupation` is added, which holds nothing.
8. **`blockaded` again**, where the province's own port is blockaded - whether or not step 2
   added it, so an overseas port in an area with no open port and blockaded itself has it twice.
   **When a port is blockaded** (read 2026-11-14, `reversing/findings/FINDINGS-modifiersources.md`,
   section 2; `confirmed`):

       a port is blockaded where it has an owner, a port and a naval base built above level 0,
       and the units in its port's sea province - those fighting no battle and not falling
       back, enemies of its controller (an undeclared war over it counting), each with at
       least one brigade or ship - hold one brigade or ship between them

   - **It is worked out only as the province's controller is set**, whoever sets it and with or
     without consequences, after the new controller is written: a fleet sailing in, sitting
     outside a port or sailing away tells it nothing. So a port that never changes hands is
     never blockaded, and one taken while the enemy's ships lie off it stays blockaded until it
     changes hands again - an oversight, kept (`docs/enhanced.md`).
   - A province without the owner, the port or the naval base is cleared and goes no further;
     one with them then works out its owner's share (below).
   - **Nothing saves it**: a loaded game has no port blockaded until a controller is set.
   - Besides this step and step 2's open port, `is_blockaded` reads it (`triggers.md`), and
     **a country's share of home ports cut off**, which `blockade = N` tests: of the provinces it
     owns that are in its acting capital's area or on its continent and have a port and a naval
     base built above level 0, those blockaded or held by somebody else, as thousandths of them
     all, truncated - nought with none. Worked out each month after the most manpower, as a game
     is entered after IC, and for a port's owner whenever that port is worked out.
   - The country's rebuild also asks it, in its walk of the provinces it controls, at war and of
     a province it owns in its capital's area - and does nothing with the answer (section 4).
9. **`land_province`** for land, **`sea_zone`** for sea. Their Finest Hour's
   `land_province` is `attrition = 1`.
10. **`manpower` times the province's manpower**, where that is above nought: each value
    times the manpower over 1000, truncated. Empty in Their Finest Hour.
11. **`nationalism` times the province's nationalism** (`occupation.md`, section 5), where
    that is not nought, its controller has no core on it and its owner is its controller:
    each value times the years over 1000. Their Finest Hour's is `local_revolt_risk = 0.3`,
    0.3 points a year.
12. **What events gave it** (`add_province_modifier`, `effects.md` section 6), in the order
    given: one whose time has passed, and whose year is above 1, is dropped; the rest are
    added. Its year is the original's count of years from its epoch, so a negative
    duration, which gives year 1, never passes - the country's rule.
13. **Radar.** Nothing is added; the controller's list of radar provinces, which what a country
    knows sees from (`intelligence.md`, section 4), is told. For a province with an owner whose
    `radar_level`, as this rebuild made it, is not the level last applied:

        last below 1 and new above 0:   put on its controller's list, unless it is there
        else last above 0 and new below 1:   taken off its controller's list
        the new level kept as the last applied, and the whole of what countries know asked for
        at the hour's end

    So it is not a crossing of 1 either way: a rise from 0.5 to 0.7 adds it again (it is there
    already), and a fall from 1.5 to 0.5 takes it off, where 0.5 reached from nought would have
    put it on. **A change of controller made with consequences** moves it too, before the new
    controller is written: on the new controller's list where its `radar_level` is not nought,
    off the old one's whatever it is, and that level kept as the last applied. A plain change of
    controller leaves it on the old holder's list, still seeing for him. The last level applied
    and the lists are not saved: a loaded game starts every province at nought and its entry's
    rebuild puts every province with radar on its controller's list (read 2026-11-14,
    `reversing/findings/FINDINGS-modifiersources.md`, section 3; `confirmed`).
14. **`revolt_risk` times the province's revolt risk as shown**, in points, where that is
    above nought - asked of the province's figures as the rebuild has them at this step
    (`revolt.md`, section 4). A mod that puts `local_ic = -0.02` in that block has said
    "minus 2% IC per point of revolt risk".

**When it is made again** - the original's schedule, and this project's since 2026-10-15:

- **every month**, every province, at the head of the month's pass and before the month
  takes nationalism down - the month a new game runs before its first hour included;
- **every hour**, in the hour's look at each province, one whose building has moved since it
  was last made: a bombing, a fallen-back defender's damage and the day's repair each mark a
  building whose working level they move, and the rebuild clears every mark;
- at once, **where a day's repair carries a building across a whole level**;
- at once, the one province: **a battle's capture** (`CMapProvince::TakeControl`), only where
  the controller changed; **a change of owner**, whether or not the owner changed; a
  building's level set by an effect; `add_province_modifier`; a decision taken, its scope's
  province where it has one and no other; and the occupation command, the setter's provinces
  of the other's;
- **every province as a game is entered**, at least twice in the original - two loops over
  every province, the second also before a new game's revolt pass.

**A plain change of controller makes nothing again**: land handed back by the day's return,
or taken by an effect, keeps the modifier it had until one of the above. So the occupation
policy reaches a province taken in battle at once, and one handed back within the hour at best
and at the month at worst. Likewise `add_core` and `remove_core` wait for the month before
`non_core` moves, and **a modifier an event gave is dropped only when the province is made
again**, so it outlives its end until then - and `has_province_modifier` still sees it.

## 4. A country's modifier

The country has a rebuild of the same shape, called after anything that could change the
answer. It has been read from end to end. **It clears the modifier and adds these, in this
order**; `confirmed` throughout.

1. **`base_values`**, whole. That is how every country comes to have 3.5 leadership, 4
   manpower and its two demands for consumer goods before anything of its own is counted.
2. **Its faction's own modifier**, when it is in one: the `modifier` of its faction's block
   in `common/ideologies.txt` (`factions.md`, section 1).
3. **Each minister in its government, as the minister type he has for the seat he holds.**
   The same man is a different modifier in a different seat; one seated where he was
   declared no type for adds nothing. **The posts are walked in their order**, the original's
   cabinet being an array of fixed slots in `common/government_positions.txt`'s order - so
   the ministers come among a modifier's sources, which the tooltips list, in that order
   however the government was seated (`reversing/findings/FINDINGS-countrymodifier.md`, the
   record's `CCountry::RebuildStaticModifiers`, step 5).
4. **Each of its laws**, **in `common/laws.txt`'s order of law groups**: the original keeps one
   law to a group, in a vector indexed by the group's place in that file, and walks it - so the
   laws come among the sources in that order whatever order the history or a save gave them
   (read 2026-11-14, the record's `CCountry::laws`). Germany's history gives its education law
   before its economic law; the manpower tooltip lists War Economy first.
5. **Its government**, which is a modifier too. In Their Finest Hour no government declares
   anything but which ideologies and positions it allows, so this adds nothing there.
6. **Each strategic resource it has the use of, multiplied by how much of it it has**
   (section 6).
7. **The whole modifier of every province it controls**, value for value. So anything a
   province's modifier holds reaches its controller's, once for each province. The local
   kinds come along too; nothing reads them off a country. The walk also asks, for a country
   at war, whether each province is blockaded, owned by it and in its capital's area - and
   **does nothing with the answer**: it only fetches the capital twice and goes on to the next
   province. A dead rule, listed in `docs/enhanced.md` (read 2026-11-14,
   `reversing/findings/FINDINGS-modifiersources.md`, section 1).
8. **`war` if it is at war with anybody, otherwise `peace`.**
9. **Espionage**: another country's spies on the "lower national unity" and "raise national
   unity" missions each contribute a static modifier scaled by their level and then by the
   mission's share, its own presence included (`espionage.md`, section 10).
10. **`war_exhaustion`, multiplied by its war exhaustion** (`warexhaustion.md`, section 4).
11. **`dissent`, multiplied by its dissent.** What `static_modifiers.txt` gives is for one
    point: `global_ic = -0.01` there is 1% of IC for each point of dissent.
12. **`neutrality`, multiplied by its effective neutrality over 100** - its neutrality less
    the threat it feels from the country that threatens it most, never below nothing.
    - *Between this and the next*, the original adds a modifier the country holds of its own,
      which **nothing fills**: every reference to it in the image is its construction, its
      destruction and this addition. It adds nothing, and is not built (`likely`; a scan of the
      image's references to it and to its values, `FINDINGS-countrymodifier.md` and
      `FINDINGS-modifiersources.md`, section 1; not watched in a running game).
13. **Its event modifiers**: what an event or a decision gave it, each with a date it lasts
    until. One whose date has passed is dropped here; one dated in year 1 never ends.
14. **Its triggered modifiers** (section 5).
15. **A puppet's master's faction's influence modifier**: for a country with an overlord, in
    no faction itself, whose overlord is in one - the static modifier its faction's group
    names as its `influence` in `common/ideologies.txt` (`align_towards_axis`,
    `align_towards_allies`, `align_towards_comintern`: `align_towards = 20` each in Their
    Finest Hour, mod-able) - **unless that very modifier is among its event modifiers**, as an
    influence or an alignment puts it there (`agreements.md`). So a puppet drifts towards its
    master's faction as an influenced country does (`alignment.md`), and an influence on it
    adds nothing more. Read 2026-11-14 (`reversing/findings/FINDINGS-modifiersources.md`,
    section 1; `confirmed`). *Ours*: where the file has no modifier of the name, nothing is
    added; the original adds through the missing modifier and faults.
16. **A difficulty block**: `very_easy_player`, `easy_player`, nothing, `hard_player` or
    `very_hard_player` for a country somebody plays, and the four `..._ai` blocks for one
    nobody does. Normal adds nothing either way.
17. `government_in_exile`, for a government in exile (`war.md`, section 14). Empty in Their
    Finest Hour.
18. `fractured_government`, where a party of another ideology group than the ruling party's holds a cabinet seat
    (`politics.md`, section 4).

**An ideology adds nothing.** **Technology is kept apart**: the IC calculation reads its
technology bonus beside the modifier, and so do resource production, leadership and
manpower.

**`base_values` is also read directly by the IC calculation**, which takes its `ic`
straight from it and adds it before the base is truncated (`ic.md`, section 3).

**A minister also changes how fast a country forgets.** A minister type may carry
`decay = { a_category = -0.25 }`. The rebuild adds that, for each sitting minister's type
in his seat, to the country's figure for the category, which the monthly decay of theories
and practicals multiplies by (`research.md`, section 7). `confirmed`, and (measured): 140
more monthly ability figures in the savegames agree once it is counted.

## 5. Triggered modifiers

`common/triggered_modifiers.txt` declares modifiers with two conditions each, `potential`
and `trigger`, and an `icon`. Every other key of a block is a modifier, as in section 2.
`confirmed` (`reversing/findings/FINDINGS-countrymodifier.md`, section 3, read 2026-10-08 and
2026-10-14):

- **Once a day, for every country, straight before its modifier is rebuilt**, each entry
  of the file is tested in file order. One whose `potential` and `trigger` both hold - in
  a scope of the country, with no FROM and no province - and that is not on the country's
  list joins its end; then every entry on the list for which they no longer both hold
  leaves it, where it stands.
- **So the list keeps the order its entries came on**, not the file's, and the rebuild
  adds them in that order (step 14) - which is the order a modifier's sources name them
  in (section 8).
- The same test runs on entering a game, each country's straight before its rebuild - for a
  new game before the country's laws are brought forward and it is set up, so what a triggered
  modifier gives is in the manpower it starts with (`manpower.md`, section 4; since 2026-11-14,
  where it was first tested on the first day and France started 30% over).
- So a triggered modifier is on or off from the day its conditions change. Nothing else
  rebuilds the list: a modifier rebuilt at another time - a decision taken - adds the list
  as it stands.
- **The player is told** (`docs/mechanics/messages.md`): a modifier joining raises
  `TRIGMOD`, with `NAME` its name localised, `EFFECT` every value of its modifier written as
  section 8 writes them and `EXPLANATION` the localised `desc_` and its key; one leaving
  raises `TRIGMODLOST`, with `NAME` and `EFFECT` only. Both are reported by the head of
  government, `MESSAGE_HEAD_GOV`. Nobody else is told.

(measured, before it was built) Five countries' leadership fits only with one each
(`leadership.md`, section 8), ninety countries' research only with `neutrality`, which takes
5% off research for a country in none of the factions (`research.md`, section 8), and
France's manpower only with `population_crisis` (`manpower.md`, section 6).

What the player sees of them is the Strategic Effects window, `strategiceffects.md`.

## 6. Strategic resources

`common/strategic_resources.txt` gives each resource a modifier, and a province's history
file may name one. `confirmed` unless marked.

**How much a province gives.**

    standing   = infrastructure * (1 + LOCAL_INFRASTRUCTURE + GLOBAL_INFRASTRUCTURE)
    standing   is never counted as less than 0.01 nor as more than 1
    efficiency = standing / what the province's infrastructure was built to

- The figures are the province's own modifier's. **So a resource is worth the share of its
  province's infrastructure that is still standing**: all of it in an undamaged province,
  less in a bombed one.
- **A province with no infrastructure gives nothing.**
- One more province modifier value is added to the efficiency flat. Which script key sets
  it has not been established; nothing in Their Finest Hour's files looks like it.

**How much a country has.** For each resource, the best efficiency among the provinces
that have it and whose controller's resources the country is given.

- **So a resource counts once**, however many provinces of it there are.
- **The figure is only ever raised** where it is worked out, **and nothing else lowers it
  while the game runs**: `confirmed` by a search of every place in the original that could
  reach it, which finds the known ones (`reversing/findings/FINDINGS-countrymodifier.md`,
  section 7). So a country that loses the province keeps the resource at the best it ever
  had it - an oversight, kept (`docs/enhanced.md`). Not watched in a running game.
- **A load forgets it.** The one thing that sets the figures back is a new session, every
  country's to nothing, and loading a save begins with one; the figures are then worked out
  from the provinces as the save has them. So a resource lost before a save is gone after
  the load, in the original and here (`confirmed`; `docs/enhanced.md`, *A whole game's
  replay*).
- A country that owns no province gets none.
- The country's modifier takes the resource's modifier with every value multiplied by the
  figure, each product dropping what does not fit.

**Whose resources a country is given.** Its own, always. Another country's when the two
are not at war with each other and one of these holds:

- it is a **major** - its own file says `major = yes` - **or has more than 100 base IC**,
  and the two are in the same faction;
- it is the **leader of a faction and at war**, the other is *not* in that faction, and the
  other's place on the triangle is within `FACTION_STRAT_BONUS_DIST` of the faction's corner,
  not of the leader (`factions.md`, section 6);

and then one of these:

- their acting capitals are on the same continent;
- failing that, **both have a naval base beside one coast region**: a sea province listed by
  one of the regions `map/coastlines.txt` names - in Their Finest Hour and BlackICE
  `indian_coast_region`, `atlantic_coast_region` and `pacific_coast_region` (mod-able: the
  file, and the regions' provinces in `map/region.txt`).

**The coast regions** (read 2026-11-14, `reversing/findings/FINDINGS-countrymodifier.md`,
section 7; `confirmed`). `map/coastlines.txt` (default.map's `coastlines`) is read with the
map, straight after `region.txt`:

- its key `strategic_resource_coastlines` holds a list of words, each taken as a region's
  name; a later block of the key replaces an earlier one, and anything else in the file is
  passed over;
- **the regions are found by walking `region.txt`'s regions in their order** and taking each
  whose name equals one of the words, **case and all**, once however often it is named; the
  original warns where it took fewer regions than there are words (a misspelt name, or one
  given twice);
- a missing file, or one without the key, leaves no coast regions, and the link never holds.

**A country's naval bases** are the provinces it **controls** whose naval base is built to
any level - what was built, so a bombed base counts. A base is beside every sea province one
of its edges leads to (the map's edges, every kind). The two countries are linked when one
coast region lists a sea province beside a base of each. No faction, level or port is asked
of the base beyond that.

(measured) France holds black soil in Indochina. The United Kingdom, a major in the same
faction with its capital in the same continent, gains manpower with black soil's 10% from
the first day; nobody else does. Gold is in the Netherlands', the Philippines' and South
Africa's hands, none of them in a faction with a major on its own continent, and those
three are exactly the countries whose money shows it.

**When.** At the start of every day, before any country's own work; and after a game is
loaded. **Not when a new game is entered**: no country has the use of any resource until
the first day is run, which is why a new game's starting manpower leaves black soil out
(`manpower.md`, section 4).

## 7. Ours

- **A modifier is a set of named values**, holding only the kinds that are not zero. The
  kinds are the closed list of section 2 since 2026-11-14, and a misspelt one is reported and
  dropped - a building's too, which the original drops in silence.
- **A country's modifier is every step of section 4** since 2026-11-14: `base_values`,
  its faction's (since 2026-10-13, with `factions.md`), its ministers, its laws, its government
  and `fractured_government` (since 2026-10-14, with `politics.md`), its strategic resources, its
  provinces' modifiers, `war` or `peace`, the spies' two unity modifiers (since 2026-10-14, with
  `espionage.md`), `dissent`, `neutrality` by its effective neutrality (since 2026-10-12, with
  `diplomacy.md`), its event modifiers and its triggered modifiers (since 2026-10-14, section 5).
  War exhaustion since 2026-11-07 (`warexhaustion.md`), the difficulty's block since 2026-11-06
  (`frontend.md`, section 4.3), and a puppet's master's faction's influence and a government in
  exile's since 2026-11-14, with the laws walked in their groups' order, where they had been
  walked in the order they were given. The modifier nothing fills, between steps 12 and 13, is
  not built: it would add nothing.
- **A triggered modifier's conditions answer as every condition does** (`triggers.md`): one
  about what the simulation does not hold answers false. In Their Finest Hour's file that is
  `lost_national` alone, so `desperate_defence` stays off until it is built.
- **Strategic resources are shared within a faction as the original shares them**, to a
  major or a country with more than 100 base IC whose capital is on the holder's
  continent or whose naval bases share a coast region with the holder's, and to a faction's
  leader at war from a country outside it aligned within `FACTION_STRAT_BONUS_DIST` of its
  corner (`factions.md`, section 6). The coast regions since 2026-11-14 (`CCoastlinesReader`,
  `CCoastlineReach`). *Ours*: the original's list of a country's naval bases is made again
  each day after the resources are worked out, and at a change of hands, so a base finished
  in play joins it a day late; here the bases are taken from the provinces as they stand
  each time. What changed: nothing at the start, where the Allies are the United Kingdom and
  France, both in Europe; in the Ireland game's saves of September 1939, May 1940 and July
  1941 the United Kingdom and France gain 13 to 25 resources by the coasts - South Africa's
  gold, Canada's uranium and tungsten, the United States' among them (`OpenHOI3.SaveCheck
  load`, which lists them). No save shows a country's levels, so this is set against the
  reading alone.
- **Nothing damages infrastructure yet**, so every province with any gives its resource
  whole, and the one unidentified modifier value is taken as nothing.
- **A country's faction is what its history file says**, and what play makes of it since
  (`factions.md`).
- **It is rebuilt once a day and on entering a game** - not at each change. Rebuilding is
  cheap, and doing it on a schedule cannot be forgotten at a call site. The cost is that a
  change takes effect at midnight rather than at once; when something needs it sooner,
  that thing's command rebuilds it.
- **A province's modifier is every step of section 3** since 2026-11-14: since 2026-10-14
  (`occupation.md`), revolt risk's step 14 since 2026-10-15 (`revolt.md`, section 4), and radar's
  list and the blockaded port since 2026-11-14 (`CRadarProvinces`, `CBlockade`). Until then a
  country's radar provinces were every province with radar it controlled at the day's rebuild of
  what it knows, never taken off; and no port was blockaded. A province in no area counts as
  having no open port (*ours*: what the original does with no area was not read). `blockaded`
  is empty in Their Finest Hour and BlackICE, so the blockade shows only in `is_blockaded`,
  `blockade` and a mod's own block.
- *Ours, inferred*: a unit fighting no battle is one with no battle of its own - a bombing is
  kept apart here (`bombing.md`) and does not count; and the units in the port's sea province are
  the hour's table of who stands where (`CUnitPositions`), whatever it holds of passengers and
  aircraft passing over. Neither was read against the original's list of a province's units;
  an enemy fleet satisfies both, which is the case that matters.
- *Ours*: the share's "in the acting capital's area" is the capital's supply area, as step 2's
  area is; where the capital is in no area nothing counts as in it, where the original would
  compare two empty areas as one.
- **A province's modifier is made again on the original's schedule** (section 3) since
  2026-10-15, on the maintainer's word; until then every province was made again once a day,
  a country's reason carried over to provinces and never decided for them. What is ours in it:
  a province holds one mark for all its buildings where the original holds one on each, which
  comes to the same since the rebuild clears every one; entering a game makes every province
  once, where the original makes each two or three times over with nothing between that the
  rebuild reads; and a new game is also made again while it is being made, before it is
  entered (`CStockpile.FillAtStart`, `CManpower.SetAtStart`, `CLeadership.SetAtStart`), for
  figures that are read before the original's entry would make them. *Inferred*: a repair
  crosses a whole level where the level's whole part is not what it was.
- **A province's area is its supply area** (`supply.md`, section 2), the same as home
  production's (`resources.md`, section 3).

## 8. A modifier's sources, written out

Read 2026-10-11 (`reversing/findings/FINDINGS-topbartooltip.md`, section 10). `confirmed`
unless marked.

**Every source a country's rebuild adds is kept, in the order it was added** - the scaled ones
as the scaled copy that was added - and the original's tooltips list them. The sources of one
kind:

    for each source, in order:
        line = its line for the kind (below); nothing where its value of the kind is 0
        before it, a newline, where the text has grown since the last source's turn
        the line, trimmed

So a source with nothing of the kind writes nothing and no newline.

**A source's line** for a kind:

    its name + ": " + colour + ("+" where positive) + figure + "§W"
    colour:  more is better ? (positive §G, negative §R, 0 §Y) : (positive §R, negative §G, 0 §Y)
    figure:  a percentage ? value x 100 to one place + "%" : value to two places

**Its name**: a law's, a minister type's, a static modifier's and a strategic resource's is
its key, localised - "Base Value" for `base_values`, "Dissent" for `dissent`. A province's
source is headed by a name of its own, in the original by what is `inferred` to be the
province's.

**Every value of a modifier**, which dissent's tooltip uses, is one line for each kind it
holds: the kind's own name - its display key, localised - then `": "` and the colour, sign and
figure as above, `§W`; newline between lines.

**Each kind has a display key and two flags**: whether it is written as a percentage, and
whether more of it is better. The original builds the 108 it knows at startup; the table is
`CModifierKinds` in `lib/OpenHOI3.Interface/Modifiers`, kind by kind. Nearly all are both.
A plain number, more is worse: the three revolt risks, `attrition`, `war_exhaustion`,
`max_war_exhaustion`, `local_partisan_support`, `neutrality_change`, `partisan_efficiency`. A
plain number, more is better: `local_manpower`, `global_manpower`, `fort_level`,
`coastal_fort_level`, `infrastructure`, `ic`, the two flat leaderships, `national_unity`,
`naval_capacity`, `air_capacity`, `align_towards`, `radar_level`, `unit_start_experience`,
`local_anti_air`, `land_intel_boost`, `naval_intel_boost`, `local_underground`. A percentage,
more is worse: `incorporate_cost`, the two consumer goods demands, `dissent`,
`supply_consumption`, `peacetime_manpower_rotation`, `unit_recruitment_time`, `threat_impact`,
`national_unity_effect`, `reserves_penalty_size`, `neutrality`, the five build speeds,
`jungle_effects`, `sw_national_unity_effect`. The 32 mission efficiencies are both, each named
by its order's key and `_eff` - `convoy_raid_eff` is what is localised for `convoy_raid` - and
so is a faction's `suseptibility_` kind, named by its own key. They come after the 107 in the
original's order, the missions first. `confirmed` (`reversing/findings/FINDINGS-modifierkinds.md`,
section 1).

*Ours:* a province's line is its name, `": "`, and its value as above - the original heads the
province's own sources with the name, and what those are was not read.

## What is not established, in one place

- what a **faction's own modifier** is loaded from - it is the faction block's `modifier`, read
  as a modifier (section 2); what more the faction's loader does with it was not read;
- which script key sets the value added to a province's resource efficiency;
- what marks the edge of an **area**: whether it is land a country owns or controls, and
  whether a strait joins two;
- whether the country's modifier that nothing fills (section 4, after step 12) is ever filled
  in a running game - a static scan says not, and no game has been watched;
- whether the original's list of a sea province's units, which a blockade counts, holds a
  fleet's passengers and aircraft passing over (section 7).
