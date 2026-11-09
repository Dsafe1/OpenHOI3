# The console

The original's cheat and developer console. No addresses and no offsets. Source: `reversing/findings/FINDINGS-console.md`, static; what follows has been tried
with `--click`, and the maintainer has not looked at it.

Hardcoded: everything here but the window, `console_wnd` and `console_entry_wnd` in `interface/core.gui` [mod].

## 1. Opening it and a line

`§` or `` ` `` opens and shuts it, and neither can be typed into it. The line goes on Enter: trimmed, kept in the history the up and down arrows go through, and split on spaces.
The first word is looked up **exactly, with its case**, by a row's name or one of its aliases. Then: no row, `Unknown command`; one of the two rows for the developers, `weather` and `crash`,
`Command available only for developers.`; otherwise the row runs with the other words. The answer is printed a line at a time, white where it worked and red where it did not.

**Arguments are counted and not typed**: a number that is none reads as nought, as C's `atoi`. An amount of goods, men or officers is whole, thousands in the game's figures.

`help` lists every row but the developers', `name(alias,alias) [arg] [arg]`, in the order of the table; `help <word>` adds a line with the row's words, and describes the developers' too. The help texts are the original's, misspellings and all (`CConsoleTable`), and nothing of the console is read from a file.

## 2. What is built

`CConsoleRunner` answers; **every change to the game is a `CCheatCommand`**, or the command that exists for it, so a game with cheats in it can be replayed, where the original's write directly.

| | |
| --- | --- |
| goods | `money`, `supplies`, `fuel`, `crudeoil`, `metal`, `energy`, `rarematerials`, `givemeall`: added to the stockpile, a government in exile's pool for one, with no floor |
| the country | `addmanpower`, `addofficers` (floor nought), `noneutrality`, `setnationalunity` (held 1 to 100), `election`, `reorg` (every brigade of the player's own in a province to its most organisation) |
| the world | `surrender`, `guarantee` (a toggle), `conquerall` (**no war needed**), `changeowner`, `changecontroller`, `event` (opens it, no trigger asked), `revolt` |
| switches | `instantresearch` (a level is done whatever its progress), `instantconstruction` (a line the day reaches is done whatever its progress), `alwayspartisants` (an underground raises them at any strength), `tooltipdebug` (under the province's name, the owner's tag and its number in the country list, the null country first, and the province's number), `cpaint` |
| tools | `printtech` (any one argument leaves noughts out; `nozero` is not compared), `copyprov`, `help`, `helplog`, with what goes to `game.log` going to the engine's log here |
| the player | `tag` (another country with land: the country left goes to the AI, and every screen is made again around the same game) |
| time and units | `date` (nothing runs for the time skipped), `teleport` (the first unit selected; any but a land unit aboard a fleet; no test of where) |
| the AI | `aipause` (none of the country's AI runs: its offers answered, its trading, ministers, deployments and units; its strategy's daily look still does) |
| the screens | `reloadinterface` and `reloadfx` (every screen made again, the interface and the effect files read again; `map`, `front`, `hierarchy`, `masked3dFlag`, or a file there), `fullscreen`, `detailedlog` |
| `debug` | `tooltips`, `fow`, `nopopup` (no message at all, the log too, and the player's events answered as the AI's), `minzoom N`, `spy` (the intelligence screen's figures true and nothing hidden behind `?`), `yesmen` (every AI answers 100 to whatever a played country offers it, without asking the script), `color TAG r g b`, `wireframe`, `render` (the map mode's layers and the console's together), `render <LAYER>`; an unknown word answers nothing |

## 3. What is not

Everything else in the table answers `Not built.`, *Ours*, in red: the commands the readme rates not worth building. `nukes` adds a bomb for each one given (`nukes.md`).

`debug render`'s layers here: `Trees`, `Borders`, `Text` (the province names and the far view's country names), `Rivers`, `Water` (the sea and the surf) and `WaterBorders` hide what they name. `Overlay`, `Objects`, `MapObjects` and `Effects` only turn over in the list, nothing here being drawn under them.

**Ours**: Escape shuts it, which was not read. The map does not scroll on Up and Down while it is open, which was not read either. The history is kept for the game and not written to `console_history.txt`. `tag` makes every screen again, which takes a moment and clears the messages' log where the original changes the country in place; the camera, the clock, the console and any recording go on. A country stopped being played has its ministers start again at their next hour. `date` refuses what is no date, where the original takes anything and walks the date backwards off it. `detailedlog` writes the whole save to `logs/<name>.log` in the player's folder, a name with a full path going there; the original's leaves the save's head out. `reloadfx` makes everything again, where the original remakes the one effect. `wireframe` makes the screens again the first time, a mesh having its lines only if made after it is asked for. The name given in `setnationalunity`'s, `surrender`'s and `guarantee`'s answers is the country's, where the reading has the literals around it and not what stands between (*inferred*).
