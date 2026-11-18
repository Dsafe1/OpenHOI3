# The log

What the game writes about itself as it runs, and where. Built 2026-11-18, as requested: "a logger to file
... in the place where the original puts them (so the documents folder)".

## 1. Where

**The original** writes its logs into `logs` in the player's folder - `Documents\Paradox Interactive\Hearts of Iron III`,
or with a mod the mod's user folder below it (`reversing/findings/FINDINGS-modlayer.md`, its rules and section 8: writes go to
the write directory and make their folders, and with a mod that is the mod's user folder). It keeps several files there - `game.log`, `error.log`,
`setup.log`, `system.log`, `ai.log` and others - each written afresh at every start.

**Here** the same folder (`CLogFiles.Folder`: the directory settings' `UserFolder` and `logs`), so a run with BlackICE logs
into `...\Hearts of Iron III\BlackICE GitHub\logs`. *Ours*: **the files are our own** - `openhoi3.log`, and a crash's
`crash-<time>.log` beside it - and none of the original's is ever written, so the two programs' logs stand side by side
and a check that reads the original's (`OpenHOI3.SaveCheck checksum` reads its `setup.log`) is not disturbed. Decided on
2026-11-18.

## 2. What

Everything the game prints: each line Godot logs - our own prints, the content's warnings (`CDiagnostics`), errors with
where they were raised, the engine's own messages - each stamped with the time (`src/Debug/CFileLog`, a Godot `Logger`).
The folder is known only once the install and the mod are, so what is printed before that is kept and written first.
Each line is flushed as it is written, so a crash loses nothing of it.

## 3. The runs kept

*Ours*: `openhoi3.log` is this run's; the four before it are kept as `openhoi3_1.log` to `openhoi3_4.log`, newest first,
each start moving them one place back and dropping the oldest (`CLogFiles.Start`) - so a run that went wrong is not lost
by starting again. Decided on 2026-11-18; the original keeps only the run it is.

A crash's file is named by its time and never overwritten. Godot's own copy of its console, in its user folder
(`user://logs`), is written as before.
