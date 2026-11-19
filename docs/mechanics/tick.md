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
  scaled: **5, 2, 1 and 0.5 real seconds an hour, and 0.0001 at the top speed**, which is an
  hour every frame. They are fixed in the executable; no define or file sets them. Read
  2026-11-18, and the live run of 2026-11-10 agreed. `confirmed`
- **Reaching the threshold sets the accumulator back to nought** - the excess over it is
  dropped, not carried. So at most one hour comes due a frame, at every speed, and a long
  stall brings one hour and no burst. `confirmed`
- **Only one hour is ever in flight.** While an hour is being processed no further hour can
  be queued, however long the processing takes; time keeps accumulating meanwhile, so a
  slow hour is caught up afterwards rather than lost. `confirmed`
- **The hour is not advanced directly: a command is posted that advances it** - see
  section 4. `confirmed`

The thresholds, the reset and the one hour a frame are the original's (`CClock`,
`CSimulation.Advance`). Until 2026-11-18 they were our own - half a second, a quarter, a tenth
and a twenty-fifth an hour, as many as a frame's budget held at the top speed, and time owed
carried over under a cap - which played eight to twelve times the original's pace.

*Ours:* pause is a separate switch rather than a speed, and time that passes while paused is
not owed when it ends.

*Ours, decided 2026-11-18: the client runs its hours beside the frame*
(`CSimulation.BeginAdvance`, `Collect`). After a frame is drawn, the hour the clock says is due - one at
most, as above, the real time of every frame since the last added up - is started on a worker thread. The
frames go on drawing meanwhile, their time owed to the clock: the map's camera pans and zooms and its
surface follows, while the screens, the HUD and the tooltips keep what they last showed and the map's
layers what they last drew - **nothing reads the game until the hour is collected**, at the start of the
first frame after it is done. Then what it told is told, in the order it told it, on the main thread -
the clock's events and the state's, the messages and popups among them - and every event the player gave
meanwhile, held as it came, is handed to the interface in its order: a click waits at most one hour's
running. A command posted meanwhile waits too, and joins the queue as the hour is collected
(`CSimulation.Post`). **What in the held interface waits a time of the real clock reads the clock**, and does not
add up the times of the frames it is processed in: at the top speed most frames pass the HUD by, and an event
window's second before its options may be pressed, counted so, came to several (fixed 2026-11-19,
`CEventWindowView`). The interface then has at least that frame with the game to itself before the next
hour starts, so at the top speed an hour is run for every frame the interface has the game - the
original's hour a frame. Tools and tests run hours in line (`Advance`, `RunHours`), and both ways run the
same hours in the same order. Measured 2026-11-18 at the top speed, Germany, with the original's clock:
Their Finest Hour 83 frames a second and 55 days a minute, BlackICE 80 and 23 - where with every hour run
in the frame, under the clock of before, BlackICE drew 8 frames a second.

*Ours, decided 2026-11-19: at the top speed the hours run back to back.* An hour a frame left the worker idle
for most of each cycle - an hour started only after the frame that collected the last one was drawn, so an
hour of 14 ms took two or three frames, about 35. So at the top speed alone the worker goes on to the next
hour as one ends, for `CSimulation.BackToBackSeconds` (a twentieth of a second) of real time, and the frame
then collects them all; below the top speed it is one hour as before. **A run ends early after an hour that**:
- **tells what may stop the clock**: a message whose type pauses the game, as the original stops it in a
  game of one player (`CSimulation.OnMain`'s `mayStopTheClock`, which `CSession`'s messages pass by their
  type). A message that does not pause - another country's trade or event logged - and a sound or an offer
  to the player let the hours run on.
- **turns a day**, so that what the interface does at a new day - the autosave, the midnight hash of
  `--hash` - reads the game as that hour left it;
- **ends the game** or passes its end date, which the interface stops the clock at.

So the clock stops at the hour it would have, and nothing else the interface does depends on which hour it
is told an hour in. The interface and the player's clicks wait one run instead of one hour - a twentieth of a
second, or an hour where one is longer. Measured on 2026-11-19, Germany at the top speed, a frame log of a
minute each: **Their Finest Hour 73 days a minute to 132, BlackICE 35 to 45**, 77 to 85 frames a second both
ways (`docs/performance.md`, section 2). A run of a tenth of a second gave BlackICE nothing more.

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
5. Every province's hourly update: its points to its holder's faction (`factions.md`, section 7), an owned province in
   no area handed to the enemy around it (`war.md`, section 11), and a moved building's modifier made again
   (`modifiers.md`, section 3) - each province all three before the next.
6. **Combat**: every battle in progress is advanced one step.
7. The AI is then released to think, once the hourly pass can no longer be disturbed by it.

The supply network is also rebuilt inside the hour when something changed it.

**Weather has two places** (`weather.md`, section 4): **the fronts and high pressure before step 1**, so their draws are the hour's first; **every province's weather between steps 4 and 5**, so a
march sees last hour's rain and mud and a battle the hour whole. Each province's season is made again first in its day.

*Ours:* steps 1 to 3 and step 6 are built as far as there is anything for them to do.
**The generator is seeded once, when a game is made, and runs on from hour to hour** (`landcombat.md`, section 8).
**The AI comes after every pass the hour runs, the day's among them**, each country reading the same world, and what it
posts runs when the queue is next drained, before the next hour (read in the running game,
`reversing/findings/FINDINGS-liveread.md`, section 2). The original runs the 102 at once on worker threads, in a different order
every hour; here they go in turn. The hour's trading stays in the hourly pass, as the original's does. **Step 1 is
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

   **Here each step runs for every country in turn**, where the original runs every step of one
   country's day before the next country's. That is the same wherever a country's day reads only
   itself; **where it reads another country, the countries after it in the order have not had their
   day yet**. **The economy is built so since 2026-11-19**: a country's day's production put in its
   stockpile, a puppet's dues, what the sea cuts off of the land it holds gathered in a harbour, its
   debts repaid, its trade (`trade.md`, section 6), its IC, its conversions, the cap a shortage sets and
   its shares, and an exile's pool held, all of one country's before the next's - so a country's trade
   finds the money the countries before it made that day and not the money of those after, as the
   running game showed: the Soviet Union's money at each country's turn of one midnight
   (`reversing/findings/FINDINGS-liveread.md`, section 140). What every country's day reads of the
   provinces - each country's production and IC summed, the land each holds, the supply network - is
   made once, before the first country's day, nothing in the stretch changing it. The later stretch
   from its shares of leadership to its cabinet is built so as well, and the alignment's step (`alignment.md`, section 3): it is taken beside
   the surrender, a country at a time, reading the IC and the factions the earlier countries' days have
   left and the IC from before the day for itself and the later ones. And the most-threatening country
   (step 2's, with the neighbours) is worked out before any country's modifier is made, as the original's
   pass before the countries' days has it - the `neutrality` static reads the threat from it.
4. Per province: the province's daily work.
5. **Events**: every country's candidate events are tried against their mean time to
   happen. See the note below - this is not where the candidates come from.
6. **Intelligence is made again**, whole: `intelligence.md`, section 3. And at the end of every hour the provinces
   units left or entered are raised, or the whole made again where the hour changed who holds what or who is allied
   to whom (section 5). The answer on 2026-10-10 had been that it is probably daily, before the hour's raise was read; on 2026-10-14, with it shown, the decision was "Keep it as read."

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

0. **the weather**: every province's temperature set to its target, then `INITIAL_SIMULATION_HOURS_AHEAD` (96) hours
   of fronts and every province's weather (`weather.md`, section 4);
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
reserves at 28.000. The engine's own pass moves laws *before* the days (`politics.md`, section 13);
the Lua's look is made before them too, but what it posts waits on the queue and is run after them,
so a save's laws are not the ones its two days ran under (`politics.md`, section 12).

*Ours:* **built on 2026-10-10**: `CTick.RunFirstDays`, which a new game runs as it is made,
in that order and with the game "being made" throughout. Three things about it:

- **Since 2026-11-14 the Lua's changes of law are run once the days are**, with the game no
  longer being made, before the figures of the first screen are worked out and before the AI's
  first pass posts anything (`CPoliticsAi.RunNewGameLaws`). So every country whose laws the Lua
  moves is held to the running game's ceilings, the player's too, and a new game's first screen
  has every brigade's organisation the start save's (`OpenHOI3.SaveCheck firstdays`, 3,792 of
  3,792). Until then the changes were made before the days and nobody was held; the reserve
  division sent to attack before the first hour, which kept its own figure for that battle -
  35 for a German reserve, where its ceiling is 14 - is held as the original holds it.
- **The figures of the first screen are worked out afterwards for a running game**, by the
  pass that is run on entering one - its IC, leadership and manpower. **What a country made at
  home and used are not, nor what its units asked for and its reinforcement and upgrade needs**:
  they are what the second day left, as the original's first screen shows them (`resources.md`,
  section 3; `supply.md`, section 11; `upkeep.md`, section 8), so the top bar's ledger and the
  sliders' needs are the second day's. A loaded game's two needs are counted on entering, by the
  original's run of the shares with nothing to spend, and what its units asked for is nought until
  its first midnight. Until 2026-11-15 the home production and the three needs were estimated here.
- **It costs nothing to speak of**: two days at about a tenth of a second each. What a new
  game takes to make - six seconds or so - is the first convoys' routes, about four, and the
  filling, about two, and both were there before.

### Checked against the start save

`OpenHOI3.SaveCheck firstdays <install> <start-date save>` makes a new game as a player
gets it and sets every country beside the save: stockpile, manpower, officers, influence,
abilities, technology, the build queue, every brigade's strength and organisation. Since
2026-11-14 it is the save's for Germany, which the save's player has, in every figure, and for
the others in every brigade, technology and ability, in influence and officers, and in manpower
but for the United States, whose New Deal its set-up takes, and France (2026-11-14: 76 of 77).
Where it differs it is the AI's: the shares it has set by the save, and the headquarters it has
made.

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
| Germany: the stockpile | six goods of seven to the thousandth; supplies 0.030 over in 8410 - 3.301 under before what leaders do to a unit's use was built (`leaders.md`, section 8) |
| the others: organisation, not under an army with a commander | 2770 of 2770 |
| the others: organisation under a commander | 764 of 764 (142 before his part was built) |
| the others: strength | 3534 of 3534 |
| the others: manpower | 74 of 77 |
| the others: money, fuel, crude oil | 76, 74 and 67 of 77 |
| the others: energy, metal, rare materials | none of 77 - their AI moved the shares of IC after the days were run, and a country that does not exist shares many a capital |

**The organisation rows are the ones `organisation.md`, section 7, had from a model** - an
hour before each day, `inferred` from three brigades. The original's order is one hour
after both days, and it gives the same 2942 and the same 142 - 764 with the commander's part.
The supply network's own
figures (`supply.md`, section 17) and the convoys (`convoys.md`, section 12) come out of the
same run.

## What is not established, in one place

- **what the AI does around those days**, law by law, and in which order - only that some
  of its changes come before them and some after (section 6);
- the **month lengths**, value by value;
- the **yearly pass**;
- **several steps of the daily pass**, and the province's own monthly work;
- what the **second per-country hour gate** guards.
