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

**A technology declared twice is two technologies under one name**, each in its place in the files' order: a save
writes both, each country's and each brigade's, and a unit type's levels listed by position count the one that names
it - but **every look by name finds the later**: a history line, a condition, an effect, a save's entry read back. So
the earlier is reached only through the unit types it names, and stays at its first level. *Measured* on a BlackICE
start save (2026-11-19), whose one technology declared twice is `cag_design_hidden` - BlackICE's named carrier air
groups in `00_unique_unit_techs.txt`, its carriers in `unique_ships_techs.txt`: the save holds Britain's at 0 and then
at 1, the hidden-technology event having given the later its level, and the carriers' lines are the save's price only
with the later in its own place in their lists. *Ours*: the earlier is kept for its unit types alone
(`CTechnologyReader`), and a brigade's level of it is asked by the name, so a type naming the earlier and a country with
the later would read the later's level - BlackICE's named carrier air groups with the United States' `cag_design_hidden`,
where the original's would read the earlier's nought; whether any is in play was not looked at. The writer writes both,
the earlier at its place and at nought (`CGameState.TechnologyDeclarations`), and a load reads both entries into the
later, the second standing, as a look by name does. Their Finest Hour declares none twice, and BlackICE's repository
none since 2026-11-19, when its two `cag_design_hidden` were made one in `unique_ships_techs.txt` - a slip of the mod's,
by its maintainer's word - so the rule rests on that one save, made with the files before. A save of then loaded into
the mod as it is now reads both of its entries into the one technology, the second standing.

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

*Ours:* the technology screen asks the same question without running a day - what would
each project be given now - so that its queue is right the moment it is changed
(`CResearch.ShareOut`). `confirmed` And the days a level will take are what is left of it
over a day's gain, and one; no day at all when a day gains nothing
(`CResearch.DaysToFinish`; `technologyscreen.md`, section 6).

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

**`allow` is the whole of the script's condition language**, the same as an event's
`trigger` (`triggers.md`). `confirmed`: a technology holds its block as an embedded "all of"
that the general trigger grammar loads, flagged as a block of script, so a key no lookup knows
when it is read is held back and tried again once every technology is loaded - which is how one
technology names another further down the files - and reported and left out if it is still
nothing (`script.md`). **It is asked of the country alone**: a scope whose country is the
country, with no province and no FROM, and which is its own root, so THIS is the country. An
empty block holds. So any condition a mod writes in a technology's `allow` is a condition here,
answering as it does for an event asked of a country, and two `allow` blocks in one technology
add to one another, as the original loads both into the one block it holds.
(`reversing/findings/FINDINGS-research.md`, the section on the gate's scope.)

Their Finest Hour's 213 blocks use six things, each answering as `triggers.md` has it:

| in the block | holds when |
| --- | --- |
| `<technology> = N` | the country has level N of it or more |
| `num_of_ports = N` | it has N or more **home ports**: provinces it **owns** - whoever holds them - with a port and a naval base **built**, bombed or not, in the area of its acting capital (`triggers.md`, section 8), so a colony's port does not count |
| `any_owned_province = { has_building = X }` | a province it owns has building X built, a level or more |
| `OR = { }`, `AND = { }`, `NOT = { }` | as they say; `NOT` holds when none of its conditions does |

`confirmed` for all of them (`reversing/findings/FINDINGS-scriptgaps.md`, section 1, for
`num_of_ports`; `FINDINGS-triggerleaves.md` for `has_building`). **A theory or practical is no
condition**: the names a condition may be are a government position, a building, a technology,
a unit type, a law group and a combined-arms group, so `infantry_theory = 3` in an `allow` is
reported and left out, as the original logs it and drops it. Neither Their Finest Hour nor
BlackICE writes one. BlackICE's blocks use `year`, `war`, `faction`, `controls`,
`check_variable`, `has_country_flag`, province scopes and more, all answered.

The original's gate has one more refusal, for a custom game only: a level below the figure the
custom game's settings give the technology. There is one start here and no custom game
(`docs/compatibility.md`, entry 1), so it never applies.

## 6. Decay

On the first of each month every ability of every country shrinks: `confirmed`

    step    = BASE_TECH_DECAY * (1 + the category's decay modifier) * (1 - slack)
    ability = ability * (1 + step)

- `BASE_TECH_DECAY` is a define, -0.025 in Their Finest Hour: 2.5% a month.
- **The category's decay modifier** is what the country's technologies' `decay` entries add
  up to for it - fourteen technologies have one, each taking a tenth off a category's decay
  a level. It is worked out when the country's modifier is rebuilt (`modifiers.md`, section
  4), and **the ministers' `decay` does nothing**: the rebuild sets every category's figure
  to nought, adds what the `decay` of each sitting minister's type gives it - 24 minister
  types have one, most taking a quarter off one category - and then **stores** the
  technologies' figure over it, so the ministers' is thrown away (`confirmed`,
  `reversing/findings/FINDINGS-bicelibsites.md`, section 4; `FINDINGS-techdecay.md`, section
  4). *Exe-hardcoded*; a fault of the original, kept (`docs/enhanced.md`, *Economy and
  production*). A modifier of -1 or less stops the decay; it cannot turn it into a gain.
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
| 1 February | 1 March | 28 | 231 of 233 |
| 1 March | 1 April | 31 | 273 of 280 |
| 1 April | 20 April | 19 | 375 of 376 |

The first two rows compared 158 and 131 technologies before ministers' `decay` was counted
(as the patch the saves were made under counts it - below, *The decay*):
a technology is left out where an ability its cost depends on does not match the save, and
many more abilities match now.

The few that do not are countries whose queue or shares the AI moved between the two
saves. Technologies that finished a level in between, or whose leadership or cost changed,
are left out and counted.

- **`neutrality` is needed to get there.** `common/triggered_modifiers.txt` has a
  modifier, `neutrality`, that takes 5% off the research efficiency of every country in
  none of the three factions - ninety of them in early 1936. Until triggered modifiers were
  built (`modifiers.md`, section 5) the tool gave that one by hand, from the file's figure
  and the save's factions; since 2026-10-14 the day's test gives it, to the same ninety, and
  the three rows come out as the hand had them. Without it only the factions' members fit,
  and everyone else is 5% fast.
- **The autosaves have moved on since the table was made**: on 2026-10-14 the same three
  pairs gave 231 of 231, 70 of 85 and 375 of 376, and the code before triggered modifiers
  gave the same 70 of 85 - 160 technologies left out for leadership the AI moved.
- **The rough square root is needed**: with a true one in its place, ten of the 375 stop
  agreeing.
- **Those saves, and every reference save before 2026-11-07, were made by an
  executable carrying BiceLib's minister decay patch on disk** (and its war exhaustion
  patch), which the fact base found that day by setting the installed executable beside an
  untouched copy (`reversing/findings/FINDINGS-bicelibsites.md`, section 1). So the
  ministers' `decay` they bore out is the patch's, not the original's, which throws it away
  (section 6). *Measured* on 2026-11-16 on the Ireland game's monthly saves of 1937, which
  the same executable wrote: 5 July to 4 August, 1,025 abilities the same without the
  ministers and 1,152 with them, of 1,173 and 1,321 compared; 4 August to 3 September, 986
  and 1,118 of 1,138 and 1,285. No save of the vanilla executable spans a month's end yet:
  one would show the original's rule where these show the patch's. The figures below were
  taken with the ministers counted, as the patch counts them.
- **The decay** agreed to the thousandth for 1067 of 1123 abilities over February and 1042
  of 1240 over March, with the slack taken from the later save's shares of IC and each
  minister's `decay` counted. What does not agree is whole countries at a time - the Soviet
  Union, France - whose shares the AI is taken to have moved in the hour between the decay
  and the save; that has not been checked.
- **A strategic resource reaches research**: the Soviet Union's atomic research agrees
  only with its heavy water counted, which is worth 40% to `nuke_research`.

## 9. A new game's head start

Read 2026-11-02 (`reversing/findings/FINDINGS-starttech.md`, section 4), the original's
`SetUpCountryLawsAndTechnologyForNewGame`, its second half; the first is the laws' (`politics.md`, section 13).
`confirmed` throughout. As a new game is entered - **every country, the player's too**, once, before its order of
battle is read, and **never when a save is loaded** - each country buys technology levels:

    years   = the game's year x 1000 + (its month counted from 0 x 1000 - 1000) x 1000 / 12000 + 2000
              - the start year x 1000                                  1917 on 1 January of the start year
    budget  = years x 3650 / 1000 x min(the leadership of its core provinces, owned or not, 5) / 1000
    while the budget is above nought:
        the cheapest candidate, the first of equals in the technologies' order, the null technology first:
            the null technology while it is not bought, at 125
            each technology not in the folder `theory_folder`, that is not of one level and had already,
            that the country may research a level further (section 5), whose current level was due before this year:
                its cost (section 3), and double where its next level is not due before this year
        none: stop
        the budget less the candidate's cost over a hundred, undoubled; its level up one, no higher than its last

Each division drops its remainder, in thousandths. A level is due as section 3 has it: `start_year` for the first,
`first_offset` for the second, `additional_offset` more a level after. **Only the level is written** - no progress, no
ability, no message - and its effects come with the country's modifier made again. The month's term is an off-by-one in
the original: January counts as a twelfth below nothing, so the figure is 1917 and not 2000. The budget comes to about
1.92 years of the core provinces' leadership at full cost, which is why the levels come in whole families - a practical
makes one family cheap - and why a poor country stops part of the way.

*Exe-hardcoded*: 3650, the two years, the twelfth, the cap of five, the doubling, the hundredth, the null technology's
125 and its place, and the folder's name. *Mod-able*: the provinces' `leadership` and cores, the start date, and every
technology's `difficulty`, `research_bonus_from`, `start_year`, `first_offset`, `additional_offset`, `max_level`, `folder`
and `allow`, and the files' order, which breaks ties.

**The null technology** is the original's built-in `notech`, which no file declares and which stands first in its
technology database at a cost of 125 with no bonus. Buying it does nothing but take 1250 of the budget, and the start save
holds it at 1 in the 88 countries whose budget reaches it. *Ours*: it is a charge on the budget and no technology here; its
level alone is kept, `CCountry.NullTechnologyLevel`, set to 1 where it is bought, read from a save and written first in
the technology block as the original writes it (since 2026-11-14, `savegame.md`, section 19).

**Against the start save**: a new game's technology levels are the save's in **28,987 of 28,987** - where they were
28,511 before, the 476 the save holds above the history now given (`OpenHOI3.SaveCheck techgap`, 2026-11-02). That is
China's whole infantry equipment, artillery, anti-tank and anti-air, cavalry and militia, and Germany's 22 levels, its 36
as far behind left alone.

*Ours*: every country's laws and levels are brought forward before any country's order of battle is read, where the
original does both and reads the order of battle country by country; a technology's `allow` that asked about another
country's units would see them later here. The start year is the game's own, one start being all there is
(`docs/compatibility.md`, entry 1); the original takes `start_date` from `defines.lua`.

## 10. What a battle teaches

Every hour of a battle, at the head of each side's shooting, every country still fighting on the side gains a
practical, by the same diminishing gain a finished level gives (section 4): `confirmed`

| battle | the practical, named by the defines' top level | the gain, `military` |
| --- | --- | --- |
| on land | `land_combat` - `land_doctrine_practical` in Their Finest Hour | `LAND_DOCTRINE_INCREASE`, 0.001 |
| at sea | `naval_combat` - `naval_doctrine_practical` | `NAVAL_DOCTRINE_INCREASE`, 0.01 |
| in the air | `air_combat` - `air_doctrine_practical` | `AIR_DOCTRINE_INCREASE`, 0.01 |
| a bombing, the bombers alone | `bombing` - `air_doctrine_practical` | `BOMBING_DOCTRINE_INCREASE`, 0.01 |

- **Both sides of a land, sea or air battle gain**, each at the head of its own hour: on land after its front is seen
  to and before the side's draw of the generator, at sea and in the air after the leaders' hours are banked. A
  bombing's bombers gain; the side bombed has no shooting of its own of this kind and gains nothing.
- **The countries are the side's countries still in the fight** - those whose units are left on it - so a beaten
  side's gain stops with its last unit.
- **A raid on a convoy gains too**: its five rounds are hours of the sea battle (`navalmissions.md`, section 8), so the
  raider's country gains the naval practical five times, and the convoy's owner with it.
- **The name is looked up among the technology categories, case and all**, and a name that is none gives nothing.
- The gain is the define as it stands, in thousandths: 0.001 an hour on land is 0.024 a day of battle. Above the
  divisor it truncates: a country with 20 in the practical gains nothing an hour on land, 0.001 x 10 / 20 being under a
  thousandth.

(measured, 2026-11-18) `OpenHOI3.SaveCheck load` with a later save now sets these practicals beside it.
`Ireland1940_05_23_15.hoi3` played one hour to `_16`: the land practical moved for the same two countries in the save
and in ours, 100 of 101 countries the save's, France 6.456 to the save's 6.459 and ours 6.458 - a battle fewer of its
in ours that hour. Five hours to `_20`: France 6.479 saved and 6.472 ours, Germany's air practical 20.763 to 20.771 in
the save, which fought air battles ours did not. Every move on either side is a whole number of hours' gain; where they
part, the battles fought did.

Sources: `reversing/findings/FINDINGS-landbattle.md`, `FINDINGS-seabattle.md`, `FINDINGS-airbattle.md`,
`FINDINGS-bombing.md`; the shared gain and the defines' names, `reversing/findings/FINDINGS-definesuse.md`, sections 2
and 3. Built 2026-11-18 (`CCombatPractical`).
Nothing new is kept: the practicals are a country's abilities, saved and loaded already.

## What is not established, in one place

- how abilities are **recomputed when a game starts**;
- how the original **orders its technology categories**, which only matters to an
  interface.
