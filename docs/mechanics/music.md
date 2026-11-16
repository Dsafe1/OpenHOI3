# Music

Read from `reversing/findings/FINDINGS-music.md`. Figures marked [exe] are hard-coded in the original; [music] come from the
`music/*.txt` files; [settings] from the player's `settings.txt`. Built in `Content/Music` (the songs), `Interface/Music`
(the pick and the loudness) and `src/Audio/CMusic` (the player).

## 1. When a song changes

In a game, a song is picked **only when nothing plays or the track playing has ended** [exe]. There is no timer, no event
or war that changes the music, no gap between songs, no fade and no crossfade, and the music plays on through a pause. A
song is picked only while the master volume is above nought [settings: `master_volume`]; the music volume does not stop the
picking, so at a music volume of nought songs go on being picked and played, unheard [exe].

Entering a game - a new one or a save - stops whatever plays on its first frame, the title theme as a rule, and a song is
picked at once [exe]. Leaving a game by any road stops the music [exe].

If no song may be picked, nothing plays, and the test is made again every frame until one may [exe].

## 2. Which song

### 2.1 The songs

Every `song = { name = ... chance = { ... } }` of every `music/*.txt`, in file order, the files in name order over the
install's layers [music; the order of the files is inferred]. Any other key at the top of a file is skipped by the
original in silence and reported here. A song's path is `music/` and its `name`, found through the layers, so a mod's
`music/` overrides the install's.

A song's `chance` is a **mean time to happen** [music], read as an event's is (`script.md`, section 4): its days are 1
where neither `days`, `months`, `years` nor a top-level `factor` sets them, and each `modifier = { factor = F ... }`
whose conditions hold multiplies them by `F`, in thousandths, in order - the original's `GetDays`, `CEventChance.Days`
here. The conditions are asked **in the played country's scope** [exe]. Their Finest Hour's chances ask
`total_offensives`, `total_defensives`, `total_sea_battles` and `total_we_bomb` 20 times, and BlackICE's music
`total_offensives` and `total_defensives` 4 more: they see the played country's war record since 2026-11-16, as the
day last counted it (`triggers.md`, section 5.1) - until then they answered false.

### 2.2 The pick

For each song in order that is not among the six chosen last [exe]:

- a roll of **5 to 9**, a whole number: a draw modulo 5, plus 5 [exe];
- its weight, `roll × 1000 × chance / 1000` in whole numbers;
- it is kept where its weight is **strictly greater** than the best so far, which starts at nought.

So the heaviest song wins, not one drawn by its weight. Equal weights go to the song read first; a weight of nought never
wins, so `factor = 0` means never; and **a factor of 2 or more beats every song of factor 1 whatever the rolls** (10 at
least against 9 at most) - factors are tiers more than odds. A Soviet player's four parts of the Soviet suite, factor 3
for `SOV` in the install, are all chosen before any other song, in an order the rolls decide.

**The roll's source is the original's own and not the game's** [exe]: a generator nothing seeds, on the stack. *Ours*: a
generator of the process's (`Random.Shared`) - never the game's, so the music draws nothing a replay or a save could
miss.

### 2.3 The six chosen last

After each pick, found or not, the oldest of the list leaves where it holds six; then the song chosen joins [exe]. So the
six songs chosen last are never chosen next, and **where five songs or fewer can win, the music falls silent** once each
has played, until another can [exe]. The list outlives a game: it is the program's, not the game's - here `Main`'s player,
`CSongPicker`.

### 2.4 `nextsong`

The console's `nextsong` makes the same pick and plays it at once over what plays, asking neither the volume nor whether
anything plays, and answers `Now playing ` and the song's path - nothing after it where none could be picked, the track
playing going on [exe]. The song goes on the list of the six like any other.

### 2.5 No song declared

Where no `music/*.txt` declares a song at all, the original plays every `music/*.mp3` but the three named below, the one
of whole seconds since the game was entered modulo their count, never the same twice running [exe]. The original waits for
the second to turn over in a loop, which with a single track never ends. *Ours*: it waits a frame instead, so a single
track plays once and the music then stays silent.

## 3. The main menu and loading

The title theme, `music/titletheme_heartsofiron.mp3` [exe], starts as the program's loading begins, before its first
loading screen, where the master volume is above nought, and plays through the main menu, the lobby and the making of a
game, until the game's first frame stops it [exe].

*Ours:* it starts as the first frame of ours is drawn - the loading screen - and not before: until then the engine's own
splash stands, which the original has none of. Every track is started at the settings' volume; until 2026-11-13 the title
theme began at full volume, the volume being set only by the first frame's look, which the startup held off.

At the main menu the original plays the title theme again when it ends - but two looks in one frame race for the track's
end, and the reading has the one that does not replay it winning as a rule, so that the theme plays once and the menu falls
silent (*inferred*, not heard). **Built so: the title theme plays once.** It is to be checked by ear against the original.

## 4. The end of a game

The end-of-game screen plays `music/victorymarchverdi.mp3` where the winner is the player's faction, and
`music/diesiraeintro.mp3` otherwise, a stalemate among it, where the master volume is above nought [exe]. It plays once and
goes on into the main menu after the screen's Exit, nothing starting after it [exe; the main menu's part follows section 3].

## 5. The loudness

The music plays at a share `v` = `master_volume` / 100 × `music_volume` / 100 [settings], set as the original sets it:
`(v^(1/4) × 10000 − 10000)` hundredths of a decibel [exe] - 0 dB at the full share, about −15.9 dB at a half, −100 dB at
nought. It follows the settings as they change, the game options window's sliders among them. A track does not loop [exe].

## Ours

- The roll is the process's generator (section 2.2); the original's is a generator nothing seeds.
- No song declared and a single track: silence after it, where the original waits for ever (section 2.5).
- The original pauses the music while a full-screen game has lost its display device, and picks nothing then; there is no
  such state here, and nothing pauses it.
- A session made again around the same game - the console's `tag`, `reloadinterface`, `reloadfx` - leaves the music as it
  is; whether the original's makes its in-game screen again, which would stop and pick, was not read.

## Not established

- Whether the title theme loops at the main menu (section 3).
- The order the original lists the `music/*.txt` files in.
