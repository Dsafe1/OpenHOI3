# BlackICE on the enhanced engine: the plan

How the enhanced mode (`enhanced.md`) reaches BlackICE and its players. **Planned with the user on 2026-11-21**; each
step says whether it is built. The next release of BlackICE, version 16, plays on both engines - the original with its
injected DLL, and this one in the enhanced mode. Releases after it are made for this engine alone.

The shape of the mode is `enhanced.md`, *The shape*; the launcher and how a mod names its engine are `launcher.md`.
This file is the order of work across the two repositories and what was decided for it.

## Decided, 2026-11-21

- **BlackICE on the normal engine is far enough to build on.** Playing it (`mechanics/mods.md`, section 7) no longer
  waits. **Stage 2 there is finished** by the user's word the same day, and BlackICE's working copy holds
  `common/openhoi_enhanced.txt` from then on: every game made from it is an enhanced one, and the faithful saves are
  no reference for it - "If we need more references we will make them again."
- **The engine is a core part of the mod's release**, not a part of the installer a player may leave out.
- **The launcher is shipped with it, and plays the base game too. Both are installed with each version of the mod**,
  in its own folder - the engine in `tfh/mod/<mod>/engine/`, the launcher in `tfh/mod/<mod>/launcher/` - so that a new
  version of the mod brings a new launcher and none has to stay compatible with an older one. The launcher takes the
  engine beside it as its own (`launcher.md`, section 2), so the one copy plays Their Finest Hour, BlackICE and every
  mod that names no engine. The installer's shortcut to
  `hoi3_tfh.exe -mod=...` has never been released, version 16 being its first, so the shortcuts are free to be made
  anew: one to the launcher, one to the original with the mod.
- **An enhanced game's saves have a folder of their own**, `save games/openhoi` in the player's folder: they hold keys
  the original does not know, and kept apart nobody loads one there.
- **Whatever a mod holds for this engine is in a subfolder of its own or carries a prefix**, so that it is plain what
  is the new engine's and nothing of it is mistaken for the original's:

  | what | where |
  | --- | --- |
  | the file that turns the mode on | `common/openhoi_enhanced.txt` |
  | other data files of ours | `common/openhoi_*.txt` |
  | Lua for this engine | `script/openhoi/`, required with a slash: `require('openhoi/x')` |
  | elements of the mod's inside the game's own windows | `interface/openhoi/` |
  | windows of the mod's own, a page a file | `script/openhoi/windows/` and the folders below it |
  | their texts | `localisation/openhoi/` |
  | an enhanced game's saves | `save games/openhoi/` |
  | an engine a mod ships | `tfh/mod/<mod>/engine/` (`launcher.md`, section 2) |
  | a launcher a mod ships | `tfh/mod/<mod>/launcher/` |

  A scalar key inside a file the original reads - `engine = "..."` in the descriptor, a define - is the one thing that
  has neither, being a key and not a file.
- **The first pages of BlackICE's utility to be made native**: one that only reads (a country's modifiers or its
  flags) and one that posts commands (the conversion of resources), so that the interface for a mod's windows is
  proven both ways before the rest follow.

## The steps

1. **The mode's skeleton, in this repository. Built 2026-11-21** (`enhanced.md`, *The shape*):
   `common/openhoi_enhanced.txt` and its `without` list (`CRulesReader`); the rules a game carries (`CRules` on
   `CGameState.Rules`), in the state's hash as its last part and in the play checksum; an enhanced save's two keys,
   `openhoi_rules` and `openhoi_engine`, and its folder; `OpenHOI3.Enhanced` and `OpenHOI3.Fixes` for a mod's Lua; and
   one fix behind it as proof, a minister's technology decay. 18 tests.
2. **Releases of the engine. Built 2026-11-21, and none made yet** (`launcher.md`, sections 2 and 7): a version of
   its own in `engine.txt`, the one program serving both modes - `openhoi3-<x>` on a clean tree tagged `v<x>`, the
   date and commit for a build between two (`tools/engine_version.ps1`); `tools/build_release.ps1`, which packs the
   engine, the launcher and the two together as archives with their SHA-256 and the release's notes, and prints the
   commands that publish them on the repository's releases - the tag's page being the link to the source the GPL asks
   a mod's release to carry (`launcher.md`, section 10); and a launcher taking the engine beside its folder as its
   own. **The first tag and its publishing are the user's to do.**
3. **The engine into BlackICE's repository. Written 2026-11-21, in that repository's working copy; tried by the user
   the same day against the releases he built, v0.1.1 and v0.1.2, the mod naming `openhoi3-0.1.2`.** The version the mod is made for is one
   line, `ENGINE_VERSION` in its `releaseCommon.py`, and never the binary (`launcher.md`, section 9);
   `installer/fetchEngine.py` fetches that release's two archives, checks them against its `SHA256SUMS.txt`, refuses
   an engine whose `engine.txt` names another version, and unpacks them - or takes them from this repository's
   `export/release` with `--from`; `engine = "..."` is stamped into the descriptor as a release is made. **The
   working copy's descriptor names no engine**, so that a developer plays it on whatever engine he has built, the
   launcher's own; nothing links an export into the working copy, and the mover of files, which clears only the
   mod's own folders, needed nothing.
4. **The installer. Written the same day, and installed and played by the user: "Install test2 works great."** The
   engine and the launcher into the mod's own `engine` and `launcher` folders, with each version of the
   mod, as part of the mod and not a component of their own; two shortcuts, "(OpenHOI3)" to the launcher with
   `--install=` and `--mod=` naming the install and this version (`launcher.md`, section 6), and "(original game)"
   to `hoi3_tfh.exe`; on a 32-bit Windows the engine, the launcher and their shortcuts are left out and the system
   check page says so; an uninstall and the removal of an older version take the mod's folder whole, the engine
   with it; `--no-engine` builds for the original alone; the fetch is a step of the release workflow. About 100 MB
   more in an installer of about 900 MB, under its limit.
5. **What a file for both engines may hold. Written 2026-11-21**, as `docs/openhoi/README.md` in the mod's
   repository - scalar keys only, new files under `common/`, nothing new in history (`enhanced.md`, *The shape*), the
   naming above, and how to try a change on both - with the third switch in the mod's Lua: `script/autoexec.lua` sets
   `G_OPENHOI` and `G_OPENHOI_ENHANCED` from the global this engine gives. **A gate for a new condition is not
   built**: script that leans on a condition the original does not know needs one the original answers false and
   the enhanced mode true, and this engine gives none yet, so the mod's document forbids such conditions until it
   does. It is wanted before step 7's first rule that script can ask.
6. **The first features.** The first is built, the second not.
   - *More game options*. **Built 2026-11-21** (`mechanics/gameoptions.md`, section 5): two more rotating autosaves,
     scrolling at the screen's edge and where an event's window comes up, as four rows on the game options' **Game
     tab** - not a tab of their own, by the user's choice: the window's four tabs fill its width, and a fifth in
     `settings.gui` the original would draw. **The mod declares the rows' elements itself**, in
     `interface/openhoi/gameoptions.gui`, which this engine reads in an enhanced game after the other `.gui` files -
     the first use of that folder, and the way the utility's windows are to come - and **every word is the mod's
     localisation**, `localisation/openhoi/options.csv`: the engine words nothing (both the user's, 2026-11-21). Kept
     in `openhoi_settings.txt` beside `settings.txt`. A tab of their own can come with the release made for this
     engine alone.
   - *The utility as windows of its own*, with the two pages above first. **Built 2026-11-22 as far as a page that
     reads** (`mechanics/modwindows.md`): **drawn with Dear ImGui by the page's own Lua** - the user's decision of that
     day, on seeing the first shape, pages laid out in the original's `.gui` elements, which he did not like the look
     of and which is gone. One window on Insert - a tab for each group of pages, the tab's pages down its left, the page chosen beside them (the user's, the same day); a page for each file of `script/openhoi/windows/`, its
     `Collect` reading the game now and then and its `Draw` drawing every frame with `OpenHOI3.ImGui`, given no game
     to ask; and the first pages, BlackICE's Active Flags and Global Flags. **And the first page that changes the
     game**, the same day: the conversion of resources, a setting of a country's own with its command
     (`mechanics/ic.md`, section 4.1), read and ordered by BlackICE's Resource Conversion page - a page gives an
     order as it draws and the screen posts it (`mechanics/modwindows.md`, section 6.1). **And eight more pages**, the
     same day: the Inspector's Active Variables and Active Event Modifiers, and Game Info whole - Traits, Generals,
     Modifiers, Techs, Units, Unit Models - on BlackICE's own data functions, which run here as they are
     (`mechanics/modwindows.md`, section 6.3). **And Country Info whole**, the same day: Country Modifiers,
     IC Days, Puppets, Strategic Res., Strategic Trades, Global Market, National Focus, Minister Buildings and the
     three AI forms, their setters posting the original's `CSetVariableCommand` as the page gathers, which goes the
     way of a player's order (section 6.1). **And Setup**, the same day, with nothing asked
     of the engine: Country - the country every page reports on, the mod's Lua global `G_PlayerCountry` as on the
     original - and Misc. Its Special page, which moves the original's 3D unit sprites aside for the sake of a
     32-bit address space, is left out: this engine loads none, and a page writes nothing into an install.
     **And Stats**, the same day: Setup, which switches the mod's collection of statistics and edits its list of
     countries, and Statistics, which reads the run's files and draws a line a series (`PlotLines`). Not carried
     over: all series in one plot, the dates under the mouse, the mod's external plotting tool. **The collection
     itself, the mod's Lua from the ministers' ticks, does not work on this engine** (the user's, having tried
     it) and is set aside: after the port it is to read a record of the engine's own, and the sliders' and trade
     AI's pages to move into the game's own screens (`enhanced.md`, *Interface*).
     **And Options and Help**, the same day. Help's three pages - Misc, Ministers + Buildings, National Focus -
     are the mod's text and data drawn. Options is one page, Utility Options, on the window's own settings, which
     the engine holds and a page reaches through `OpenHOI3.Window` (`mechanics/modwindows.md`, section 6.4): the
     key that shows it, its look, its text size, its layout. The overlay's Game Settings and Custom Auto-Saves
     pages are no pages here - they are the game options' rows (step 6, above) - and its crash reports and debug
     console are BiceLib's own.
     **And three of the Inspector's larger pages**, the same day: Province Buildings, on the mod's own data and
     a read of a province's figures in BiceLib's shape; OOB Browser and OOB Report, on a read of a country's whole
     order of battle, the tree and the counts made in the mod's Lua (`mechanics/modwindows.md`, section 6). The
     browser's "Move to" is not there: a page has no way to move the map.
     **The Inspector's last three**, by the user's word of that day (`enhanced.md`, *Interface*): the Unit
     Inspector and the Custom Mapmode are **not to be pages** - the first goes into the game's own unit panel, on
     new kinds of element, the second into the engine as a list a right click on a map mode's button opens - and
     Combat Reports stays a page, on a record of finished battles the engine is to keep.
     **Not built**: those three; the Debug tab; and the two reads Country Modifiers asks of BiceLib, a country's offmap
     IC and its trickleback. The setting's effect for script is not wanted for now (`enhanced.md`, *Rules*). Agreed, 2026-11-22: **no single file that registers the pages** - one a submod would have to
     overwrite - but a file a page, found by listing a folder over every layer; and the widgets a page may ask for
     grow as pages need them ("lets not limit ourselves"). What a mod declares in `.gui` files stays for what stands
     inside the game's own windows, as the game options' rows do.
   - **The fact base's reading is done, 2026-11-22** (`reversing/findings/FINDINGS-guifolders.md`): the original
     never opens a subfolder of `interface`, `localisation` or `script` - its listing takes one folder and drops
     directories, and `script` is not listed at all, a file there opened only by a `require` that names it, with a
     slash and not a dot; a top-level window no code asks for is an unused type; an extra child of a window the
     original makes is built, shown and drawn; and an unknown kind inside a `.gfx` type block is a null write as it
     loads, so no new kind goes into a `.gfx` file the original reads. So everything of this engine's in the three
     folders is in their `openhoi` subfolder, the localisation tables with it since that day.
7. **The fixes and the rules** of `enhanced.md`, *BiceLib's features, each one's place*, one at a time, each with its
   name in `CFixes` or its define, a test for each arm, and where the DLL needs it for the original a second
   declaration in the mod.

## Open

- **A save of the original's, loaded in an enhanced game.** An enhanced game lists only its own folder, so a campaign
  begun on the original cannot be carried on here. Listing the original's folder too would allow it; a save made with
  the DLL holds what this engine does not.
- **A save whose rules are not the folders'.** A loaded save's rules stand (step 1), but the mod's Lua is told the
  folders' - `OpenHOI3.Enhanced` is set as the scripts are loaded, before any save is.
- **Whether new defines carry the prefix.** Proposed: yes, `openhoi_` in front, though a define is a key.
