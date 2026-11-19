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

## 4. The validation log

*Ours*, asked for on 2026-11-19 - "a log file that acts as a validator for people": **`openhoi3_validation.log`**, beside the
run's log, lists **every fault found in the install's or the mods' own files**, one to a line, so that whoever writes a
mod has one list to work through. It is written afresh at each start, and the run's log says once where it is. **A line
is written once**: a file several readers parse - an interface file read for its sprites, its fonts and its objects - says
each of its faults once, however often it is read.

Each line is where the fault is and what it is, in the form compilers write, which editors and terminals open at the
place (`CDiagnostics.Located`):

    C:\...\BlackICE GitHub\localisation\zDD-tech.csv:13: 'TECH_INVEST' is given again and dropped; its first text, at ...:12, stands
    tfh/mod/BlackICE GitHub/events/event test.txt:490: 'limit' is no condition [UnknownKey]
    'C:\...\map\provinces.bmp': 14217 pixels in 21 colours match no row of 'definition.csv' and belong to no province

**What goes there and what does not.** A fault in the content - a key nothing reads, a name nothing declares, a value of
the wrong shape, a file that cannot be read or decoded, a sprite, font, window or effect technique an interface names and
no file declares, a localisation key given twice or given no text (`mods.md`, section 2) - is a problem
(`CDiagnostics.Problem`). What is about this program or this run stays in the run's log as a warning
(`CDiagnostics.Warn`): what is not built (a save's keys not loaded yet, a sound's pan, `debug_saves`), a deliberate
departure (`alwaysCounters = no`), a file that could not be written, the player's own `settings.txt`, the shipped
typefaces, the shader translator's own limits, a `-mod=` that names no mod, and the Lua's errors and unbuilt calls.

- **Each script problem is a line of its own** at its file and line, its kind in brackets (`script.md`, section 10) -
  where the run's log had one line counting them.
- **Each localisation line dropped is a line of its own**: a key given again, with where its first text is, and a key
  given no text in the language read, which shows as the key itself unless a later line gives it - the two the
  original logs as `Duplicate text-key` and `Empty key` into a log of its own it never writes to disk
  (`reversing/findings/FINDINGS-localisation.md`, section 2).
- **The other loaders' faults keep their own wording**, each one line: most name their file, quoted, and none a line,
  the Clausewitz reader keeping no line numbers; and several gather a kind of fault into one line with a count and the
  first names. *Loose end* (`docs/worklist.md`): a line for every fault, and line numbers from the Clausewitz reader.

(measured, 2026-11-19) A start of the base game, the main menu and a new game as Germany: 29 lines, among them the
script's 13 problems, three localisation keys given twice - `Tag`, `CHAT_ALLIES` and `desert_fox` - and four `xxxx` header
rows with no text. With BlackICE GitHub: 2,683 - 713 script problems, 121 keys given again and 1,831 keys with no English
text. The run's log of either had no warning left in it; every warning the runs before printed was a fault in the files.
Before keys were matched case and all (`mods.md`, section 2) the base game's showed 20 keys given twice, 17 of them
pairs like `MANPOWER` and `manpower` that the original keeps apart.

A tool reports to standard error unless it says otherwise: a problem as `PROBLEM: `. `OpenHOI3.SaveCheck modsurvey`
gathers problems and warnings alike as its findings.
