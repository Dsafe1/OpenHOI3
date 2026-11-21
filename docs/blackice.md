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
  | Lua for this engine | `script/openhoi/` |
  | windows a mod defines | `interface/openhoi/` |
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
     `settings.gui` the original would draw. The rows are the tab's own autosave row copied, so the mod adds no
     file; their words may be given in its localisation under `OPENHOI_OPT_...` keys and are English without. Kept
     in `openhoi_settings.txt` beside `settings.txt`. A tab of their own can come with the release made for this
     engine alone.
   - *The utility as windows of the game*: the interface for windows a mod defines, laid out in `.gui` files under
     `interface/openhoi/` and fed by the mod's Lua under `script/openhoi/`, with the two pages above first.
   - Before either, one reading in the fact base: whether the original lists `interface/` and `script/` with their
     subfolders, and what it makes of a `.gui` file or an element type it does not know.
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
