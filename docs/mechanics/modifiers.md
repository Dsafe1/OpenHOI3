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
`reversing/findings/FINDINGS-startup.md` (section 7), `reversing/findings/FINDINGS-definitions.md`.

## 1. What a modifier is

- **There is a fixed set of modifier kinds - 143 of them** - each with a name that is also
  the key the script files use: `global_ic`, `local_ic`, `infrastructure`,
  `supply_throughput` and so on. `confirmed`: every one was read by name out of a running
  game. The list itself is not written out in the fact base's prose.
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
other key as a modifier**. `confirmed` for buildings, by reading; `inferred` for the others
from the same loader shape and from the files.

| file | what it declares | its own keys |
| --- | --- | --- |
| `common/buildings.txt` | buildings, one modifier each | see `ic.md`, section 1 |
| `common/laws.txt` | law groups, each holding laws | `cost`, `allow` |
| `common/minister_types.txt` | what a minister can be good at | `decay` |
| `common/static_modifiers.txt`, `event_modifiers.txt` | named modifiers the engine or an event applies | `icon` |
| `common/occupation_policies.txt` | occupation policies | `allow` |
| `common/strategic_resources.txt` | what each strategic resource is worth to whoever has it | none |
| `common/ideologies.txt` | ideology groups and ideologies | `color` |
| `common/triggered_modifiers.txt` | modifiers that apply while a condition holds | `potential`, `trigger` |

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

Rebuilt from nothing, adding in this order. `confirmed` throughout unless marked.

1. **Every building in the province:** its one modifier times the building's *current*
   level.
2. **`overseas`**, from `static_modifiers.txt`, when the province is **both** in a different
   area from its controller's capital **and** on a different continent. An area is a block
   of land in one country's hands, so a province that can be reached from the capital
   overland is never overseas however far away, and an island off the capital's own
   continent is not either. **What an area is exactly is `supply.md`, section 2**: land one
   country controls, touching, each province with more than one level of infrastructure -
   so a province with one level or none is in no area, and on another continent than the
   capital it is overseas even when it can be walked to. In Their Finest Hour it takes 75% off the province's manpower
   and 90% off its leadership.
   - `blockaded` is tested only for a province that is overseas, and exempts one with a
     port and a naval base. In Their Finest Hour it is an empty block.
3. **`non_core`**, when the province's **owner** has no core on it and is also its
   controller. An occupied province gets nothing here; step 6 is what applies to it. In
   Their Finest Hour it takes 75% off manpower, 80% off leadership and half off IC and
   resources.
4. The terrain and coast family of static modifiers, each added flat.
5. The timed and conditional province modifiers - those an event applied.
6. **The occupation policy**, when owner and controller differ. The policy the controller
   has set towards the owner is a modifier and is added whole. This is the *entire*
   occupation penalty: there is no separate reduction to an occupied province's IC,
   resources, manpower or leadership. A province held by rebels gets none.
7. The `manpower` static modifier, scaled by the province's own manpower.
8. The `nationalism` static modifier, on land its owner holds but has no core on; entries
   age and are dropped after a year.
9. **The `revolt_risk` static modifier, multiplied by the province's revolt risk in
   percentage points.** A mod that puts `local_ic = -0.02` in that block has said "minus 2%
   IC per point of revolt risk".

It is rebuilt every month, on any day the province has been flagged as changed, and
whenever something that feeds it changes - a building's level, for one.

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
   declared no type for adds nothing.
4. **Each of its laws.**
5. **Its government**, which is a modifier too. In Their Finest Hour no government declares
   anything but which ideologies and positions it allows, so this adds nothing there.
6. **Each strategic resource it has the use of, multiplied by how much of it it has**
   (section 6).
7. **The whole modifier of every province it controls**, value for value. So anything a
   province's modifier holds reaches its controller's, once for each province. The local
   kinds come along too; nothing reads them off a country.
8. **`war` if it is at war with anybody, otherwise `peace`.**
9. **Espionage**: another country's spies on the "lower national unity" and "raise national
   unity" missions each contribute a static modifier scaled by their level and then by the
   mission's share, its own presence included (`espionage.md`, section 10).
10. **`war_exhaustion`, multiplied by its war exhaustion.**
11. **`dissent`, multiplied by its dissent.** What `static_modifiers.txt` gives is for one
    point: `global_ic = -0.01` there is 1% of IC for each point of dissent.
12. **`neutrality`, multiplied by its effective neutrality over 100** - its neutrality less
    the threat it feels from the country that threatens it most, never below nothing.
13. **Its event modifiers**: what an event or a decision gave it, each with a date it lasts
    until. One whose date has passed is dropped here; one dated in year 1 never ends.
14. **Its triggered modifiers** (section 5).
15. For a subject that is in no faction while its overlord is: the overlord's faction's
    `align_towards_...` modifier.
16. **A difficulty block**: `very_easy_player`, `easy_player`, nothing, `hard_player` or
    `very_hard_player` for a country somebody plays, and the four `..._ai` blocks for one
    nobody does. Normal adds nothing either way.
17. `government_in_exile`, for one.
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
- The same test runs on entering a game, each country's straight before its rebuild.
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
- **The figure is only ever raised** where it is worked out: `confirmed`. **That nothing
  else lowers it is `likely`**, not confirmed: a search of the original found no other
  place that writes it but the one that sets every country back to nothing when a session
  is reset, and it has not been watched in a running game. If it holds, a country that
  loses the province keeps the resource at the best it ever had it, which reads like an
  oversight.
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

- their capitals are on the same continent;
- both have a naval base beside the same sea region. The arithmetic of this was read and
  what a region is here was not.

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
  kinds are not a closed list yet: with no list of the 143 in prose, any key that is not
  one of a block's own is taken as a modifier. So a misspelt modifier is *not* reported
  yet, which is the opposite of what this project wants, and it stays a loose end until the
  list exists.
- **A country's modifier is steps 1 to 9, 11 to 14 and 18 of section 4**: `base_values`,
  its faction's (since 2026-10-13, with `factions.md`), its ministers, its laws, its government
  and `fractured_government` (since 2026-10-14, with `politics.md`), its strategic resources, its
  provinces' modifiers, `war` or `peace`, the spies' two unity modifiers (since 2026-10-14, with
  `espionage.md`), `dissent`, `neutrality` by its effective neutrality (since 2026-10-12, with
  `diplomacy.md`), its event modifiers and its triggered modifiers (since 2026-10-14, section 5).
  The rest wait for what they need: war exhaustion, subjects, a difficulty setting and
  governments in exile.
- **A triggered modifier's conditions answer as every condition does** (`triggers.md`): one
  about what the simulation does not hold answers false. In Their Finest Hour's file that is
  `lost_national` alone, so `desperate_defence` stays off until it is built.
- **Strategic resources are shared within a faction as the original shares them**, to a
  major or a country with more than 100 base IC whose capital is on the holder's
  continent, and to a faction's leader at war from a country outside it aligned within
  `FACTION_STRAT_BONUS_DIST` of its corner (`factions.md`, section 6). The sea-region link is
  not built: it needs to know what a region is.
- **Nothing damages infrastructure yet**, so every province with any gives its resource
  whole, and the one unidentified modifier value is taken as nothing.
- **A country's faction is what its history file says**, and what play makes of it since
  (`factions.md`).
- **It is rebuilt once a day and on entering a game** - not at each change. Rebuilding is
  cheap, and doing it on a schedule cannot be forgotten at a call site. The cost is that a
  change takes effect at midnight rather than at once; when something needs it sooner,
  that thing's command rebuilds it.
- **A province's modifier is its buildings, `overseas` and `non_core`**: steps 1 to 3 of
  the nine, without `blockaded`. It is rebuilt once a day, before the country's, and on
  entering a game. Steps 4 to 9 arrive with the systems they belong to.
- **A province's area is taken to be the land its controller can walk to from its
  capital**, the same test home production uses (`resources.md`, section 3). What marks
  the edge of an area in the original has not been read.

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
`jungle_effects`, `sw_national_unity_effect`. The 32 mission efficiencies (`*_eff`) are both.

*Ours:* a province's line is its name, `": "`, and its value as above - the original heads the
province's own sources with the name, and what those are was not read. A kind the table does
not have is written as a percentage, more being better, as most are.

## What is not established, in one place

- the **list of the 143 modifier kinds**, in a form this project may read - 140 of them are now
  in `CModifierKinds`, read from the original's definitions;
- what a **faction's own modifier** is loaded from;
- what a **region** is in the link that lets two countries on different continents share
  a strategic resource, and which script key sets the value added to a province's
  efficiency;
- what marks the edge of an **area**: whether it is land a country owns or controls, and
  whether a strait joins two.
