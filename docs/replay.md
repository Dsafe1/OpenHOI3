# Replay: a game's AI and players recorded, and played back in their place

Ours, not the original's: the original has nothing like it. Built 2026-11-03 at the maintainer's word ("Make the Replay
system our next step"), because the headless runs that look into the AI's wars take twelve minutes each and most of them
re-run one war with more tracing; and the players' commands the same day (section 8), so that a game somebody played can
be recorded and watched again too.

## 1. What it is for, and what it is not

**A replay runs the game again with the AI's decisions, and the players', taken from a recording instead of being made.**
Everything else -
movement, battles, supply, production, events, the theatres - runs as ever. So it saves the AI's share of each hour, which
on 2026-11-03 was about seventy per cent: in thirty days of 1936, the unit agents 10.8 s and the AI's trading in Lua 7.7 s
of some 30 s (`OpenHOI3.SaveCheck profile`, Release).

**A recording holds only while the rules do not change.** It is the AI's commands for one game, hour by hour; a change to
what any rule does puts units elsewhere, and the recorded orders no longer fit what they find. So a replay serves:

- a run told more about - a trace added to the headless tool, or anything that only reads the game;
- a bug reproduced at its hour, as often as it takes;
- the check that the game is replayable from its seed and its commands at all (section 5).

It does not serve a fix tried: that needs the AI deciding again.

## 2. What is recorded

**The AI's steps of the hour** are marked in `CTick` by `AiStep` in place of `Step`:

| pass | steps |
| --- | --- |
| hourly | the offers answered, trading, the politics, intelligence, foreign, production and tech ministers, the placing of finished units, the unit agents |
| daily | the AI's strategy, the production sliders |

For each, as it runs, a `CReplayStep` keeps:

- **the commands it posted**, in order - everything it added to the outbox;
- **the draws it took** of the game's generator and of the AI's (`CRandom.Draws` before and after, the AI's written as `a`
  where it is not nought, since the AI's own generator of 2026-11-12, `mechanics/landcombat.md`, section 8);
- **the unit numbers it took** (`LastUnitId` before and after) - the fleet and air regrouping take a new unit's number as
  they post the split, as the original does, and the number travels in the command.

A step that seeds the generator again cannot be recorded, and stops the run.

**Not marked, and run in a replay too**, being parts of the game the AI's steps would otherwise change without a command:

- **the theatres** (`CTheatres.RunHourly`): they make and disband headquarters units and move units between commands. They
  are worked out from the game alone and cost 0.6 ms an hour.
- **what entering a game runs**, the AI's laws, strategy and first pass over its units with the rest: a replay begins with
  the first hour, both games having been made the same way.
- **the foreign ministers told of a new pair at war** (`CForeignAi.TellWar`), which the war code calls: the Lua is loaded in
  a replay as in any game, and answers from the game.

## 3. Playing it back

Where a step is played back, it does not run: its kept commands go to the outbox where it would have posted them, the
generator is moved on by its draws (`CRandom.Skip`) and the unit numbers by those it took. The recording's next step has to
be the hour and the step the game has reached; where it is not, the run stops and says so.

**What the AI keeps for itself does not move in a replay** - its agents, plans, prepared wars and the Lua's own memory -
so a tool that reads them in a replay reads them as they stood at the first hour. Nothing of the game reads them.

## 4. On disk

`CReplayFile`, compressed with gzip: a line naming the format (`OpenHOI3 replay 2`); a line of what the game has to be
made with, the countries played and the seed (`CReplayHeader`), which a playback checks and refuses where they differ;
then a line of JSON an entry, in the order they happened, each with `k` its kind - `s` one of the AI's steps (`h` the
hour, `s` the step, `d` the draws, `u` the unit numbers, `c` the commands), `p` a player's command (`t` the drains before
it, `c` the command), `t` an hour begun (`t` the drains before it). About 320 KB a month of 1936. It is written as the game
runs, so a run that stops leaves what it reached, and read an entry at a time, so that what a command names exists when it
is read.

`CCommandCodec` writes a command field by field, whatever its kind: a command holds its own targets as plain values. What
comes from the loaded files is written by name - a region, a casus belli type - a rebel faction by its number, and an
event's scope by its figures. **A command holding a live part of the game** - a unit, a country, a province, a war, a leader
- **or a delegate cannot be written, and the recording stops** rather than leave a step that will not play back.

## 5. Checking it

**The hash.** `CStateHash` fingerprints the game in six parts - how far the generator has gone, the countries, what they
hold of each other, the provinces, the units, the wars - and, field by field, each country's figures, province's and unit's.
It leaves out what the AI keeps for itself.

- `OpenHOI3.SaveCheck headless <install> <date> hash` writes the parts each midnight: **two runs of one game agree on every
  line** - January 1936 on 2026-11-03, 31 of 31 days.
- `OpenHOI3.SaveCheck replaycheck <install> [date]` runs two new games hour by hour side by side, the first recording its AI
  and the second playing it back, every step taken through the file's form and back; it sets them beside each other at
  every midnight and, on the first day they part, names each figure that differs. On 2026-11-03: **608 of 608 days to 1
  September 1937**, through the Spanish Civil War and the war in China - 254,057 commands - the replay's hours 224 s to the
  recording's 584 s.
- `OpenHOI3.SaveCheck headless ... record=FILE` and `replay=FILE` do it through a file: January 1936 recorded in 35.1 s and
  replayed in 11.1 s, the hashes 31 of 31.

**What the check found and mended, 2026-11-03:**

1. The unit numbers the regrouping takes as it posts a split - all the build queue's numbers one or two out from the first
   day - recorded and moved on (section 2).
2. The naval body's intercept gave the fleets that join the first their join order directly, where the regrouping and the
   invasions post a `CJoinUnitCommand`: it posts one too (`moving.md`, section 14, phase 9). The order now takes effect in the
   same hour's next drain, as the intercept given the first fleet already did.
3. The theatres made and disbanded headquarters units in an AI step: run in a replay too (section 2).

## 6. Watching one in the client

`--replay=FILE` (2026-11-03, the maintainer's choice of the first and third of three pieces): the client makes the game
with **the countries played the recording's header names**, and plays the recording back from the first hour. Who plays
changes the game itself - a played country is left its events to answer, its idle fleets start no battle, its ministers
do not run - so a replay played by another would part from its recording at the first such point; `--country` is left
aside.

**A replay looks on: everything is shown and nothing pressed does anything** - the maintainer's word on 2026-11-03, after
a first try that left out what stood for no country, the top bar with it: "show everything, but block actions". **A replay
takes no command from outside** (`CSimulation.Post` answers false, `TakesCommands`), and a paused frame does not drain the
queue as it otherwise does - the recording says when to. And on top of that, what may be pressed is shown so:

- **any unit is selected by a click** - a counter, or a fleet in port or an air unit at its base, anybody's in turn - and
  Shift and Ctrl work as for a player's; the unit panel and the battle window are given no player, so their checkboxes,
  their cancel and retreat buttons and the leader window's choices cannot be pressed, and a right click orders nothing
  (`CMapView.LooksOn`);
- **the province view** comes up for any province, its building rows, underground and covert operations greyed;
- **the top bar** is all there.

**Recorded by the headless run, nobody played**, there is no country to look through, and by the maintainer's choice that
day **each thing is seen through its own country's eyes**: a province through its controller's, else its owner's - what
the view says of it hangs on how well the country looking knows it - and a unit through its owner's. The top bar's figures,
flag and name are empty, and its buttons open nothing, there being no country's screens: the production, technology,
diplomacy, politics and intelligence screens, the deploy menu, the alerts, events, messages and offers stand for the
country played and are not built. **Recorded in the client** (section 8), the screens are the played country's, and the
province view sees through its eyes.

**Where the recording ends**, the hour asks for a step it does not hold (`CReplayEndedException`): the clock stops there
for good and the client says so, that hour being left part run. Tried on 2026-11-03 with January 1936 run past its end
with `--run --click=h800`.

**Not built**, the second piece: any country to look through without control - `--observe=TAG` - in a game nobody played,
its screens with it. Nor are a played country's screens greyed in a replay: their buttons press to no effect, and that
they do nothing is all that is shown of it.

## 8. A game somebody plays

Added 2026-11-03, after the maintainer asked whether every command could be caught at the queue rather than tied to the
AI: it can be told apart there, and must be, because a command the game posts itself comes out again by itself in a
replay, and one given twice would run twice. So **`CSimulation.Post` is the way in from outside the game** - a player's
screens, the developer switches, a tool - and only what comes that way is recorded as a player's; what the game posts as
a command runs (the outbox) and the hour's tick are queued another way and not recorded.

**When a command from outside runs** depends on how often the queue has been drained since it was posted: a paused client
drains it every frame, and what a command posts runs in the next drain. So a recorded post keeps the drains before it
(`CCommand.Turn`), and so does every hour's tick (`CReplayTick`); a replay drains the queue until it has been drained as
often, then posts - `CReplay.BeforeHour`, run before each hour's tick - and a recorded post or hour that finds the queue
drained more often than that stops the replay.

`--record=FILE` in the client records the game played: its header names the country played, and the player's orders, the
AI's steps and the hours go in as they happen. **Tried on 2026-11-03**: ten days played as Germany, a research queue and a
unit ordered through the developer switches and seven trade offers posted for Germany between hours 343 and 371, recorded
and replayed in the client with `--hash`, the hashes ten of ten. And `ReplayTests` has a player's command whose consequences
run in later drains, one of them a paused frame's, come out in the same order replayed.

## 7. A savegame of our own

Not built. It would start a run anywhere - 30 June 1937 - and, unlike a recording, serve a fix tried as well. It would
write the game, and what of the AI's own the original's saves hold too: the theatres, each theatre's plan and the AI's
strategy lists (the `area_theatre` and `ai` blocks). **Not the Lua's state**: the original's saves hold none, so a loaded
game starts its scripts afresh, and Their Finest Hour's keep nothing between calls - their only assignments at the top of a
file are three tables of functions (`Utils`, `Support_Trade`, `Support_License`).

## Not established

- **What else of the AI writes the game directly** in what the checks so far have not reached; the hash names it the day it
  shows.
- **Whether the Lua answers a call from outside the AI's steps the same** where its own memory has moved on in the recording
  and not in the replay - so far nothing has shown it.
