# Research: the queue, what a level costs, and what finishing one does

How the original spends a country's research share each day, written as rules this project
can implement. No addresses and no offsets. Each rule says how sure it is - `confirmed`,
`likely`, `inferred` in the fact base's vocabulary - or **measured**, meaning checked on
2026-10-08 against four savegames the original wrote between 1 February and 20 April 1936
with no mods. Where this project chooses its own behaviour the paragraph is marked **Ours**.

Sources: `reversing/findings/FINDINGS-research.md`,
`reversing/findings/FINDINGS-researchshare.md`, `reversing/findings/FINDINGS-techdecay.md`.

Arithmetic is in thousandths with every division truncating, except progress, which is in
the shares' kind of number where 32768 is one level; see `numbers.md`.

## 1. In the files

**`common/technology.txt`** lists the *categories*: 25 `theoretical` and 23 `practical` in
Their Finest Hour - `infantry_theory`, `infantry_practical` and so on. A country has a
level in each, from 0 up, called its ability. (measured: the lists; the savegame writes
one line a category for every country.)

**`technologies/*.txt`** declares the technologies. Beside what a level is worth
(`resources.md`, section 4) a block has:

| key | what it is |
| --- | --- |
| `difficulty` | a whole number; what a level costs comes from it. A fraction is dropped, so `0.5` is 0 |
| `start_year` | the year **level 1** is due |
| `first_offset` | the year **level 2** is due. Not an offset |
| `additional_offset` | the years added for each level past 2. **Absent or 0 makes it a single-level technology** |
| `max_level` | the highest level |
| `research_bonus_from` | categories and weights: whose ability makes it cheaper |
| `on_completion` | the category that gains from finishing a level |
| `is_nuclear` | the country's `nuke_research` modifier counts towards its bonus |
| `allow` | conditions for researching it at all |
| `decay` | categories and amounts: how much this technology changes that category's monthly decay, a level |

`confirmed` for what each does. `change`, `can_upgrade` and `folder` are for the interface.

**A country's history file** sets its ability in a category with the category's name -
`infantry_theory = 10` - and its level of a technology with the technology's.

## 2. The queue

A country has an ordered list of the technologies it is researching. `confirmed`

Each day, after officers, diplomacy and espionage, the research share is given its
allocation - share times leadership, in the shares' kind of number (`leadership.md`,
section 2). Then, **in queue order, while any allocation is left**:

- A technology whose next level **may not be researched** (section 5) is given nothing and
  uses nothing; it keeps its place.
- Otherwise it is given **one point of leadership, or what is left if that is less**, and
  that much is taken off the allocation.
- Its progress goes up by the gain (section 3).

**Anything further down the queue than the leadership reaches gets nothing that day.**
(measured: Germany on 1 February has 28.796 to spend on 29 projects; 28 of them show
1.000 assigned and the last 0.796.)

The leadership given is turned into thousandths, rounded down, before the gain is worked
out, and is what the savegame records beside each technology.

## 3. What a day adds

For the level being researched - one more than the country has:

    progress += leadership * efficiency / year penalty / cost

in levels: a technology is finished when its progress reaches 1. `confirmed`, and
(measured) to the last digit the savegame prints (section 8).

**Efficiency** is `1 + RESEARCH_EFFICIENCY + the technology bonus`, never below 0.1. The
first is the country's modifier of that name; the second is what its technologies'
`research_efficiency` adds up to.

**The year penalty** is for researching ahead of time:

    due  = start_year                                  for level 1
         = first_offset + (level - 2) * additional_offset    from level 2
    early = due - this year
    penalty = 1 + early * TECHNOLOGY_YEAR_IMPACT   when early is above nothing, else 1

The years are calendar years and `TECHNOLOGY_YEAR_IMPACT` is a define, 1.5 in Their Finest
Hour: a level due in 1940 and researched in 1936 takes seven times as long. **Nothing here
forbids it**; being late gives no bonus.

**The cost**, in leadership-days:

    base  = (1 + difficulty * 0.1) * 125
    bonus = the sum, over research_bonus_from, of weight * the category's term
            + NUKE_RESEARCH                      for a technology marked is_nuclear
    bonus is never above 0.9
    cost  = base * (1 - bonus)

**A category's term** comes from the country's ability in it:

    a = ability, kept between 0 and 20, less 5
    if a is above 1:  a = the rough square root of a
    term = a * 0.1

- **An ability under 5 makes the technology dearer**: at 0 the term is -0.5.
- The 20 is built in; a mod that raises `MAX_TECH_ABILITY` past it gains nothing here.
- **The square root is rough on purpose or by accident, and has to be copied exactly.** It
  starts from half the number and improves the guess until its square is within the
  number itself of the number - which is after one or two steps. 5 gives 2.25 where the
  true root is 2.236; 10 gives 3.179. Section 7 has the steps.

Where another country shares a category with this one, the higher of the two abilities is
used. *Ours:* nothing is shared yet.

The steps, in order, each product divided by 1000 and truncated before the next:

    cost       = ((difficulty * 100 + 1000) * 125000 / 1000) * (1000 - bonus) / 1000
    penalty    = 1000 + (early * 1000) * TECHNOLOGY_YEAR_IMPACT / 1000
    gain       = efficiency * leadership / 1000
    gain       = gain * 1000 / penalty
    progress  += (gain as the shares' kind) * 32768 / (cost as the shares' kind)

A technology that costs nothing or less makes no progress.

## 4. Finishing a level

When a technology's progress reaches 1: `confirmed`

1. One is taken off the progress, so **what was over carries into the next level** - unless
   the technology is single-level, when it is set to nothing.
2. The country's level goes up by one, and what the level is worth applies at once.
3. **The category named by `on_completion` gains**:

       gain = 1 / max(1, ability / TECH_ABILITY_GAIN_DIVISOR)

   so a full point while the ability is under the divisor - 10 in Their Finest Hour - and
   less and less above it. The ability is never below 0 or above `MAX_TECH_ABILITY`, 99.
4. The technology leaves the queue. **If it has further levels and the next may be
   researched, it goes back on at the end** - after every technology has had its day.

(measured: Germany's `at_barrell_sights` finishes level 2 between 1 and 20 April, keeps
0.00439 of progress, and is last in the queue in the later save.)

## 5. What may be researched

A level of a technology may be researched when all of these hold: `confirmed`

- it is single-level and the level is 1, or it is not single-level;
- the level is not above `max_level`;
- its `allow` block holds.

*Ours:* a technology with no `max_level` has 1. All 85 of Their Finest Hour's that leave
it out are single-level, where the first rule already limits them.

**`allow` is written in the script's condition language**, which is stage 4. Their Finest
Hour's 213 blocks use six things, and those are read here; anything else is reported when
the files are loaded and then left out, as the original leaves out a key it does not know.

| in the block | holds when |
| --- | --- |
| `<technology> = N` | the country has level N of it or more |
| `<category> = N` | its ability in the category is N or more |
| `num_of_ports = N` | it controls N or more provinces with a naval base |
| `any_owned_province = { has_building = X }` | a province it owns has building X |
| `OR = { }`, `AND = { }`, `NOT = { }` | as they say |

`inferred` for all six: none has been read out of the executable, and for `num_of_ports`
whether a port is a naval base, and whether owning or controlling counts, is a guess.

## 6. Decay

On the first of each month every ability of every country shrinks: `confirmed`

    step    = BASE_TECH_DECAY * (1 + the category's decay modifier) * (1 - slack)
    ability = ability * (1 + step)

- `BASE_TECH_DECAY` is a define, -0.025 in Their Finest Hour: 2.5% a month.
- **The category's decay modifier** is what the country's technologies' `decay` entries
  add up to for that category - fourteen technologies have one, each taking a tenth off a
  category's decay a level. A modifier of -1 or less stops the decay; it cannot turn it
  into a gain.
- **`slack` is the reinforcement share plus the upgrade share** of the country's IC
  (`production.md`, section 1): the positions of the two sliders, whether or not anything
  is spent through them. A country with a tenth of its IC on each loses a fifth less.
- Theories and practicals decay at the same rate.

It runs after the day's research on the first of the month.

## 7. The square root, step by step

For a number `x` above nothing, in thousandths:

    guess = x / 2
    repeat:
        error = guess * guess / 1000 - x
        guess = guess - error * 1000 / (2 * guess)
    until error is between -x and x

The test uses the error from before the guess was improved, so there is always at least
one step. `confirmed`, and (measured): the costs it gives are the ones the savegames'
progress needs.

## 8. Ours, and what the savegames confirm

- **The queue is set by a command**, whole: a list of technologies, each of which must
  exist, appear once and be allowed. There is no AI, so nothing queues anything unless a
  command does.
- **A level's effect applies at once**, by rebuilding the country's technology totals.
- **Nothing is shared between countries**, and the custom-game ceiling on a technology's
  level is not there.
- **A minister's `decay`** is not applied. Minister types carry the key; how the original
  combines it with the technologies' is not established.
- **A history file's abilities and levels are taken as written.** (measured: the savegame
  of 1 January has every country's abilities exactly as its history file gives them, 1316
  of 1316.)

`tools/OpenHOI3.SaveCheck research` takes the savegames in pairs. For every technology a
country was researching in the earlier one, with the same level in the later, it works
out one day's gain from the earlier save's own state - abilities, levels, laws, leadership
assigned - and compares it, times the days between, with how far the progress moved, as
the five decimals the save prints.

| from | to | days | technologies that agree |
| --- | --- | --- | --- |
| 1 February | 1 March | 28 | 156 of 158 |
| 1 March | 1 April | 31 | 127 of 131 |
| 1 April | 20 April | 19 | 375 of 376 |

The few that do not are countries whose queue or shares the AI moved between the two
saves. Technologies that finished a level in between, or whose leadership or cost changed,
are left out and counted.

- **One thing is given by hand to get there.** `common/triggered_modifiers.txt` has a
  modifier, `neutrality`, that takes 5% off the research efficiency of every country in
  none of the three factions - ninety of them in early 1936. Triggered modifiers are not
  applied by this project yet (`modifiers.md`, section 4), so the tool adds that one itself,
  from the file's figure and the save's factions. Without it only the factions' members
  fit, and everyone else is 5% fast.
- **The rough square root is needed**: with a true one in its place, ten of the 375 stop
  agreeing.
- **The decay** agrees to the thousandth for 933 of 983 abilities over February and 911 of
  1084 over March, with the slack taken from the later save's shares of IC. What does not
  agree is whole countries at a time, whose shares the AI moved in the hour between the
  decay and the save.

## What is not established, in one place

- the six `allow` conditions of section 5, none of which has been read;
- **triggered modifiers**, which make every unaligned country's research 5% slower than
  this project has it;
- what a **minister's `decay`** does;
- how abilities are **recomputed when a game starts**;
- how the original **orders its technology categories**, which only matters to an
  interface.
