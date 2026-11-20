# The file checksum

The figure the original works out over its files as it starts, logs to the player's `logs/setup.log` as
`Checksum is HoI3 v4.02 (GSKH)    1499173604`, and writes as four letters on the main menu and in the lobby. Read in
`reversing/findings/FINDINGS-checksum.md`. Built 2026-11-13 (`lib/OpenHOI3.Content/Checksum`), **and it is the original's
figure to the unit** for Their Finest Hour and for BlackICE 15.1 (section 6).

Every rule below is the executable's [exe]; the files summed are the install's and a mod's [mod].

## 1. A file's checksum

Two sums from nought over the file's bytes taken unsigned: the first adds each byte, the second adds the first after each
byte, and each is brought back by subtracting 65535 once whenever it passes 65535 - so a sum that lands on 65535 stays
there, where a remainder would make it nought. The answer is the first in the upper 16 bits and the second in the lower.
It is the map cache's stamp's sum (`mapcache.md`; `CMapCache.Checksum`). A file that is not there gives nothing.

## 2. What is summed

Every term is added into one 32-bit figure, so the order is no part of the result. In the original's order:

1. `common`: its `.lua` files, then its `.txt` files; `common/countries`, `.txt`
2. `events`, `missions`
3. `units` and `units/models`, `.txt`; then `units` and `units/country`, `.lua`
4. `technologies`, `decisions`
5. `history/countries`, `history/diplomacy`, `history/provinces` **with every subfolder**, `history/wars`,
   `history/leaders`, `history/units`
6. `map/default.map`, `map/cache/time` and `map/cache/adjacencies.bin`, each alone
7. `cgm`, `.txt`, with every subfolder; `battleplans`, `.txt` and then `.pln`, with every subfolder
8. the version name `HoI3 v4.02`, its bytes taken **signed** - which changes nothing for its ASCII, and is kept so

Folders are `.txt` unless said. A file goes in where its name ends in the extension byte for byte, case and all: `a.TXT`
does not. A name with a character past ASCII is never listed, as the original's file system refuses one; nor is `.svn`,
as a file or as a folder to go down into. A folder whose name ends in the extension is not a file. Nothing else goes in:
not `interface`, `gfx`, `localisation`, `script` - the AI's Lua - `sound`, `music`, `scenarios`, any other map file, or any
subfolder of `common` but `countries`.

**The version name is the original's**, by the decision of 2026-11-13: "match the originals name". So the same
files give the same letters here as in the original, and a player can set this game's beside the original's main menu.

## 3. Which copy of a file

The original reads through one file system laid over several folders, the last mounted first: without a mod the player's
folder, `Documents\Paradox Interactive\Hearts of Iron III`, then `tfh/`, then the install's root; with one, the mod's
own player folder below it and the mod, then `tfh/` and the root - **the plain Documents folder not at all**
(`mods.md`, section 2; `reversing/findings/FINDINGS-modlayer.md`, which corrects this section's first reading of it).
A folder's names are all of theirs, each once, and a name is read from the first folder that has it. **In a folder a
mod's `replace_path` names, a file is listed only where its first copy is the mod's, or the player's folder's** - the
base game's beside them are not summed at all.

So the map's three files come from wherever they are first found, and **the map cache's two are the player's folder's**,
the one the original builds and the one this game builds the same byte for byte (`mapcache.md`). The checksum is
therefore worked out after the cache is current: here the cache is opened, and built where it is not the map's, with the
map at `INIT_MAP_LOGIC`, where the original checks it too, and the checksum runs at `CREATING_CHKSUM` after the history
(`loadingscreen.md`, section 3).

*Ours*: the client's search path is the played game's (`CDirectorySettings.SearchPath`, `CChecksumSearchPath.For`): the
player's folder, a mod's folder where one is played, `tfh/` and the root, a replaced folder by the first copy's place -
the savegame tool's the same (section 6). Until 2026-11-16 it put the player's folder last, which gave the same figure
for Their Finest Hour because the player's map cache is the shipped `tfh/map/cache` byte for byte (`mapcache.md`).

## 4. When

Once, at the first front end of a run - the original's once at startup - and kept for every front end after it.
*Measured*: 1.7 to 2.0 s for Their Finest Hour's 11,824 files, where the original's log has 6.7 s; 5.7 s for BlackICE 15.1's
14,177, the original's 10 to 27 s. A game started by a developer switch skips the front end and has no checksum.

## 5. The label

The main menu's `version_label` and the lobby's `checksum` say the name, a space and the four letters in brackets. With
the checksum as a signed 32-bit figure, the letters are the figure, then the figure shifted right by 4, by 8 and by 12
keeping its sign, each C's remainder by 26 made positive and added to `A`. The original adds its version's `executable`
figure to the checksum first; that is nought in every run logged, and nothing here sets it.

*Ours*: the name is `OpenHOI3` where the original's is `HoI3 v4.02` (`CVersionText`); where a mod is played the original's
`    Mod: ` and its localised name follow, since 2026-11-16 (`mods.md`, section 3).

## 6. Checked

`OpenHOI3.SaveCheck checksum <install> [mod name]` works the figure out and sets it beside the last `Checksum is` line of
the player's `logs/setup.log`, the mod's user folder's where a mod is named. On 2026-11-13: Their Finest Hour
`1499173604 (GSKH)`, 11,824 files, the same as the original's log of that day; BlackICE 15.1 `-410340735 (ZAUD)`, 14,177
files, the same as its log of 9 November, no file of the mod newer. On 2026-11-16, with the search path of section 3 as
the mod layer reads it: Their Finest Hour again, BlackICE GitHub `1599539514 (OXLS)` and BlackICE 16.0rc1
`-1864650988 (SDXE)`, each its log's. A log written before a summed file last changed differs, and says nothing.

## 7. What it does not guard

The original's figure is a weak guard, kept as it is and listed in `docs/enhanced.md`: it is a sum of sums, so swapping two
files' contents, moving a file between summed folders or adding an empty one changes nothing; it is 32 bits; and it leaves
out the AI's Lua, the interface and the localisation. A multiplayer of this game's own would want more.

## 8. The play checksum - ours

A second figure beside the original's, which stays as it is so that a mod's letters can still be set beside the
original's main menu. Decided 2026-11-20 (`docs/multiplayer.md`, section 2) and built the same day as `CPlayChecksum`:
**what two machines compare before they play one game.** Nothing of the original's stands behind it.

It is a SHA-256 hash of, in this order:

1. the text `OpenHOI3 play checksum 2`, which is raised when what goes in, or how, changes;
2. the engine's version, as its `engine.txt` names it (`docs/launcher.md`, section 2) - empty for a game run from its
   sources;
3. the rules a game is played by beyond the original's - the enhanced engine's, of which there are none yet;
4. every file of the folders `common`, `decisions`, `events`, `history`, `map`, `missions`, `script`, `technologies`
   and `units`, with every subfolder, **of the kinds a game reads there** - `.txt`, `.lua`, `.csv`, `.bmp`, `.dds`,
   `.tga`, `.map`: its path in lower case, its length and its bytes, the files in the order of their lower-case paths.

**Until 2026-11-20 every file was counted whatever its extension, and that refused two players of the same game each
other**: one install held, in those folders, a `Thumbs.db` Windows had made, an archive, three `.bak` files and a
map cache kept under another folder's name - sixteen files no game reads. What lies there and is of no kind a game
reads is a machine's own and is left out (`CPlayChecksum.Extensions`). Their Finest Hour is `FBB3-AB63` since.

**What went into it is written down** (2026-11-20, two players' figures still differing after that): as the main
menu works the figure out, `openhoi3_checksum.txt` is written beside the logs - the format, the engine's version and
the rules, then a line for every file that went in, its path, its length and the first sixteen digits of its own
SHA-256, and the checksum last; 13,646 files for Their Finest Hour. `python tools/checksumdiff.py <one> <other>`
sets two machines' files beside each other and prints the files only one has and those that differ.

Each text goes in with its length in front. So a file moved, two files exchanged and an empty file added each change
it, and the AI's Lua is part of it - the things the original's figure lets by (section 7).

**Which copy of a file** is the one a game here reads: a mod's folder, `tfh/`, the install, a replaced folder by the
mod's alone - and **not the player's folder**, which is no layer of game files here (`docs/compatibility.md`, entry 28;
`CChecksumSearchPath.ForPlay`). **Left out**: `map/cache`, which each machine makes for itself; `.svn` and `.git`
folders; and the interface, the pictures, the localisation, the sound and the music, which change what a player sees
and hears and nothing a game does. A mod's interface or localisation may therefore differ between two players who are
let play together.

A player is shown its first eight hexadecimal digits in fours, `FBB3-AB63` - **on the main menu, after the
original's letters**, `OpenHOI3 (GSKH) FBB3-AB63`, once it is worked out beside the menu, some seconds; the label is
moved left by what it has grown, its box running to the right from a fixed place (asked for on 2026-11-20). The
lobby's players box, which has no room for it, keeps the label without.

**Checked** by `OpenHOI3.SaveCheck checksum`, which prints it under the original's: on 2026-11-20 Their Finest Hour
`6B30-DA67`, 13,662 files in 2.3 s, and BlackICE GitHub `4AFB-2CB2`, 20,567 files in 3.6 s - the original's own figures
of the same runs unchanged, `GSKH` and `PURE`. **The client does not work it out yet**: it would add those seconds to
every start, and nothing reads it before there is a lobby between machines.
