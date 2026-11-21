# BlackICE on the enhanced engine: the plan

How the enhanced mode (`enhanced.md`) reaches BlackICE and its players. **Planned with the user on 2026-11-21**; each
step says whether it is built. The next release of BlackICE, version 16, plays on both engines - the original with its
injected DLL, and this one in the enhanced mode. Releases after it are made for this engine alone.

The shape of the mode is `enhanced.md`, *The shape*; the launcher and how a mod names its engine are `launcher.md`.
This file is the order of work across the two repositories and what was decided for it.

## Decided, 2026-11-21

- **BlackICE on the normal engine is far enough to build on.** Playing it (`mechanics/mods.md`, section 7) no longer
  waits.
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
3. **The engine into BlackICE's repository.** Not built. The version the mod is made for, in a file there and never
   the binary (`launcher.md`, section 9); a script beside the installer's that fetches that release's two archives
   and checks them against its `SHA256SUMS.txt`; `engine = "..."`
   stamped into the descriptor as a release is made; for development, a link from the working copy to a local export,
   and the mover of files told to leave it.
4. **The installer.** Not built. The engine and the launcher into the mod's own `engine` and `launcher` folders, with
   each version of the mod; the two shortcuts; a 64-bit Windows asked for, the engine being 64-bit where the
   original is not - on a 32-bit one the installer says so and installs for the original alone; removal with an
   uninstall and with an older version; the step in the release workflow. About 250 MB more in a payload of about
   800 MB, under the installer's limit.
5. **What a file for both engines may hold**, as a document in the mod's `docs/` - scalar keys only, new files under
   `common/`, a gate on every new condition, nothing new in history (`enhanced.md`, *The shape*), and the naming above
   - and the third switch in the mod's Lua beside the two it has. Not built.
6. **The first features.** Not built.
   - *More game options*: a tab of the engine's own in the game options window - two more rotating autosaves,
     scrolling at the screen's edge, where an event's window comes up - and the enhanced mode's own options after
     them.
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
