# Replay: a game's AI and players recorded, and played back in their place

Ours, not the original's: the original has nothing like it. Built 2026-11-03 by the decision of that day to make the
replay system the next step, because the headless runs that look into the AI's wars take twelve minutes each and most of them
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

**What sections 2 to 9 describe is for development.** It records from one starting point - a new game, or one save
loaded - and plays back from that point, so it never crosses a load. **The replay wanted for players** (2026-11-12)
records a whole game from its start to its end, through every save and load, to be watched afterwards. **Its recording
is built since 2026-11-25** (section 10): the game is not made to go on after a load as it would have without one -
which a save in the original's keys cannot give, `enhanced.md` - the playback makes the same load instead.

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
  they post the split, as the original does, and the number travels in the command;
- **whether its asking made what the game makes for whoever asks first** - the table of who stands where
  (`CGameState.Positions`) and the supply network with the areas it remaps (`CSupplyNetworks.Refresh`), written `m` and
  `n` where it did. A step played back makes them at its place, before its commands go to the outbox; otherwise the
  game playing back would make them at its own next asking, another moment, and two units made either side of it would
  stand in another order (2026-11-20; `multiplayer.md`, section 3). A decision the game's pass calls is kept where it
  did only this, too.

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
**A strategy's lists are the exception since 2026-11-20**: the replaying game makes them itself right after the daily
step it played back, on the same days (`CAiStrategies.RunDailyFollowing`), for a dialog's chance on a machine that
follows a host; `replaycheck` sets them beside the recording game's each midnight (`multiplayer.md`, section 12).

**A decision of the AI's that the game's own pass calls** reads them all the same, and so is kept as a step is, through
`CReplay.RunCalledStep`: the sealift's look as a land unit enters a province in the march (`CSealiftArrival.OnEntered`;
`mechanics/moving.md`, section 15), which boards it onto a waiting transport and sails the fleet, reading the agent's
transports, its plan's embark and debark and the unit's sealift target; and since 2026-11-18 the agents told of a province
their country lost (`CProvinceLost.ControllerChanged`, named by the province's number; `mechanics/moving.md`, section
7.7), which reads the agents' land units. It is kept **only where it posted, drew or numbered
anything**, under a name that carries the unit's number; replaying, the march calls it in the same order, and it takes the
next entry where that is this hour's of its name and does nothing where it is not - so it looks at the next entry without
taking it, one entry read ahead, since a recording on disk is read an entry at a time. *Found 2026-11-15*: from 2026-11-14
the decision ran in the replay as in the game, deciding there on the first hour's agents, and a recording and its replay
parted on 5 January 1936 - Italy's 22a Divisione and its 4a Squadra - 3 of 31 days the same, until it was kept so. **A new
call from the game into the AI must go the same way**, or be worked out from the game alone.

**What the game keeps between askings is made ready before every step, by the game** (`CAiEntry.Prepare`), recorded,
played back or neither. The supply network is kept until a province changes hands (`mechanics/supply.md`), and the AI
asks for it all the time; a step that made it again left the game the network of that moment, where in a replay, the step
not running, it was made later by whatever the game asked next. The network hangs on more than who holds what - the
infrastructure, the naval bases, the capitals, the puppets - so the two could differ, and the day's gathering of what lies
in cut-off land with them. *Found 2026-11-20*: a headless run's recording and its replay parted at midnight on 19 July
1942, Germany's stockpile 35,422 supplies against 30,963 and 148 provinces' pools beside it, the generator, the units
and the diplomacy the same, in two runs that agreed with each other to the byte. So a step never makes the network
itself now, and **anything else the game keeps that a step can make on first asking goes into `Prepare` the same way.**

**A run of provinces handed over together makes the network once** (`CProvinceHands.Batch`, 2026-11-21): a puppet's land,
an annexation's two walks. Each change makes it ready before the AI's answer to the lost province, and within such a run
each one was thrown away at the next change, only the last lasting; so a change in a run that no agent would answer -
asked of the game's state alone (`CProvinceLost.MayAnswer`) - leaves it to the run's end, where it is made once. The
moment the lasting network is made is unchanged, which is what matters: skipping the making after a single change, the
first try, moved it later and parted the state's hash on 8 January 1936. With the run, the hash is the same every
midnight to 1 February 1936 and from the 1944 bookmark to 20 November 1944, and the surrender of 17 November 1944,
which made the network again for each of a puppet's provinces, takes 0.6 s where it took 5 to 6.

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

`--replay=FILE` (2026-11-03, the first and third of three pieces, as chosen that day): the client makes the game
with **the countries played the recording's header names**, and plays the recording back from the first hour. Who plays
changes the game itself - a played country is left its events to answer, its idle fleets start no battle, its ministers
do not run - so a replay played by another would part from its recording at the first such point; `--country` is left
aside.

**A replay looks on: everything is shown and nothing pressed does anything** - decided on 2026-11-03, after
a first try that left out what stood for no country, the top bar with it: "show everything, but block actions". **A replay
takes no command from outside** (`CSimulation.Post` answers false, `TakesCommands`), and a paused frame does not drain the
queue as it otherwise does - the recording says when to. And on top of that, what may be pressed is shown so:

- **any unit is selected by a click** - a counter, or a fleet in port or an air unit at its base, anybody's in turn - and
  Shift and Ctrl work as for a player's; the unit panel and the battle window are given no player, so their checkboxes,
  their cancel and retreat buttons and the leader window's choices cannot be pressed, and a right click orders nothing
  (`CMapView.LooksOn`);
- **the province view** comes up for any province, its building rows, underground and covert operations greyed;
- **the top bar** is all there.

**Recorded by the headless run, nobody played**, there is no country to look through, and by the choice made that
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

Added 2026-11-03, after the question was raised whether every command could be caught at the queue rather than tied to the
AI: it can be told apart there, and must be, because a command the game posts itself comes out again by itself in a
replay, and one given twice would run twice. So **`CSimulation.Post` is the way in from outside the game** - a player's
screens, the developer switches, a tool - and only what comes that way is recorded as a player's; what the game posts as
a command runs (the outbox) and the hour's tick are queued another way and not recorded. A command posted while the
client's hours run beside the frame (`tick.md`, section 2) is held, and posted - and recorded - as they are collected.

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

**A game following another as it runs** (2026-11-20, for a game between several machines; `multiplayer.md`): the
recording is its own wire. Three things were added for it, none of which a recording played from its start needs:

- **a drain with no hour** is kept (`CReplayDrain`, written `q`) wherever a drain of the queue outside an hour ran
  anything in a game being recorded - a paused frame's, a tool's `Pump`. A playback from the start drains as far before
  the next hour in any case; a follower would otherwise not run an order given while the clock stood until the clock
  ran again;
- **`CSimulation.Follow`** (`CReplay.FollowBetweenHours`) takes the posts and drains that have come and no further, and
  **`HourHasCome`** says whether an hour's tick is next - a follower takes what was ordered first, then runs the hour
  when it has come;
- **`CSimulation.ToHost`**: a game replaying takes no order from outside, but one that follows hands its own player's
  to the game it follows, which posts it; it runs on both when it comes back in that game's order.

`TwoMachinesTests` runs the two in one process with every entry through the file's form: an order from each machine in
one paused frame, then hours, three days long, the hashes alike each midnight.

## 7. A savegame of our own

Not built. It would start a run anywhere - 30 June 1937 - and, unlike a recording, serve a fix tried as well. It would
write the game, and what of the AI's own the original's saves hold too: the theatres, each theatre's plan and the AI's
strategy lists (the `area_theatre` and `ai` blocks). **Not the Lua's state**: the original's saves hold none, so a loaded
game starts its scripts afresh, and Their Finest Hour's keep nothing between calls - their only assignments at the top of a
file are three tables of functions (`Utils`, `Support_Trade`, `Support_License`).

## 9. A run of the original's, played back draw for draw

The other way round from the rest of this file: not our game recorded, but **the original's draws put into ours**, so that a
difference between the two in the series of the original's hourly saves is a rule and not a roll. Built 2026-11-15, when
Germany's one-in-six call of Italy into its war against Mongolia (`docs/worklist.md`, *Next up*, item 5) moved the July 1941
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

**The naval patrol's centre** is drawn on the C runtime's `rand()` of the thread that runs the country's pass, which no game
state gives back. Recorded with `hoiDrawsPatrol` set (the hour's pass on a software breakpoint, its debug register given to
the patrol's `rand()`; it cannot be set together with `hoiDrawsGameCallers`), each draw is a `P` line, filed to its country
like Lua's, and handed to the patrol through `CGameState.PatrolRandFeed`; a recording without `P` lines leaves the patrol
its own stream (`reversing/findings/FINDINGS-liveread.md`, section 112). On wr13, 2026-11-16, every one of the original's
39 patrol draws was handed over and ours made one more, and the fleets' orders agreed, 29 of 29.

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

**What it does not do**: the trading's states (`CAiWorkers`) keep their own generators and are not fed; and only one run
is followed, so whether the original agrees with itself still wants its other runs.

**What the recording does to the original**: `hoidraws.lua`'s breakpoints hold the threads they watch, and on entering
July 1941 that changes the order a theatre's area of operations takes its fronts in - Smolensk HQ's other front's 779
first, where an unprobed entering keeps the save's order with it last - which moves the land attack's ramp and so the
stance of its targets (`reversing/findings/FINDINGS-liveread.md`, section 147). Ours keeps the save's order, as the
unprobed game does; a playback of such a recording parts on entering wherever that matters - `wr11`'s and `wr21`'s
Soviet attacks on 1576 and 1440 - and that parting is the probe's, not a rule's.

## 10. A game recorded beside its saves, and played back across them

Built 2026-11-25, the first piece of the replay for players, by the decisions of that day: of two ways across a
save and a load - an enhanced save that loses nothing, or a playback that makes the load too - "go with solution 1",
the second; recorded by "a lobby option"; and "recording through loads" before any screen to watch one on.

**The idea.** A recording is the orders given to one game. A game loaded from a save is another game than the one
that was saved - some sixty things differ (`enhanced.md`, *A whole game's replay*) - so the orders given after a load
fit only a game that was loaded too. So **a playback, where the player loaded a save, writes a save of its own and
loads it**: it loses exactly what the played game lost, and nothing has to be added to the save for it. Each of those
sixty things mended later makes the step at a crossing smaller and changes nothing here.

**It rests on two things**, both checked: a save written from the same game is the same save, and a save loaded
twice is the same game.

### 10.1 On disk

Beside the saves, in `save games/openhoi/replays` under the player's folder (`CReplayStore`):

- `<name>.replay` - **a recording**, in section 4's form (format 3; 2 is read too), from one entering of a game to
  wherever it was left. Its header also holds its own name, **its parent** - the place it was begun from, none for a
  recording with a start of its own - what it was made with (the engine's version and the rules) and the lobby's choices
  the game was entered with, as the numbers they travel between machines as.
- `<name>.start` - for a recording with no parent, **the game it begins from, as a save**, compressed.

**A save's mark** (`CReplaySaved`, written `v`: `m` its number in the recording, `t` the drains before it) goes into
the recording wherever the game is saved - the menu's Save Game, every autosave - and the file is flushed there, so
everything up to a mark is on disk before the save that names it. **A save names its place**: an enhanced save's top
holds `openhoi_replay="<name>:<mark>"` (`CSaveTop`, `CReplayLink`; `savegame.md`, section 23). A name is letters and
digits alone, being a file's name.

So the game that led to any save is **a chain of recordings** (`CReplayStore.Chain`), each played to the mark the next
was begun at. Loading an older save and playing on begins another recording with the same parent: a fork, and both
lines keep.

### 10.2 Recording

**The lobby's rule `record_replay`** (`CGamePlaySettings.RecordReplay`; the tick is in the mod's window
`openhoi_lobby_rules`, its texts `OPENHOI_RULE_RECORD_REPLAY` and `_TIP`), an enhanced game's alone, off unless ticked,
and in no save: a loaded save shows it ticked where it names a recording. Not between machines yet.

On Play (`Main.BeginRecording`):

- **A save that names a recording still there goes on from it**: the save's file is loaded again, the lobby's choices
  written into it and the country chosen taken up (`CFrontEnd.TakeUpLoaded`), and a recording begun whose parent is the
  save's place.
- **Anything else begins a recording with a start of its own** - a new game, or a save that names none: the game as
  Play made it is written as a save, kept as the `.start`, and **loaded**.

**The game recorded is always one loaded from a save, on a Lua begun afresh** - *ours*, because that is the one thing a
playback can make again: what the lobby made a new game from - its bookmark, its victory conditions, whatever a later
lobby option adds - is all in the save, and the mod's Lua holds nothing of the front end's making of worlds. The price
is the tooltip's: **a recorded new game is saved and loaded once as it begins, and a save played on is loaded twice**.
A recorded new game is therefore the game a player gets by saving at the first hour and loading - not the game an
unrecorded Play gives.

### 10.3 Playing back

`--replay=<save>.hoi3` (`Main.PlaySaveRecording`): the chain to the save is found, the first recording's start loaded
and its recording played to its mark. **At the mark the recording ends for the game played back** (`CReplay.Replaying`
with a mark; the hour asked for next is `CReplayEndedException`'s, before anything of it runs), which stands where the
game was saved: it is written as a save, that save loaded in the session's place under the loading screen - the choices
and the country of the next recording's header taken up as Play took them - and the next recording played. The camera
and the speed go across. At the last mark the clock stops, as at any recording's end.

**A game played back writes no autosave** (it would be written over the player's own) and marks nothing.

A recording made with another engine or other rules is said, and played all the same: it stops where it no longer
fits. *Not built*: any test of the mod's files, which a recording hangs on as much.

### 10.4 Checked

- `OpenHOI3.SaveCheck replayload <install> [save date ...] [player=TAG] [folder=FOLDER]`: a game recorded beside its
  saves, saved and loaded at each date, each loaded game recorded on; then the chain to the last save played back, a
  save written and loaded at each crossing, and every midnight's hash set beside the played game's. On 2026-11-25:
  the base game with nobody playing, saves on 10 and 20 January and 1 February 1936, **31 of 31 days, across 2
  loads**; BlackICE as Germany, saves on 6, 12 and 18 January, **17 of 17, across 2 loads**, each loaded save giving
  back its place. Before the chain was built, the bare question: a recorded game and its playback each saved on 15
  January wrote **the same save byte for byte**, and the two loaded games were alike for the 31 days after.
- **In the client**, 2026-11-25, BlackICE as Germany with `--recordreplay` (the rule, by switch), `--save=one` after
  six days and three more played; then `--load=` of that save recorded on, `--save=two` after five days; then
  `--replay=` of `two.hoi3` with `--hash`: **11 of 11 midnights' hashes those of the two games played**, the five after
  the load with them - where the first game's own seventh day, played on without a load, hashes otherwise.
- `ReplaySavesTests`: a mark and what a save is given of it, a playback ending at its mark with what was ordered before
  the save run and nothing after, the chain by its parents with a fork, a missing recording and a ring, a file still
  being written read to its last mark, a place's text and a name's letters, the header's choices, the start's file, and
  the save's key both ways.

### 10.5 Not built

- **A country to look through** other than the one that played (section 6's second piece), and a list of replays
  apart from the list of saves (section 11).
- **A game between machines.** The host's recording is its wire already (section 8); nothing keeps it beside the saves.
- **Moving through a recording**: a playback runs every hour from the start. A start kept at each crossing, or at
  intervals, would let one begin late.
- **Clearing recordings no save leads to**, and telling the player how much they hold: about 320 KB a month of 1936 in
  the base game, 560 KB for BlackICE's first six days with its start.
- **The second load of a save played on** could be spared wherever the mod's Lua is known to keep nothing.
- **A recording that outlives its engine**: section 1's limit stands - it holds only while the rules do not change.

## 11. Watching one from the lobby

Built 2026-11-25, on the word of that day to begin the interface too. Ours throughout.

- **The list of replays is the list of saves.** A save is a place in a game, and its replay is the game up to it; so
  nothing new is listed. In the lobby's Saved Games a save is loaded as ever, and where it names a recording that can
  be followed back to a start that is there (`CReplayStore.Probe`, asked once as the save loads), **a window of the
  mod's, `openhoi_lobby_replay`, is shown with the button `watch_replay`** (`CLobbyOptions`; BlackICE's
  `interface/openhoi/lobbyreplay.gui`, under its window of rules). Not shown between machines.
- **The button** says `OPENHOI_WATCH_REPLAY`; its tooltip is `OPENHOI_WATCH_REPLAY_TIP` with `$COUNT$` the number
  of recordings - or, where one of them was made with another engine or other rules, the button is greyed and the
  tooltip is `OPENHOI_WATCH_REPLAY_OTHER_ENGINE` with `$MADE$` and `$NOW$` (`CReplayText`, which has words of its own
  where a table has no key). So a recording that will not fit is said before it is tried; `--replay=` still tries it.
- **Pressed**, the front end gives way to the playback under the loading screen (`Main.WatchReplay`), as to a game.
  The game comes up **paused, as every game does**, looked on through the country that played (section 6); pause and
  speed are the top bar's.
- **At a save the player loaded** the loading screen comes up with `OPENHOI_REPLAY_LOADING` over it, and the playback
  goes on at the speed it had.
- **At its end** the clock stops and `OPENHOI_REPLAY_ENDED` stands in the upper part of the screen, in the notices'
  font (`CScreenNotice` with a band), over the game as it stood at the save. The menu's Resign leads back to the main
  menu.

**Tried on 2026-11-25** with `--click` on BlackICE, in a player's folder of its own: Saved Games, the save `two` of
section 10.4's trial chosen - the window and its tooltip looked at, "Record a replay" ticked for it - the button
pressed with `--run`: the playback ran to 7 January, loaded, and ran on to its end on the 12th by itself. The end's
notice was looked at in the playback by switch. **Not reviewed in play**; not tried: a recording of another engine in
the lobby, Resign out of a playback, and a playback begun a second time in one run of the program.

## Not established

- **What else of the AI writes the game directly** in what the checks so far have not reached; the hash names it the day it
  shows.
- **Whether the Lua answers a call from outside the AI's steps the same** where its own memory has moved on in the recording
  and not in the replay - so far nothing has shown it.
