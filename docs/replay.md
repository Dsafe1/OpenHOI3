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

**It is for development only.** It records from one starting point - a new game, or one save loaded - and plays back from
that point, so it never crosses a load. **The replay the maintainer wants for players** (2026-11-12) records a whole game
from its start to its end, through every save and load, to be watched afterwards; that needs a game to go on after a load
exactly as it would have without one, which a save in the original's keys cannot give. It is in `enhanced.md`, and every
addition is checked against it (`CLAUDE.md`, *Always*).

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

**A decision of the AI's that the game's own pass calls** reads them all the same, and so is kept as a step is, through
`CReplay.RunCalledStep`: the sealift's look as a land unit enters a province in the march (`CSealiftArrival.OnEntered`;
`mechanics/moving.md`, section 15), which boards it onto a waiting transport and sails the fleet, reading the agent's
transports, its plan's embark and debark and the unit's sealift target. It is kept **only where it posted, drew or numbered
anything**, under a name that carries the unit's number; replaying, the march calls it in the same order, and it takes the
next entry where that is this hour's of its name and does nothing where it is not - so it looks at the next entry without
taking it, one entry read ahead, since a recording on disk is read an entry at a time. *Found 2026-11-15*: from 2026-11-14
the decision ran in the replay as in the game, deciding there on the first hour's agents, and a recording and its replay
parted on 5 January 1936 - Italy's 22a Divisione and its 4a Squadra - 3 of 31 days the same, until it was kept so. **A new
call from the game into the AI must go the same way**, or be worked out from the game alone.

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

## 9. A run of the original's, played back draw for draw

The other way round from the rest of this file: not our game recorded, but **the original's draws put into ours**, so that a
difference between the two in the series of the original's hourly saves is a rule and not a roll. Built 2026-11-15, when
Germany's one-in-six call of Italy into its war against Mongolia (`readme.md`, *Next up*, item 5) moved the July 1941
series by half each time an unrelated commit shifted the scripts' shared `math.random`. It is a measuring instrument of the
savegame tool and nothing of it is in the game.

**Recording**, on the running original with the fact base's `probes/hoidraws.lua` (`reversing/findings/FINDINGS-liveread.md`,
section 103), beside the hourly saves `hoiseries.lua` takes: each hour, the game's generator and the AI's dumped as the
hour's pass begins; every draw on the AI's generator with the index it drew at and its caller; every value Lua's
`math.random` was given by the C runtime's `rand()`; and each country's AI pass as it begins on a thread. The original runs
the 102 passes side by side on worker threads in a different order every hour, so a draw is filed under the country whose
pass runs on its thread; the hourly trading, which runs inside the pass, under the country it trades for.

**Playing back**, `OpenHOI3.SaveCheck load ... series=... draws=FOLDER` (`CDrawPlayback`): as each hour begins the game's
generator is put where the original's stood (`CRandom.Restore`), and every draw a country's AI makes is answered with the
original's for that country that hour, in order - on the AI's generator through `CRandom.Feed`, which the AI's draw sites
reach by naming the country they draw for (`CRandom.Next(who)`, the trading's with its kind), and in the Lua's
`math.random` through `CAiScripts.RandFeed`, the country being the outermost script call's (`CGameApi.Drawer`). A value of
the AI's generator is worked out from the hour's dump and the index it drew at, so a draw the probe missed moves no other.
Where ours draws more than the original did that hour, the draw is ours and counted ("ours beyond"); where less, what is
left is counted. Each hour of the series says so in one line.

**What it showed first**, the July series against one recorded run: 267 ways the same, 59 ours alone, 21 none and 6 at
another end, where the same run without the draws gave 186, 128, 27 and 81 (the first recording, its trading unfiled: 252, 62, 27 and 14 against 167, 140, 38 and 88). And that ours rolls the hourly trading for
every country every hour, about 180 draws, where the original made 4 to 8 - an order of its own for the queue.

**The load's draws are made again** (2026-11-16): the original seeds its AI generator once, at start-up, with the whole
seconds its process has run, and its load makes every country's ministers on it, one draw each (`mechanics/savegame.md`,
section 2). The playback finds that seed as the one whose generator, twisted once, holds the first hour's dumped words - 5
in all the recordings so far, the save loaded about five seconds after launch - seeds ours's AI generator with it and makes
the ministers again (`CAutomation.MakeMinistersOfLoad`), so every first hour of a minister's is the original's run's and
entering draws on from where the original's did.

**Entering is recorded too** (2026-11-16): armed between the load and Play (`hoiBeforePlay` in the fact base's `hoi.lua`),
the recorder logs entering's two passes, and the playback hands what comes before its first hour marker to entering -
Holland's foreign minister's decisions and Greece's politics minister in July 1941, the save's hour being theirs. **And
the units' first looks**: the original never initialises the cache of the half of a unit's modifier that hangs on the unit
alone, so a loaded unit's first estimates take what memory held where its dirty byte is nought (`mechanics/moving.md`,
section 11.4). `probes/hoimodelcache.lua` dumps every unit's two caches before Play (`caches.txt`), and the playback hands
them to entering through `CGameState.FirstLookModel`, a defender's that is not a number as such - the original's
"their damage is not above nought" then takes it, and the odds are 50: so the original's Soviet attack on 2701 on entering
the July 1941 save of one run, which a German division's NaN half gave, is ours too, and with it two Slovak re-routings.
Against such a recording (`wr5`) the July series gave 278 the same, 20 none, 4 at another end, and about 26 ours alone
outside its last hour.

**Where the game's generator stands at each battle** (2026-11-16): the playback restores the game's generator each hour,
but within the hour ours's draws must come in the original's number and order for a battle's dice to be the original's.
`battles=FILE` now writes, for each battle each hour, the generator's draws so far, and the fact base's
`probes/hoibattlegen.lua` - or `hoidraws.lua` with `hoiDrawsGameCallers` set, which also logs the caller of every draw on
the game's generator from a watchpoint on its index - gives the original's. Set beside each other they found the bomber's
naval sighting roll out of the wrong figure (`mechanics/navalcombat.md`, section 1); with that mended every draw before the
first battle and every battle's dice are the original's for eight hours of July 1941, after which one battle fires two
shots fewer in ours, not yet followed.

**What it does not do**: the trading's states (`CTradeWorkers`) keep their own generators and are not fed; and only one run
is followed, so whether the original agrees with itself still wants its other runs.

## Not established

- **What else of the AI writes the game directly** in what the checks so far have not reached; the hash names it the day it
  shows.
- **Whether the Lua answers a call from outside the AI's steps the same** where its own memory has moved on in the recording
  and not in the replay - so far nothing has shown it.
