# Multiplayer

What is decided about playing one game on several machines. **Nothing of this is built.** It is the last large piece
the normal engine lacks, and its shape is being worked out from 2026-11-20; this file holds what has been decided, in
the order it was, and what is still open.

## 1. What the original gives us to go by

Little. The fact base has the original's commands whole - how one is stamped, written and run in a single ordered loop
(`reversing/findings/FINDINGS-commands.md`) - and almost nothing of its networking: the transport, the client's side,
what it does about machines falling out of step and the bodies of its multiplayer commands were never read (a survey of
2026-11-20). What is known and bears on a design:

- only a machine that may post advances the clock, and a client's hour comes to it in the host's tick command
  (`reversing/findings/FINDINGS-tick.md`; the last step *inferred*);
- speed and pause are commands, and only the host has the speed buttons (`reversing/findings/FINDINGS-topbar.md`);
- a lobby sends its save to every client, and a client whose own file has that name and checksum loads its own
  (`reversing/findings/FINDINGS-savegame.md`, A.1);
- each machine makes its object ids in a number space of its own, so two cannot give one id to two things
  (`reversing/findings/FINDINGS-savegame.md`, B.3);
- no comparison of the game's state between machines was found.

## 2. Decided

All on 2026-11-20.

- **The networking is this engine's own.** "We shouldnt get limited by the old in this regard": no wire format, lobby
  protocol or metaserver of the original's is followed. The rules of play stay the original's; a machine running the
  original and one running this cannot share a game, and are not meant to.
- **The AI runs on the host's machine, always.** This settles the worklist's question of 2026-10-13. A client runs no
  AI: what the AI does reaches it as commands, with the draws and the unit numbers the AI's steps made - what a replay
  already plays back in the AI's place (`replay.md`).
- **Every machine runs the whole simulation** (lockstep). A client is sent commands, not state. It is what the engine
  is built for - every change a command, a game replayable from its seed and its commands - and it keeps the traffic
  small.
- **Machines compare the state's hash each midnight**, the hash a replay is already checked by (`replay.md`). A client
  whose hash differs is sent the game again as a save. Midnight "for now": how often is a question of cost.
- **A player may join a game that is running**, by the path a differing client takes - the save sent, then the
  commands from its hour on - **and the host may turn that off.**
- **Machines connect directly, by address.** As the original is played today, over the open network or a virtual one.
  No game list, relay or account.
- **Two checksums.** The original's file checksum is kept as it is, so that players can still set a mod's letters
  beside the original's (`mechanics/checksum.md`). A second one of this engine's own covers what the first does not -
  the engine's version, the enhanced engine's rules, and every file a game reads by path and content
  (`enhanced.md`, *The file checksum*) - and is what machines compare before they play. **Built 2026-11-20** as
  `CPlayChecksum` (`mechanics/checksum.md`, section 8): the files that decide how a game goes, the AI's Lua among
  them, and not the interface, the pictures or the localisation. The client does not work it out yet.

- **A game sent to a machine is loaded by every machine, the host too - until there is a snapshot.** A save in the
  original's keys does not hold everything a game goes on from, and a game loaded parts from the one that saved it
  within the hour (`enhanced.md`, *A whole game's replay*; `OpenHOI3.SaveCheck reload`). So when a client is sent the
  game - on joining, or because its hash differed - the host saves and all machines load that save, which the same
  load makes the same game on each: the original's lobby does no other. The game changes a little by it, as by any
  load. When the enhanced engine's save holds the whole state, that is sent to the one machine and nobody else
  loads; what carries a game between machines is made to take either.
- **A player's order is acted on at once.** It is never kept for an hour's turn: a client sends it to the host the
  moment it is given, and the host puts it into the stream ahead of the next hour it runs, as it does its own
  player's - "a delay in that case can be fatal". What stands between the click and the order is the way to the host
  and back, and nothing added to it.
- **Anybody may pause and resume; only the host sets the speed. Nothing pauses by itself**: the in-game menu, the
  Statistics screen and a message whose type pauses stop the clock of a game of one player and of no other, as the
  original's do. **Built 2026-11-20 as far as it can be without networking**: every pause and change of speed goes
  through `CPace` (`CSimulation.Pace`), which holds these rules, and nothing in the client writes the clock itself.
  How a client's pause reaches the host's clock is the networking's.
- **A client loads the AI's Lua to show a chance, and never lets it act.** The diplomacy, trade and licence dialogs
  ask the AI's scripts how an offer would be answered; a client asks its own copy - simpler than asking the host, and
  at once. A mod whose scripts keep memory between calls may show another figure than the host's AI will use.
- **A player who drops pauses the game, and his country goes to the AI.** He may come back where joining a running
  game is on.
- **The host waits for a machine that is behind.** It starts no hour while a client has not confirmed one a few hours
  back at most - the window is small, its size to be measured - so the game runs at the pace of its slowest machine,
  and what each player sees stays near what the host runs: an order given on a lagging machine would otherwise land
  later than its player thinks. The original's clock waits for its slowest player too (*inferred*,
  `reversing/findings/FINDINGS-frontend.md`, D.3). Who the game is waiting for is shown.
- **A dropped player where joining a running game is off** stays out until the game is saved and started again from a
  lobby. Anybody may resume the game once his country is the AI's.
- **The host leaving ends the game.** No other machine takes it on.
- **Anybody may save.** A save is a file on the machine that makes it, so no rule says who may; **the autosave alone
  is the host's.** A game is loaded in a lobby and sent to every machine.
- **What a client is sent for an hour is what a replay records** - the tick, the AI's commands with their draws and
  unit numbers, the generator's place - in the recording's own form (`replay.md`).
- **The lobby's look is not prescribed**: "I dont care." It needs a country for each player, the host's options -
  joining a running game among them - a ready state and chat.

## 3. To do before any networking

- **A sweep of the client for state changed outside a command.** Whatever a player's machine writes to the game other
  than by a command parts two machines, and `replaycheck` sees that on the AI's side only. The original has such
  writes of its own - the automation flags, the slider locks (`reversing/findings/FINDINGS-frontend.md`, D.3 and D.4).
  **Swept on 2026-11-20**, `src/` and `lib/OpenHOI3.Interface` by script over every member the simulation lets be set
  and every method it defines, the hits read; the screens were not read line by line. No order from a screen writes
  the game itself. What it found:
  - the clock's pause and speed, written from twenty places - mended, above;
  - **the supply network is made again when something asks for it** after land has changed hands
    (`CSupplyNetworks.Refresh`), and that making remaps the theatres' and the AI's areas - so a tooltip, the convoy
    window or a trade dialog decides *when*, and two machines may differ by it;
  - **who stands where is filled in the order it is asked** (`CGameState.Positions`), and the simulation reads that
    order - the first enemy fleet in a sea province: two units made one after the other end in the order the
    countries are walked in, or in the order they were made where something asked between them (shown by a test);
  - **both mended 2026-11-20: a screen's look keeps nothing.** A client sets `CGameState.ScreensLook` for the game it
    shows, and from then on what asks while no command runs - or while a running command has stopped to tell a screen
    of something - is answered from a copy made beside the game and thrown away as the game goes on
    (`CGameState.LooksOnly`); only a running command makes the table or the network the game keeps, and remaps the
    areas. A tool or a test never sets it, and for them nothing has changed. So the game makes both at the moments
    its own commands ask, the same on every machine. *Ours*: until then a screen's asking made them for the game too.
    Not tried in the running client;
  - **a second sweep, 2026-11-20, for anything else the game keeps that is made on first asking**: 37 such things
    under `lib/OpenHOI3.Sim` and `lib/OpenHOI3.Lua` looked at, and every routine of the simulation a screen calls
    scanned by script for writes; 26 are made from the game as it stands alone, their stamp covering all they read.
    Two more a screen could reach are **mended**: an area's fronts (`CAreaFronts.Of`) kept from a screen's asking
    against the network it had been shown, and `check_variable` making its variable, at nought, for whoever asks -
    a decision's tooltip among them; each now keeps nothing for a look. **Left**, with the work they belong to:
    - **the AI's steps are first askers too.** The host's AI makes the game's table of who stands where and its supply
      network when it asks, a client takes the AI's steps without running them, and so makes both at its next asking
      of its own - another moment. Latent: no order of events that shows it was found, and the state's hash holds
      neither. **Mended 2026-11-20**: a recording keeps, for each of the AI's steps, whether its asking made either
      (`CReplayStep.MadePositions`, `MadeSupplyNetwork`), and a machine that plays the step back makes them there -
      so nothing of when the AI asks, or of what it is shown, has changed (`replay.md`, section 2);
    - **a dialog's chance asks the game's own Lua state**, whose own random and whatever its scripts keep move by it,
      and a script that posts from a score function would post into the game. With the Lua a client keeps for the
      chance alone (section 2), a state of its own for dialogs that can post nothing - **built**, section 12;
    - **what listens to the state's events builds its message inside the running command**, where a look is not yet
      a look; nothing it reads today is made on asking;
    - `CAirBaseHolders` keeps an hour's air bases by a stamp that misses one built in that hour - the AI's alone.
  - the lobby writes its options, the played countries and the victory conditions into a made game, one of them by a
    draw of its own; a lobby between several machines has to send these in any case;
  - the console and the developer's switches make the game's entry pass run again or drain the queue themselves; a
    game between several players takes neither, as the original's shuts its console.
- **The AI out of the tick**, into a step the host alone runs and that only posts commands (`worklist.md`, *Loose
  ends*), proved by two games in one process, one fed by the other as a replay is, before a socket stands between them.
  **Much of it is there already, as the replay** (found 2026-11-20): every step of the AI's in the hour and the day is
  marked (`CTick.AiStep`, `CReplay.RunCalledStep`), a recording keeps what each posted, drew and numbered, and a game
  playing one back runs none of them - and `CReplay.Following` feeds one game from another as it runs, which is what
  `OpenHOI3.SaveCheck replaycheck` does for a year of play. So a host is a game that records and a client one that
  follows it, and the recording's entries are what goes over the wire. **What a client still runs of the AI**, and
  so has to hold the AI's Lua and its own state for: what entering a game runs - the laws, the strategies, the first
  pass over its units - the theatres each hour (`CTheatres.RunHourly`), which are worked out from the game alone, and
  the foreign ministers told of a new war (`replay.md`, section 2).
- **A game that plays the AI back does not yet stay with the one it follows** (2026-11-20). What the AI's steps
  write themselves and not by a command stays, on a client, as the first hour left it - and some of it is read by
  what every machine runs. Found by reading, every step's writes followed and their readers looked for, the agents'
  16,900 lines by pattern and not line by line; and **shown the same day**: `CStateHash` was given a seventh part,
  `theatres` - the theatres' numbers, provinces and headquarters, the next theatre's and the last unit's number,
  every active plan's stance and how many offers wait - and with it `replaycheck` to 1 March 1936 agrees on none of
  59 days, where the six older parts agree on all of them - by the lists of offers waiting alone, the part's
  fields then said. **Mended the same day, each as said below; `replaycheck` to 1 March 1936 then 59 of 59**, the
  host's own run unchanged by the agents' and theatres' mending - the same 17,427 steps and 26,859 commands before
  and after. Two months of peace show the offers and nothing of the rest: the others are mended by the reading.
  **The tool for it**: `CGameState.DoInStep` - what one of the AI's steps has to do to the game itself and at once
  it does as a command, run there and kept with the step (`CReplayStep.Done`, written `x`), and a machine playing
  the step back runs it at the step's place (`replay.md`, section 2).
  - **the ten-day mark that has the theatres made again** (`CAiStrategy.TheatresDirty`) is set by the AI's daily
    step, and the theatres' hour - which every machine runs, `replay.md` wrongly saying it is worked out from the
    game alone - reads it: a host makes its theatres again every ten days, a client only on a change of hands;
  - **the wars a country prepares** (`CCountry.WarTargets`) are written by the Lua in the AI's steps and decide what
    is a front worth a theatre of its own; a theatre more or fewer is a headquarters made or not, and **every unit
    number after it differs**, so the AI's commands name other units.
    *Both mended*: a recording watches the two across every step of the AI's (`CAiMarks`) and keeps, for each
    country either changed for, a `CSetAiMarksCommand` with the step - watched there and not at each write, the
    writes being many and some of them the Lua's;
  - **whether a headquarters is the AI's** is asked of the agent bound to it (`CUnit.Agent`), which only the host
    binds: the theatres' hour takes a plan off through it on the host alone, a player's `CAutomateUnitCommand` goes
    one way on the host and the other on a client, and a plan's stance picks a battle's tactic.
    *Mended*: what the game holds of "the AI runs this headquarters" is its plan being on, which commands set on
    every machine, and whatever every machine runs asks that - `CAgents.LetGo` and `HighestAutomated` in the
    theatres' hour, `CAutomateUnitCommand`, a unit attached, lent, taken home or annexed. **And the command no
    longer makes the agent**: it switches the plan on, and the AI's pass takes the headquarters up
    (`CAgents.Adopt`) - so the area, the front line and the powers of a headquarters just handed over come with the
    AI's next pass, within the hour, and **not while the clock stands**, where they came at once. *Ours*, for the
    sake of several machines; the AI's own code still asks its agents;
  - **a player's theatre command sorts by an AI flag** (`CUnit.AiBound`, in `CTheatreForcesCommand`). *Mended*: the
    command's sort goes by each unit's way alone (`CAgentUnits.WayOrPlace`);
  - **offers to a played country are handled inside the AI's step** that answers offers, with the event a screen
    listens for - so a client's player is never shown an offer made to him, and no machine but the host empties the
    lists of offers waiting, which a client's save then holds. *Mended*: a played country's offers are put to its
    player by a step of the game's own (`CPactPasses.AskPlayers`), before the AI's answering, and what the
    answering and the automated trading leave of a list they leave through `DoInStep` (`CKeepOffersCommand`);
  - **the Lua states' own random and memory** are a game's old on the host and new on a machine that joins, and
    entering a loaded game runs the ministers' scripts on every machine. **Not mended**: it belongs with the
    sending of a game to a machine, which is not built;
  - on a client only shown wrongly, nothing in the game moved: its own theatres' fronts, a theatre's needs and
    power bars, the operation named, an automated headquarters' line and arrow until the host's agent next posts.
  **What mends most of it** is one rule: nothing a machine runs for itself may ask the AI's own state - a mark, a
  list or a flag the game needs is set by a command or by a step every machine runs.

## 4. Open

- Chat's place in play, and what a player is told of the others - who dropped, who paused.

## 4.1 Decided for the network, 2026-11-20

- **TCP.** Every line has to come, and in order, which it gives; the traffic is small, and a stall for a lost packet
  is what the window of three hours is for.
- **The host's port has to be reachable, and nothing is done about it**: on a local or a virtual network it is; over
  the open one the host forwards the port. One port, which the host may change. **IPv4 and IPv6 both** (built
  2026-11-20): the host listens on one socket for both families, and an address typed to join is a name or an
  IPv4 address, which may end in a colon and a port, or an IPv6 address - bare, or in square brackets with a
  colon and a port after them (`CNetAddress`).
- **A game is sent whole, compressed, with the game paused until every machine has it** - the save every machine
  then loads (section 2). **A client that already holds the same save, by its hash, is sent nothing.** And **every
  machine makes its Lua states afresh at that load, the host too**, so that the scripts' own random and memory begin
  alike everywhere (section 3).
- **A client's Lua for the chance of an offer is a state of its own that can post nothing.**
- **A player who comes back chooses among every country nobody plays**, the one he had among them: "There should be
  no limit on which country the rejoining player can choose."
- **The host checks that an order is for the country of the player who sent it**, and drops one that is not. No more
  than that: "in the grand scheme there is no need for a real trust model."
- **The lobby is the one there is** - the map, the options, the bookmarks and the saves - with the little the
  original has before it: a player's name, and an address to join in place of its list of servers, there being none.
  Chat and the rest come later.
- **The lobby is shared** (decided 2026-11-20): the host's lobby listens from the moment it is opened, a machine
  that joins sees the same lobby, each player picks a country on its map, and the host's Play starts the game for
  every machine. Joining a game that is running stays as it is. So the lobby's choices - the bookmark or the save,
  the options, who has which country - have to travel between the machines. **Not built.**
- **The address is typed where the original has it typed**: "The original has a second panel that opens once you
  choose internet game." `temp_frontend.gui` has `multiplayer_menu` - `playername`, `host`, `join`, `join_direct`,
  `scan`, `metaserver`, `Back` and the list `games` - and `naming_window`, a `Title`, an edit box `edit`, and the
  buttons `ok_host_button` and `back_button`, which its own comment says is "used for both player and game name".
  **What the original does with them is not read**: which the second panel is, its title and what its button says
  when it asks for an address. To be read in the fact base or told, before the panel is built.

## 5. A host and its clients - built 2026-11-20, with no network under it

`lib/OpenHOI3.Sim/Multiplayer`: `CHostSession` and `CClientSession`, each handed the lines that came and a way to
send one. **How a line travels is no part of them** - no socket is built - so everything here runs and is tested in
one process. What they do:

- **The host's game records, and every entry of the recording is sent to every client as it is made**; a client's
  game follows them (`replay.md`, section 8).
- **A client's order goes to the host and nowhere else**, is posted there, and runs on every machine when it comes
  back in the recording - while the clock stands too, at the host's next frame.
- **A client starts no hour before the host has said it is whole.** An hour's entries are sent one by one as the
  host runs it, and one begun on some of them would stop for want of the rest.
- **The host waits**: it starts no hour that would put a client more than the window behind, so no client is ever
  further behind than that, and it tells every client who is waited for. A client behind catches up by running more
  hours a frame.
- **Anybody's pause is taken by the host**, whose clock paces the game; a client's clock shows the host's and paces
  nothing. The speed is refused on a client (`CPace`).
- **Each midnight the host sends the state's hash**, and a client sets its own beside it. One that differs says so,
  runs nothing more, and is no longer waited for or sent anything - `CHostSession.Parted` is where sending it the
  game again will hang. **That sending is not built.**
- **A client gone pauses the game.** Its country going to the AI is not built: nothing but the console changes who
  plays a country.

**The lines** (`CWire`), each one line of text whose first letter is its kind:

| from the host | |
| --- | --- |
| `e` | an entry of the recording, as a recording has it on disk |
| `h` | an hour that turned a day, and the state's hash as it then stood |
| `d` | an hour, whole: everything of it has been sent |
| `s` | the host's clock: paused or not, and its speed |
| `w` | who the game is waiting for, by name |

| from a client | |
| --- | --- |
| `o` | its player's order, as a recording writes a command |
| `c` | the hour it has run the game to |
| `p` | its player pauses or resumes |
| `x` | the hour at which its hash was not the host's |

**Checked.** `MultiplayerSessionTests` on a game of two provinces, each rule above with the lines held in lists
between the two. And `OpenHOI3.SaveCheck twomachines <install> [date] [window]`: two new games with nobody playing
and the install's AI, the host an hour a frame, the client's frame every third of the host's, an order from the
client's side once a day. On 2026-11-20, to 1 March 1936: **59 days, every midnight's hash the same and the two
alike at the end**; 7.4 MiB sent to the client, about 128 KiB a game day as plain text, the longest line 163,466
characters, and 17 KiB to the host. **The lines stay plain text** (decided 2026-11-20), **and compressed they are a
tenth**: the same run's lines to the client as one Deflate stream at its fastest, flushed at every line so that each
can be read as it comes, are 809 KiB - 10.7% of the text, about 14 KiB a game day - for 144 ms of the host's time
over the 59 days; Brotli at its fastest, flushed the same way, 37.8%. So what carries the lines compresses them with
Deflate, as a stream. **The window of 3 hours stands** (decided the same day).

**The connection** (built the same day): `CLineLink` and `CLineListener` - TCP, each way one Deflate stream at its
fastest flushed at every line, a line sent from any thread and taken on the thread that asks, the other side's going
seen; the port 19390 where the host names no other (*ours*). `LineLinkTests` runs it over the machine's own
loopback. And `twomachines ... tcp` puts the same real game through it: to 1 March 1936, **59 days, every
midnight's hash the same**, the host waiting on 7 of 1,423 frames for the client to catch up - which the run that
hands the lines across in lists never has it do.

**Before and around play** (built the same day):

- **A machine is let in by its play checksum.** Its first line is its checksum and its player's name (`j`); the
  host answers `y`, or `n` with its own checksum where the two differ (`CHostSession.Admit`).
- **A game is sent whole, or kept** (`CGameSender`, `CGameReceiver`): the bytes of a save offered by their SHA-256
  (`g`); a machine that holds those bytes says so (`k`) and is sent nothing; any other asks (`t`) and gets them in
  pieces of 48,000 bytes as base64 (`b`), then the end (`G`), and checks them against the hash; it says when it has
  loaded them (`r`). Making the save and loading it are the caller's, the simulation knowing neither.
- **The host drops an order that is not its sender's country's** (`COrderCheck`), by the field a command names its
  country in - `_owner` or `_country`, which 68 of the 107 commands have; one with neither is let through.

**A machine joining a running game, checked**: `OpenHOI3.SaveCheck joinmachines <install> <date joined> <until>` -
a host plays alone to the first date, saves, the save is sent to a second machine line by line, **both load it, each
with scripts made then**, and they play on as host and client. On 2026-11-20, joined on 1 February 1936: the save
10.2 MiB, 219 lines, 1.3 MiB as the connection compresses it; **28 days on, to 1 March, every midnight's hash the
same and the two alike at the end**. So a game loaded by every machine, the host with them, goes on the same on
each - what a join and a parted machine both come down to (section 2). Tried with nobody playing, and on one start.

**Who plays a country, changed in play** (built the same day): `CSetPlayedCommand`, *ours* - the original changes
it in its lobby alone. A country taken up is played, the AI runs its units no longer, and every headquarters the AI
ran is let go, its plan switched off; one left is the AI's again, its ministers starting at their next hour. The
host posts it: `CHostSession.Drop` for a client gone, with the pause; `Assign` for a player given a country nobody
plays - any that owns land (`Unplayed`), so one who comes back chooses among them all, the one he had or another -
the country he held going to the AI, and the client told which is his (`a`).

**Not built** in the simulation's side: chat. What the client does with it is section 6.

## 6. The client - begun 2026-11-20

`src/Multiplayer`: `CNetGame`, with `CNetHost` and `CNetClient` - this machine's part in a game between machines.
**It outlives the sessions that play the game**: every game sent is loaded as a new session around a new
simulation, so the boot scene (`Main`) keeps it, pumps its connections every frame whatever is on screen, hands it
to each session, and loads the game it says to.

- **Two developer switches stand in for the lobby, which is not built.** `--host` (or `--host=port`) beside a switch
  that says what to play - `--country=GER`, `--load=` - hosts that game. `--join=address[:port]` with
  `--country=ENG` and `--name=` joins one, asking for that country. `--netlog` writes each new day to the log, and on
  a client how many midnights its hash has been the host's.
- **A machine is let in and says what it would play** (`j`, then `W` and a tag). The host's next frame that has the
  game to itself runs every order given, **saves, and offers the save to every machine - those already playing
  too - and loads it itself** (`CNetHost.SendGame`, `Main.LoadNetGame`), each with `CAiScripts` made then. The same
  is done when a client says its hash differed. A country asked for that is played or owns no land is refused
  (`n country`) and the machine let go.
- **Before the offer the host says which countries are played in the game, its own first** (`P`), and each client
  which is its (`a`). A save names one player; every machine loads it as that one's and takes the others up
  (`CSetPlayedCommand.Take`) before the game is entered, so that every machine enters the same game.
- **No hour runs until every machine has said it has loaded** (`r`); what a client said before that is of the game
  before and is dropped, and what the host sends a client between the game's last piece and its session being made
  is kept for that session.
- **A session takes its player from what the host gave** (`CNetGame.Country`), not from the state's first played
  country. **A client runs no clock**: each frame that has the game to itself it takes the lines that came and
  runs what came of the orders given between hours; after the frame is drawn the hours that are whole are started
  **beside the frames that follow, as a game of one's are** (`CSimulation.BeginFollowing`), one after another for
  `BackToBackSeconds`, and no line is taken while they run. A host's frame takes its clients' lines and tells them
  of the clock while no hour runs beside it.
- **The console changes nothing between machines, and of the developer switches only those that look are taken**
  (`CSession.NetSwitches`), with the host's `--run`: a command of the console that would change the game answers
  "Not available in this game." - as it now does in a replay, where it had said it was done. *Ours.*
- **A client gone** is dropped at the host's next such frame: the pause, and its country to the AI. **The host
  gone**, or a refusal, ends the game on a client: back to the main menu, the reason in the log. **A client whose
  host is gone first saves the game as it stands there** (asked for on 2026-11-20, so that a host's crash loses
  nobody the game; `CSession.SaveAsTheHostIsLost`): to its own saves as `Multiplayer_` and the name the save dialog
  offers, `Multiplayer_United_Kingdom1936_03_17_17.hoi3`, written as that machine's player's. Tried by processes:
  the host stopped, the file written, its `player` the client's country. **And it says so on screen**: the reason
  and the save's name over the way back to the main menu and twenty seconds on (`Main.ShowNotice`; *ours*) - not
  seen on screen by a check.
- A line under the top bar says who is waited for, or that a game is being sent or received, once that has stood a
  second: at the top speed the host waits a frame or two for a client all the time, the window at work. *Ours.*
- A game loaded in place of another keeps where the map and the clock stood (`CSession.LeaveForLoad`).

| added to the lines | |
| --- | --- |
| `W`, from a client | the country its player would play, by its tag |
| `P`, from the host | the countries played in the game about to be offered, the host's first |

**Checked**, 2026-11-20, a host and a client as two processes of the client on one machine, Germany hosted at the
top speed and the United Kingdom joining on 31 January 1936: the save of 10 MiB sent and loaded by both, and **131
midnights on, to 12 June, the client's hash the host's every one**; the host waited for the client 817 times, the
client's hours on its frame's thread being the slower. **Looked at in play the same day**, from an export, two
windows on one machine: unit orders, trade deals, production automation, the catching up at speed 5 and the pause
from either side, "looks good". Not tried: a machine dropped or come back, the host gone, a game parted. With the
client's hours beside its frame, the same two processes: **125 midnights the same**, to 4 June, the host waiting
678 times - the game goes no faster for it, the window being what holds the host; it is the client's frames that
no longer carry the hours. That and the console's refusal have not been looked at in play.

**A machine dropped, come back, and the host gone - tried the same day** by three processes with `--netlog`, which
also says who plays and whether the clock stands whenever either changes: a client playing the United Kingdom
stopped on 19 March 1936 after 46 midnights the same - **the host paused that hour and the United Kingdom went to
the AI**; a second machine asked for the United Kingdom - **the game sent again, loaded by both, played by GER and
ENG, still paused**; the host stopped - **the client said "The host has left the game."** and left the game.

**A game parted, tried the same day**: `--netpart=1936.3.10` on a client gives its player's country a little
manpower it never had, once, from that day - nothing else makes a game part. The client found its hash another
than the host's at the next midnight and said so; **the host sent the game again, both loaded it and played on,
the clock running as before, 88 midnights the same after it**, to 7 June.

**What a load between machines loses**, the save being in the original's keys: the production screen's slider
locks and the automation of every player but the host, which a save keeps for one player; and a headquarters a
player other than the host had handed to the AI, whose plan `Take` switches off. To be said in `enhanced.md` with
the snapshot.

**Not built**: the lobby, a name and an address entered on screen; a country chosen from those nobody plays, by a
player joining or come back; the reason a game ended shown on screen; a client keeping the last game it was sent,
so as to be sent nothing where it holds it; chat in play. (A client's own Lua for an offer's chance is built,
section 12.) A game's sending blocks the
host's frame until the connection has taken it.

## 7. The shared lobby - built 2026-11-20

Decided the same day (section 4.1). The menu before it is `mechanics/frontend.md`, section 8.

- **The host's lobby listens from the moment it is opened**: Host's name given, the lobby comes up and the play
  checksum is worked out beside it, some seconds; then the port opens (`Main.HostLobby`, a `CNetHost` with no country
  yet).
- **A machine let in while the host is in its lobby is told how the lobby stands** (`l`): the place of the
  bookmark chosen among the bookmarks, and the players - the host first - each a name and the country chosen or
  none. It is told again whenever any of it changes. **That line is what takes a joiner to the lobby** and not to a
  wait in the connect log; a host in a running game sends none, and the joiner is given a country as before.
- **A client's lobby is the lobby there is** (`CLobby.ClientMode`): it shows the host's bookmark, its rows and its
  saves take no click, its Play is greyed, and the Options panel is hidden - *ours*, the original greying its
  arrows. Its player clicks a country on the map; the choice goes to the host (`W`), **who gives it where no other
  player has that country** and says so in the lobby's line. The players' box (`multiplayer_list`) has a row a
  player with the shield of what each chose, and their number.
- **The host's Play is greyed while a player has chosen none.** Pressed, it makes the game as a game of one's is
  made, the host's country played; the machines in the lobby wait to join with what they chose, and **at the game's
  first frame it is saved, sent and loaded by every machine** - section 6's join, nothing new. So a game from the
  lobby is loaded twice on the host.
- A client's Back leaves the lobby and the connection; the host's closes the lobby for every machine, each of which
  is back at its connect log with CONNECTION_FAILED.

| added to the lines | |
| --- | --- |
| `l`, from the host | its lobby as it stands: the bookmark's place, a space, then `name=TAG` for each player with `&` between, the names escaped as a URL's are |

**Checked**, 2026-11-20, two processes driven by `--click` through the menu: the host hosting from the menu and
choosing Germany, the client joining by address, coming into the lobby and choosing the United Kingdom, the host's
Play - **the game loaded on both, played by GER and ENG**, each machine its own. The game then stood paused, as a new
game does, so no midnight was compared after a start from the lobby. Nobody has looked at it.

**Not built, or a stand-in**: a save the host loads in its lobby is not shown on a client, which keeps the
bookmark's map and chooses by it; the options and the victory conditions the host sets reach the others only in the
game sent; a country that owns no land at the start chosen is refused only as the game starts.

**Brought in line with the original's lobby the same day**, once it was read
(`reversing/findings/FINDINGS-multiplayerlobby.md`; `mechanics/frontend.md`, section 9, which says what is and is
not built of it): a row's "name (Country)", the host's rows with kick and ban (`CNetHost.Kick`: the machine told
`n kicked`, its connection closed), a client's tabs greyed, **the chat** - a typed line to the host (`C`), who
tells every machine in the lobby, and the lobby's own lines by their keys (`CLobbyChatLine`) - and a client whose
host is gone or that was thrown out staying in its dead lobby with one line in its chat until its Back. Tried by
two processes: the rows, the chat's box and a typed line come back from the host, on a screenshot of the client.
Kick, ban and the host gone from a lobby were not tried.

| added to the lines | |
| --- | --- |
| `C`, from a client | what its player typed in the lobby's chat |
| `C`, from the host | `t`, a name and what that player typed; or `k`, a localisation key and the name of the player it speaks of |

## 8. Several players of one country - built 2026-11-20

Decided that day: "I want co-op. In the original the first player to choose the country is the 'primary' one, and
gets event popups." The original's rule is `reversing/findings/FINDINGS-multiplayerlobby.md`, section 2: no
country is refused, and a human is marked primary where no other human of that country is.

- **Any country may be chosen, one another player has among them**, in the lobby and by a machine joining a game
  that runs; a country nobody plays still has to own land.
- **The first to have chosen a country is its primary player** (`CNetHost`: a count that grows with each choice,
  the host's own among them; a host started by switch chose first). The host tells each machine with its country
  whether it is primary (`a TAG 1` or `a TAG 0`), and tells it again when it becomes so, the one before it gone.
- **In the lobby a player who shares a country and is not its primary has an asterisk after the row's name**,
  "name (Country) *" - *ours*, asked for on 2026-11-20; the lobby's line carries it as an asterisk after the tag.
- **Only the primary is shown the country's events** (`CEventWindows.Shows`, by `CNetGame.Primary`). Everything
  else is every player's alike: each may give the country's orders - the host lets an order through that is its
  sender's country's, however many machines play it - and each sees the same screens. *Ours, beyond the events*:
  what else the original keeps for the primary was not read - messages, the offers put to a player, the
  automation, which a save keeps for one player.
- **A machine gone takes its country to the AI only where no other player has it**, the host's own among them
  (`CHostSession.Drop`, `HostCountry`); the game pauses either way.

**Checked** by three processes: a client asked for Germany, which the host played - sent the game, **played by
GER alone, the client not primary, 34 midnights the same**; the client stopped - the host paused and **Germany
stayed played**; a second machine asked for Germany again and was let in, not primary. That a second player's
orders run, that the events stay off its screen, and a primary handed on were not tried: the first two want
looking at, and the third wants three machines.

## 9. A game that parts, named

Built 2026-11-20, after a game played over the internet by two players of France was sent again three times in its
first months with nothing to say why. **What differed is now written down by both machines.**

- A client that finds its midnight's hash another than the host's sends, with the hour, its hash of each of the
  state's seven parts (`x hour p1 ... p7`), and writes every figure's own hash as its game then stood to
  `openhoi3_parted_<hour>_client_<process>.txt` in the logs' folder (`CClientSession.PartedFields`).
- The host keeps each midnight's parts and figures for three days while a client follows (`CHostSession.MidnightAt`);
  told of a parted game it says in its log which parts differed and writes its own figures of that midnight to
  `openhoi3_parted_<hour>_host.txt`.
- `python tools/partdiff.py <logs folder> [hour]` sets the two beside each other and prints the figures that
  differ.

**Checked** with `--netpart=`: the host said "out of step ... in countries", both files were written, 44,356 figures
each, and the tool named the one that differed, `ENG manpower`.

**What made that game part - found the same evening, with the files.** A second game, nothing done in it but an event answered, parted at the same hour, 21 January 1936, in one figure of 44,391: `FRA neutrality`. Germany had reoccupied the Rhineland; France is told by event 1002, whose one option takes 3 off its neutrality. **A player's answer to an event never ran on a client**: the answer holds the event, a recording wrote the event out field by field and read it back as an event of its own, and the answer runs only for one of the game's own events (`CSelectEventOptionCommand.MayExecute`) - so it was dropped wherever it had been read. The host took the 3 off and the client did not. An event is now written as its id and read back as the game's own (`CCommandCodec`), with a test. The AI's answers never showed it: every machine makes those itself, and no check had a player answering an event. No other command holds a thing of the script's by reference. **Not tried between machines since** - it wants an event answered in a game of two.

**A game being loaded to bring machines into step says so**: "Syncing games..." over the loading screen, where a game was already being played (asked for on 2026-11-20; *ours*).

## 10. Choosing a country to join a game that runs - built 2026-11-20

Until then a machine that joined a running game from the menu was given the unplayed country owning the most
land. *Ours*: the original's own screen for it, `CIngameLobby`, is not read.

- **A machine that joins by the menu names no country.** A host whose game runs answers as a host in its lobby
  does, with the lobby's line (`l`), the bookmark's place **-2** for "a game is running" (`CNetHost.RunningGame`)
  and the players of that game with their countries.
- **The joiner's lobby opens on that** (`CLobby.HotJoinMode`): its own map of the start, the players' rows, no
  chat. A click on the map chooses and **sends nothing**; **Play, enabled once a country is chosen, asks for it**
  (`W`). A click alone joins nothing, where in a host's lobby the click is the choice.
- **The host gives it** where the country is played - shared, section 8 - or owns land, and sends the game as for
  any join. **Where it does not exist in the game** the joiner's chat says NOT_EXIST with the country's name, and
  it chooses again; it is not let go.
- A machine started with `--join=` and `--country=` names its country at once, chooses on no screen, and is still
  refused and let go where it cannot have it (`CNetPeer.Direct`).

**Checked** by two processes: a host by switch, running; a client through the menu and the address, into the
lobby - the host's row "Host (Germany)", its own row with no country, on a screenshot - the United Kingdom clicked,
Play: **the game sent, played by GER and ENG, 82 midnights the same**. NOT_EXIST was not tried.

~~**What it does not do**: the map is the start's, not the running game's~~ - it is the running game's since the
same evening, section 11.

- **No hour runs while a machine is in that lobby** (asked for on 2026-11-20: "the running game should be paused
  as long as someone is in the hot-join lobby"): the host's hours are held, not its clock stopped, so the game goes
  on by itself once the machine has joined or gone, and every machine's waiting line says "Waiting for *name* to
  choose a country" (`CNetHost.Choosing`, `CHostSession.AlsoWaitingFor`). Tried by processes: the host stood at one
  hour from the joiner's coming to its joining. A machine that sits there holds the game for everybody until it
  joins or leaves.

## 11. A game sent to be looked at - built 2026-11-20

"It should send over the running game, you could probably also do the lobby-savegame selection sending in the same
stroke." One thing does both: **the host sends a game that is to be shown in a lobby and not played.**

- Before the offer the host says so (`V`); the game then travels as any game does (`g`, `t`, `b`, `G`), and the
  client, instead of leaving what it is in and loading the game to play, **loads it as its lobby's world**, as a
  save's row loads one (`CNetClient.LobbyGame`, `CLobby.ShowHostGame`). It answers nothing when it has.
- **A machine in the lobby of a game that runs is sent that game as it stands**, once: the host saves it at its
  next frame that has the game to itself (`CNetHost.ViewWanted`, `SetView`). So the map a country is chosen on is
  the game's own - a country that has come to be can be chosen, one that is gone is not there - and the header
  gives the game's date.
- **A save the host chooses in its own lobby is sent to every machine there as it is chosen**, as the original
  sends it, and to a machine that comes in later; back on a bookmark, the lobby's line names it and the clients
  make the start's world again. The host loads its save at once and does not wait for the others, where the
  original loads only when every client has it; `save_progress` shows nothing; a client keeps no copy and is sent
  the bytes each time.

**Checked** by two processes: a joiner's lobby of a running game with the header "Saved Game, 15 of February, 1936",
the host's date, on a screenshot; then the whole of it, the United Kingdom chosen and joined, 71 and 76 midnights
the same. **A save chosen in a host's lobby reaching a client was not tried** - no check clicks a save's row.

## 12. The Lua a chance is asked of - built 2026-11-20

Section 3's last open point. The dialogs of an offer say how likely the recipient's AI is to accept, which is that
AI's own score function in the install's Lua. Until now the game's own Lua state was asked - the AI's, whose scripts
keep things between calls and have a random of their own - and on a client there was none to ask.

- **Every session keeps a second Lua state for looking** (`CGameState.DisplayScripts`, made by `CSession` the first
  time a dialog asks and thrown away with the session; `CDiplomacyActions.Chance` asks it). It loads the same
  `autoexec.lua`, takes 13 ms to make, and is never handed to the tick: nothing of the AI runs on it. A host and a
  game for one have it too, so that a dialog no longer moves what the AI's scripts keep. Where it cannot be made the
  game's own is asked as before, and the failure is said once.
- **What a script posts while a chance is worked out is dropped and said**, and the game's two generators are put
  back as they were - the second was already so for a peace's roll (`mechanics/war.md`, section 11). A look changes
  nothing; on a client anything else would part the games.
- **The state is not in a save and not sent**: a machine makes its own, as it makes the AI's (section 4.1).

**Checked** with `OpenHOI3.SaveCheck aiscore <install> second`, which asks both states the same of one new game: ten
of the eleven score functions that take only tags answered alike for all 6,006 pairs and posted nothing;
`DiploScore_PeaceAction` differed in 2,742, since it rolls dice at every call - the dialog's figure for a peace is one
roll's on any machine, the original's too. A test has a script that posts and rolls and finds neither afterwards.
**Not looked at on a client's screen.**

**A client's chance may be a little off the host's answer**, for what the scores read that only the AI's steps
keep current, which a client does not run (`replay.md`, section 3):

- **what the scripts themselves remember** is each state's own;
- **the military access list of a strategy** is written by the AI's scripts and a client's stays empty.

**The recipient's AI strategy** (`GetStrategy()` in the alliance, non-aggression, trade, licence, peace and
break-alliance scores) is made again every ten days in an AI step, which a client plays back and does not run. **A
game that plays the step back makes the lists itself, right after it** (`CAiStrategies.RunDailyFollowing`, called by
the tick where `CReplay.IsReplaying`; decided 2026-11-20, "Ok build it"): the same countries on the same days, a
making reading nothing but the game, rolling nothing and posting nothing. It leaves out the two things of a making
the game's hour reads - the prepared wars' recheck and the theatres' mark - which come with the played-back step
(`CAiMarks`). The makings at a load and at a change of war, peace or faction were every machine's already.
**Checked** with `replaycheck`, which now sets every country's lists side by side each midnight: the same on 365 of
365 days to 1 January 1937, the hashes too; `twomachines` 59 days the same.

`CAIIntel` is worked out from the game as it is asked and is the same everywhere.
