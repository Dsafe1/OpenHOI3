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
`reversing/findings/FINDINGS-manpowergain.md` (section 6),
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
`inferred` from the files; the code that resolves it has not been read.

## 3. A province's modifier

Rebuilt from nothing, adding in this order. `confirmed` throughout unless marked.

1. **Every building in the province:** its one modifier times the building's *current*
   level.
2. **`overseas`**, from `static_modifiers.txt`, when the province is **both** in a different
   area from its controller's capital **and** on a different continent. An area is a block
   of land in one country's hands, so a province that can be reached from the capital
   overland is never overseas however far away, and an island off the capital's own
   continent is not either. In Their Finest Hour it takes 75% off the province's manpower
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
answer. **Most of what it adds is not established**: the routine is long and only four
parts of it have been read. `confirmed` for these:

- **It starts from `base_values`, added whole.** That is how every country comes to have
  3.5 leadership, 4 manpower and its two demands for consumer goods before anything of its
  own is counted.
- **Its faction's modifier comes next**, when it is in one.
- **The whole modifier of every province it controls is added to it**, value for value. So
  anything a province's modifier holds reaches its controller's, once for each province -
  the country-wide kinds included. The local kinds come along too; nothing reads them off
  a country.
- **Espionage**: another country's spies on the "lower national unity" and "raise national
  unity" missions each contribute a static modifier scaled by their strength - which is why
  national unity moves the moment a spy is placed, rather than drifting daily.

That laws and ministers are added to it is `inferred`: they are modifiers, a country has
them, and nothing else could make a law's `global_ic` reach the IC calculation, which reads
the country's modifier and nothing else.

Two more sources are (measured) from the savegame and have not been read:

- **Strategic resources.** `common/strategic_resources.txt` gives each resource a modifier,
  and a province's history file may name one. Gold is `global_money = 0.33`, three provinces
  have gold, and the three countries that hold them - the Netherlands, the Philippines and
  South Africa - are exactly the three whose daily money is a third of base more than their
  laws give. The Dutch gold is in the East Indies, so the province need not be joined to
  the capital. Whether a resource counts once or once a province, and whether it is
  holding or owning the province that matters, is **not established**: no country has two
  of one, and nobody holds another's. `likely` it comes in with the province's modifier,
  above, which would make it once a province and the controller's; the step that would put
  a resource into a province's modifier was looked for and not found. **It is not there
  when a new game is set up**: the two countries with black soil start with manpower
  worked out without it (`manpower.md`, section 4).
- **Triggered modifiers.** `common/triggered_modifiers.txt` declares modifiers that apply
  while a condition holds. Five countries' leadership fits only with one each
  (`leadership.md`, section 8), ninety countries' research fits only with `neutrality`,
  which takes 5% off research for a country in none of the factions (`research.md`,
  section 8), and France's manpower only with `population_crisis` (`manpower.md`,
  section 6).
- **Event modifiers.** `common/event_modifiers.txt` declares modifiers an event or a
  decision gives a country for a time. The United States has one two days into a game,
  from a decision its AI takes at once, and its manpower fits only with it.

**Measured against a savegame of the original**, 2026-10-07: with a country's modifier taken
as its laws and ministers and nothing else, its home production of crude oil - which is
its provinces' yield times `1 + global_resources` - comes out right to the thousandth for
all 20 countries that produce any. So for `global_resources` at least, at the start of a
game, **laws and ministers are the whole of it.** See `resources.md`.

**`base_values` is also read directly by the IC calculation**, which takes its `ic`
straight from it and adds it before the base is truncated (`ic.md`, section 3).

**Not established:** whether ideology and government add to the country's modifier; how the
difficulty, war and peace static modifiers are chosen. Technology is kept *apart* from the
modifier: the IC calculation reads its technology bonus separately, and so do resource
production and leadership.

## 5. Ours

- **A modifier is a set of named values**, holding only the kinds that are not zero. The
  kinds are not a closed list yet: with no list of the 143 in prose, any key that is not
  one of a block's own is taken as a modifier. So a misspelt modifier is *not* reported
  yet, which is the opposite of what this project wants, and it stays a loose end until the
  list exists.
- **A country's modifier is `base_values`, its seven laws, its ten ministers and its
  strategic resources, summed.** Nothing else feeds it yet. Its technology is summed
  separately, beside it.
- **A strategic resource counts once, for whoever controls a province that has it.** Both
  halves of that are this project's reading of the three cases the savegame has. It is
  left out once, as the original leaves it out: when a new game works out what manpower a
  country starts with.
- **It is rebuilt once a day, before IC, and on entering a game** - not at each change.
  Rebuilding is cheap, and doing it on a schedule cannot be forgotten at a call site. The
  cost is that a change takes effect at midnight rather than at once; when something needs
  it sooner, that thing's command rebuilds it.
- **A province's modifier is its buildings, `overseas` and `non_core`**: steps 1 to 3 of
  the nine, without `blockaded`. It is rebuilt once a day, before the country's, and on
  entering a game. Steps 4 to 9 arrive with the systems they belong to.
- **A province's area is taken to be the land its controller can walk to from its
  capital**, the same test home production uses (`resources.md`, section 3). What marks
  the edge of an area in the original has not been read.

## What is not established, in one place

- the **list of the 143 modifier kinds**, in a form this project may read;
- what the **country's rebuild** adds, beyond `base_values`, the faction, its provinces'
  modifiers and espionage: where laws, ministers, strategic resources and triggered and
  event modifiers come in, and in what order;
- whether a **strategic resource** counts once or once a province, and for the controller
  or the owner; and why it is missing when a new game is set up;
- what marks the edge of an **area**: whether it is land a country owns or controls, and
  whether a strait joins two;
- how a **minister's type for his position** is resolved, and what `decay` on a minister
  type does;
- how the **difficulty, war and peace** static modifiers are selected.
