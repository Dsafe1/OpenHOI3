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
      neither. To be settled with the AI out of the tick: the AI's steps made to look only, or both made at set
      points of the hour that are not the AI's;
    - **a dialog's chance asks the game's own Lua state**, whose own random and whatever its scripts keep move by it,
      and a script that posts from a score function would post into the game. With the Lua a client keeps for the
      chance alone (section 2), a state of its own for dialogs that can post nothing;
    - **what listens to the state's events builds its message inside the running command**, where a look is not yet
      a look; nothing it reads today is made on asking;
    - `CAirBaseHolders` keeps an hour's air bases by a stamp that misses one built in that hour - the AI's alone.
  - the lobby writes its options, the played countries and the victory conditions into a made game, one of them by a
    draw of its own; a lobby between several machines has to send these in any case;
  - the console and the developer's switches make the game's entry pass run again or drain the queue themselves; a
    game between several players takes neither, as the original's shuts its console.
- **The AI out of the tick**, into a step the host alone runs and that only posts commands (`worklist.md`, *Loose
  ends*), proved by two games in one process, one fed by the other as a replay is, before a socket stands between them.

## 4. Open

- The size of the window the host waits within.
- Chat's place in play, and what a player is told of the others - who is behind, who dropped, who paused.
