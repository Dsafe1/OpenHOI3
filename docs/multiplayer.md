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
  (`enhanced.md`, *The file checksum*) - and is what machines compare before they play.

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
- **Anybody may pause and resume; only the host sets the speed.**
- **A player who drops pauses the game, and his country goes to the AI.** He may come back where joining a running
  game is on.
- **The host leaving ends the game.** No other machine takes it on.

## 3. Open

- What a client's hour needs from the host beyond the commands: the tick itself, the AI's draws, the game's generator.
- What happens while one machine is behind the host.
- Saves: who may save, what a save made on a client is, and loading one in a lobby.
- The lobby: choosing countries, the host's options, chat.
- What in the client today is a local write and not a command, and would part two machines.
