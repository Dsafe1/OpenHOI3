# The tick: the clock, the passes, and the command queue

How the original gets from one hour to the next and what it does when it arrives, written
as rules this project can implement. No addresses and no offsets. Each rule says how sure
it is - `confirmed`, `likely`, `inferred` in the fact base's vocabulary - and where this
project chooses its own behaviour the paragraph is marked **Ours**.

Sources: `reversing/findings/FINDINGS-tick.md`, `reversing/findings/FINDINGS-schedule.md`,
`reversing/findings/FINDINGS-commands.md`, `reversing/findings/FINDINGS-events.md`,
`reversing/findings/FINDINGS-techdecay.md`.

## 1. Time

- **The unit of time is the hour.** The game state holds one counter of hours and every
  date is derived from it. `confirmed`
- **A year is 365 days, always.** There are no leap years: the counter's epoch is a whole
  number of 365-day years before year zero. `confirmed` for the 365; that the month lengths
  are the ordinary ones with a 28-day February is `inferred` - the original keeps them in a
  table that was not read out value by value.
- **Dates are written `year.month.day`**, and the interface shows the hour in front:
  `13:00, 1 January, 1936`.

*Ours:* the counter starts at hour 0 of 1 January, year 0, rather than five thousand years
earlier. Nothing depends on the epoch.

## 2. Real time into game time

- **Every frame adds the frame's length to an accumulator.** When the accumulator reaches
  the threshold for the current speed, it is set to zero and one hour is due. `confirmed`
- **There are five speeds, each with its own threshold** - a table of five, not one number
  scaled. `confirmed` for the table; the five values are **not established**.
- **Only one hour is ever in flight.** While an hour is being processed no further hour can
  be queued, however long the processing takes; time keeps accumulating meanwhile, so a
  slow hour is caught up afterwards rather than lost. `confirmed`
- **The hour is not advanced directly: a command is posted that advances it** - see
  section 4. `confirmed`

*Ours:* the five thresholds are our own - half a second, a quarter, a tenth and a
twenty-fifth of a second per hour, and at the top speed as many hours as fit in a frame's
time budget. Pause is a separate switch rather than a speed. Like the original, time owed is
carried over, but it is capped so that a long stall does not turn into a burst.

## 3. What an hour does

One hour, in this order. `confirmed` throughout unless marked.

1. **The hour counter goes up by one**, and the date before is remembered.
2. **The state of the random generator is recorded**, to travel with the tick.
3. **The hourly pass.**
4. If the **day** changed: **the daily pass**, then the interface's own daily update (which
   is where the autosave is considered).
5. If the **month** changed: **the monthly pass**.
6. If the **year** changed: **the yearly pass**.
7. **Units queued for removal during the hour are removed**, last of all. A unit is never
   freed in the middle of a pass: whatever decides it should go puts it on a queue, and
   this step empties the queue.

A change of month always comes with a change of day, and a change of year with both, so on
1 January all three run, daily first.

### The hourly pass, in order

1. Per country, one after another: every unit **checks its order and its combat**; units
   that are finished are removed and the rest are collected.
2. Every collected unit's **hourly update**.
3. Every country's hourly update.
4. The AI's trade work.
5. Every province's hourly update.
6. **Combat**: every battle in progress is advanced one step.
7. The AI is then released to think, once the hourly pass can no longer be disturbed by it.

The supply network is also rebuilt inside the hour when something changed it.

*Ours:* steps 1 to 3 and step 6 are built as far as there is anything for them to do.
**Before step 1 the hour's rolls are seeded** - the original from a figure the hour's tick
carries, here from the game's seed and the hour (`landcombat.md`, section 8). **Step 1 is
where a unit moves and where a battle starts**: the look at its order (`fleetsandair.md`,
section 5), the look for an enemy ahead, its attack delay, its march (`movement.md`,
section 6), and the look at the province it stands in, every unit of every country in turn.
**Step 6 is every battle's round**, in the order the battles began (`landcombat.md`,
section 4) - after every unit's organisation has been seen to. Step 2, a unit's own
hour, is the return of its organisation; step 3, a country's, is the count of the officers
its units call for and the ratio that comes of it (`organisation.md`, sections 1 and 3).
The order matters: a unit's hour uses the ratio the hour before left, and a unit has moved
before its organisation is seen to. Until 2026-10-09 the march was put at the end of step 2,
from a reading that named the wrong caller.

**The AI is on the hourly schedule but each country's ministers act once a day**, at the
hour `(country index + 13) mod 24`, so the AI's work is spread around the clock instead of
landing on one hour. `confirmed` A second per-country gate fires at the hour equal to
`country index mod 24`; what it guards is **not established**.

### The daily pass, in order

1. The supply network is rebuilt if needed, **the order supply is handed out in is sorted,
   and the supply pass runs** - once a day, not once an hour. Built: `supply.md`, sections
   11 and 12.
2. Per country: neighbours are recomputed.
3. Per country, one after another: the country's daily work, which includes **recomputing
   its IC**, every unit's daily update, and the daily politics - effective neutrality,
   alignment, and the rebels' dissent being held at zero. **Its convoys sail early in it,
   before its units draw, and are made, unmade and given their ships late in it**, after the
   day's production. Built: `convoys.md`, section 10.
4. Per province: the province's daily work.
5. **Events**: every country's candidate events are tried against their mean time to
   happen. See the note below - this is not where the candidates come from.
6. **Intelligence is refreshed.** Built as far as a fleet's sighting asks: `navalmissions.md`,
   section 9. The original also does it at the end of an hour once a change of who plays a
   country has asked for it; whether an ordinary game is in that state was not established.

Several further steps of the daily pass are called in a known order and are **not yet
identified**.

### The monthly pass, in order

1. Per province: its modifiers are rebuilt, and **nationalism falls by 0.083, never below
   zero**.
2. Per country: the monthly work, which is **where technology practicals and theories
   decay**.
3. **Each country's list of candidate events is rebuilt.**

**That third step is why events come in bursts.** The daily pass only tries events that are
already on a country's candidate list, and the list is built here and nowhere else during
play. A freshly loaded game has empty lists, so nothing fires until the first of the
following month - which is exactly what players of the original see. `confirmed`

### The yearly pass

**Not established.** It exists, it is small, and it is the whole of the original's yearly
behaviour.

### On threads

The original spreads five of the steps above across worker threads - the unit, country,
trade and province hourly updates, the neighbour recomputation, the AI and the intelligence
refresh. The order between steps is fixed; the order *within* a spread step is not.

*Ours:* everything runs on one thread, in a fixed order, until a measurement says
otherwise. A simulation that must give the same result on every machine cannot let the
order of its work depend on a scheduler, and the original's own arrangement - nothing is
freed mid-pass, removals are queued and applied at the end - is the discipline that would
make spreading the work safe later.

## 4. The command queue

- **Everything that changes the game goes through one queue.** A player's click, the AI's
  decision and the hourly tick itself are all commands. `confirmed`: the same posting
  idiom appears at over three hundred places, across about a hundred kinds of command.
- **A posted command is stamped** with the session's turn number and a serial, and put on
  the queue. `confirmed`
- **The queue is drained in one loop and each command is executed at one place, in
  order.** The turn number goes up once per drain. `confirmed`
- **A command can refuse to run**: each kind answers "may I be executed" just before it
  would be. `confirmed`
- **The last hundred serials are remembered**, and a command whose serial is already among
  them is skipped as a duplicate. `confirmed`
- **In a game between several players the same commands run on every machine**, in the
  same order. That is what keeps the machines in step, and it is why the tick is a command:
  the hour advances everywhere at the same point in the stream. `inferred` - read from the
  code's structure, not observed on a network.

**The rule that follows, and it is the one that cost the original's modders a session:**
*a local input may choose which commands are posted, never what a command does when it
runs.* A command executes on every machine, so anything it reads that exists on one machine
only - a pressed key, what is selected, which country this player is - makes it do
something different on each. A command names its own targets. `confirmed` by a
multiplayer test.

*Ours:* the same shape. Nothing changes a game except a command's execution, the hour is a
command, and commands run in the order they were posted. The duplicate history is not
reproduced until there is a network for a duplicate to arrive over.

## 5. The random generator

The original draws from a Mersenne Twister with one deviation from the reference algorithm
in how it tempers its output, and it sends the generator's bookkeeping along with every
hourly tick. `confirmed` for both; that the bookkeeping is there to keep machines in step is
`inferred`.

*Ours:* one generator per game, part of the game state, drawn from only inside a command.
Reproducing the original's exact sequence is not a goal - the same sequence only gives the
same game if every draw is made in the same order, which would mean reproducing the whole
executable. Nothing draws yet, so there is no generator yet.

## 6. Before a game's first hour

`confirmed`, and **measured**: `reversing/findings/FINDINGS-supplypass.md`, section 7, for
the order, and `reversing/findings/FINDINGS-organisation.md` and
`reversing/findings/FINDINGS-techdecay.md` for what is different while no game is running.
A game that is not loaded from a save does not start at its history files. As it is
entered, with the clock standing still, the original runs:

1. **a daily pass**;
2. **the filling** of the provinces along the supply network (`supply.md`, section 16), at
   the end of which a unit that still has nothing is given its fifteen days;
3. **a daily pass**;
4. **a monthly pass**;
5. **an hourly pass**.

So two days of production, research, building, upgrades, rotation, manpower and leadership
are behind the first screen, and one hour of a unit's own. **The date does not move**: the
first screen says hour 0 of the first day, and the first hour played is hour 1.

**All of it is run before the original counts a game as running**, and three things are
different while it does not:

- **a brigade's most organisation is its own figure and nothing else** - no reserve's share,
  no officers, no modifier (`organisation.md`, section 2). So the hour at the end brings
  nobody down and can raise only a brigade below its figure, which is one whose figure a
  doctrine or an upgrade raised in those two days;
- **the monthly pass decays nothing**: an ability is what the history gave it
  (`research.md`). The start save has all 1316 that way;
- **a convoy is manned for nothing** (`convoys.md`, section 3).

**The original's AI then changes laws**, for every country, the player's among them, and a
change of law holds every brigade of that country to the running game's ceilings
(`organisation.md`, section 5). That is why the original's first screen shows Germany's
reserves at 28.000. It also changes some laws *before* the days and some after, so a save's
laws are not always the ones its two days ran under (`upkeep.md`, section 9).

*Ours:* **built on 2026-10-10**: `CTick.RunFirstDays`, which a new game runs as it is made,
in that order and with the game "being made" throughout. Three things about it:

- **Nothing changes a law here**, there being no AI, and the player's country keeps its
  history's by decision (`docs/compatibility.md`, entry 6). So nobody is held: a reserve
  stands at its own figure on the first screen and the first hour played brings it to its
  ceiling, which is what the original does for the one country whose laws its AI left alone.
  **One thing comes of it that the original all but never shows**: the hourly return skips
  a unit that is fighting or marching onto enemy ground (`organisation.md`, section 3), so
  a reserve division sent to attack before the first hour is run keeps its own figure for
  that battle - 35 for a German reserve, where its ceiling is 14. Holding every brigade
  once the days are run would close it and is one line; it is not done because it is not
  what the original does, and it is the maintainer's to say.
- **The figures of the first screen are worked out afterwards for a running game**, by the
  pass that is run on entering one, so the top bar shows what the first day played will
  make.
- **It costs nothing to speak of**: two days at about a tenth of a second each. What a new
  game takes to make - six seconds or so - is the first convoys' routes, about four, and the
  filling, about two, and both were there before.

### Checked against the start save

`OpenHOI3.SaveCheck firstdays <install> <start-date save>` makes a new game as a player
gets it and sets every country beside the save: stockpile, manpower, officers, influence,
abilities, technology, the build queue, every brigade's strength and organisation. **It is
meant to differ**, and says where: the original's AI has changed laws and shares by then,
and has made headquarters. For Germany, which the save's player has: the abilities and the
build queue agree, and the rest is what `two_year_draft` and `war_economy` make of it.

The like-for-like is `OpenHOI3.SaveCheck supplypass <install> <start-date save> countries`,
which gives every country the save's laws, ministers, technology and shares, makes its
units, its manpower and its queue under them, runs these same first days - the game's own,
not a model of them - and then holds a country's brigades as a change of law would wherever
the save shows one held. `history=<tag>:<group>+<group>` after it keeps a country on its
history's law for a group, for the laws the AI changed only after the days were run.
**Measured** on the save of 1 January 1936, with `history=GER:training_laws+industrial_policy_laws`
and the same for the industrial policy of the two German states that do not exist and share
its land:

| | agree |
| --- | --- |
| Germany: strength and organisation of every brigade, ship and wing | 172 of 172 each |
| Germany: manpower, officers, influence, abilities, technology, the build queue | all |
| Germany: the stockpile | six goods of seven to the thousandth; supplies 3.301 under in 8410, which is its leaders (`upkeep.md`, section 1) |
| the others: organisation, not under an army with a commander | 2770 of 2770 |
| the others: organisation under a commander, no leaders built | 142 of 764 |
| the others: strength | 3532 of 3534 |
| the others: manpower | 74 of 77 |
| the others: money, fuel, crude oil | 76, 74 and 67 of 77 |
| the others: energy, metal, rare materials | none of 77 - their AI moved the shares of IC after the days were run, and a country that does not exist shares many a capital |

**The organisation rows are the ones `organisation.md`, section 7, had from a model** - an
hour before each day, `inferred` from three brigades. The original's order is one hour
after both days, and it gives the same 2942 and the same 142. The supply network's own
figures (`supply.md`, section 17) and the convoys (`convoys.md`, section 12) come out of the
same run.

## What is not established, in one place

- **what the AI does around those days**, law by law, and in which order - only that some
  of its changes come before them and some after (section 6);
- the **five speed thresholds**;
- the **month lengths**, value by value;
- the **yearly pass**;
- **several steps of the daily pass**, and the province's own monthly work;
- what the **second per-country hour gate** guards.
