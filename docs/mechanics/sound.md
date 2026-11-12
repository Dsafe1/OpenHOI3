# Sound effects

Read from `reversing/findings/FINDINGS-soundeffects.md`. Figures marked [exe] are hard-coded in the original; [sfx] come from
`interface/sound.sfx`; [gui] from the interface files; [settings] from the player's `settings.txt`. Built in `Content/Sound`
(the file), `Interface/Sound` (the loudness and which sound is played) and `src/Audio/CSoundEffects` (the player). The music is
`music.md`.

## 1. The sounds

Every sound effect is named in one file, and played by its name. Nothing is positional: a sound is the same wherever on the
map its cause is [exe]. Every sound the game itself plays is the played country's or the local player's own doing (section 7).

With a negative `master_volume` the original makes no sound object at all and plays nothing [exe; settings]; here the volume
comes out at nought (section 4), which is the same silence.

## 2. `interface/sound.sfx`

**One file**, through the layers - a mod's over the install's [exe]. Every top-level key is one sound, the key its name. In
each [exe]:

| key | default | what it does |
| --- | --- | --- |
| `file` | none | the wav, in `sound/` |
| `pitch` | none | **a playback rate in Hz**, held between 100 and 100000; applied only where given |
| `pan` | 0 | held between -10000 and 10000 |
| `voicetype` | 64 voices | **exactly `single`**, case and all, gives one voice; anything else, or nothing, 64 |
| `volume` | 100 | the sound's own loudness, in hundredths |

Any other key is reported, as the original reports it [exe]. **The base install and Their Finest Hour use only `file`** [sfx].

A name is looked up **without case**, and **the first of a name wins**, a duplicate being reported [exe]. A name the file has
not got plays nothing, and is reported once [exe].

## 3. Voices and playing

A 64-voice sound plays on the first of its voices not playing, the first restarting when all 64 are; a one-voice sound restarts
its voice [exe]. Every sound plays from its start and none loops [exe].

**Almost every sound is played "unless already playing"**: a sound asked for while any voice of it still plays is **skipped** -
not restarted and not doubled - so a box of twenty divisions selected is heard once [exe]. **Different sounds always mix
freely.** The exceptions play over themselves, up to their voices: the event window's `message` (section 7) and a window's or a
button's up, down and over sounds (section 5) [exe].

## 4. The loudness

v = `master_volume` / 100 × `sound_fx_volume` / 100 [settings], and every sound is scaled by **0.5·v + 0.5, held at 1, where
v is above nought, and by nought where it is not** [exe]. A sound plays at its `volume` / 100 times that scale, as
`(int)(that × 10000) − 10000` hundredths of a decibel [exe]:

| v | decibels, `volume = 100` |
| --- | --- |
| 1 | 0 |
| 0.5 | −25 |
| just above 0 | about −50 |
| 0 | silence |

**Linear in decibels**, quite unlike the music's fourth-root curve (`music.md`, section 5): the effects never fall below about
−50 dB until they stop altogether. A `volume` above 100 would ask for more than 0 dB, which the original's sound card refuses,
keeping the voice's last volume [exe, *inferred*]; it is held at 0 here. The scale is asked at each play, so a sound follows the
settings as they change, the game options window's slider among them. `ambient_volume` is read by nothing [exe].

The maintainer's own settings on 2026-11-12 - `master_volume` 55, `sound_fx_volume` 5 - play a sound at −48.6 dB, which is
what the first run with sound gave.

## 5. Buttons and checkboxes

**A button's click** is, in order [exe; gui]: the button's own `clicksound`; else its sprite's `clicksound` (the `.gfx`
`spriteType`'s); else `click`, the default the in-game screen and the front end both give. **The choice goes by whether a name
is written, not by whether it is a sound**: `clicksound = close_window` or `start_game`, neither in `sound.sfx`, leaves the
button silent - ten such buttons in the base game's and Their Finest Hour's files [gui]. (`CClickSound`)

It sounds **when the left button is let go over a button it pressed**, after what the button does, unless the click is already
playing [exe]. The right button, a disabled button and a **keyboard shortcut** make no sound [exe].

**A checkbox** sounds its `clicksound` (else the sprite's, else `click`) when it is ticked, and **its `upsound`** (else the
sprite's `clicksound`, else `click`) when it is unticked [exe]. Its `oversound` is read and never played [exe].

**A button's over, down and up sounds** and **a window's up and down sounds**, played as it is shown and hidden, are read by
the original; **no button or window of the base game or Their Finest Hour sets any of them** [gui]. They are read here and not
played.

## 6. While a game is entered

**Nothing is heard while a game's screen is being built**: the original switches its sound off as the game is entered and on
once the screen stands [exe]. Here the sounds are off from the session's making to its first frame. (`CSoundEffects.Enabled`)

## 7. What the game plays

Each "unless already playing" but where it says otherwise [exe]. **Built** marks what plays here.

| sound | when | built |
| --- | --- | --- |
| `click` | a button or checkbox with no sound of its own (section 5) | yes |
| `click` | a province selected on the map, in a game or in the lobby | yes |
| `click` | a map mode chosen, unless it is already on | yes |
| `click` | the zoom buttons and their keys, Page Up and Page Down - not the mouse wheel | yes |
| `click` | the game's speed changed, by the keys as by the buttons | yes |
| `click` | a merge posted, a brigade moved in the reorganise window, the player's unit disbanded, a reinforcement posted | through their buttons' own click |
| `army_selected`, `navy_selected`, `air_selected` | each unit newly selected, by its kind - a headquarters as an army | yes |
| `army_move`, `navy_move`, `air_move` | the player's right-click order (below) | yes |
| `error` | a right-click order the target refuses (below) | yes |
| `message` | each alert icon that comes up | yes |
| `message` | each event window shown - **played over itself**, so two windows at once are heard twice | yes |
| `land_combat_1` … `6`, `naval_combat_1` … `3`, `air_combat_1` … `2`, `bomb_1` … `3` | while a battle's window is up (section 8) | yes |
| `army_selected` | a revolt in a province the player holds, with its `REVOLT` message | yes |
| `convoys_1` | ships lost from one of the played country's convoys - only a raid that sank something takes any | yes |
| `convoys_2` | the played country's raider sinking a convoy's ships, once a hit | yes |
| `tech_discovered` | the played country finishing a level of the day's research | yes |
| `declarewar` | the player declaring war - by the diplomacy screen, the AI or the `war` effect, which all declare alike | yes |
| `declarewar_onus` | war declared on the player; nobody else hears either | yes |
| `diplomaticoffer` | an offer's icon coming into the player's bar | yes |
| `diplomaticsuccess`, `diplomaticfailure` | the answer to one of twelve offers, with its message (below) | yes |
| `ding` | a line added to the multiplayer connection log | no - there is no multiplayer |

**The answer to an offer** sounds `diplomaticsuccess` where it is accepted and `diplomaticfailure` where it is refused, heard
by the side told the message's first key - the actor, the asker - and by nobody else [exe]. The twelve: asking for
lend-lease, an alliance, asking military access, offering it, a call to arms (refused, only where an alliance was broken),
a non-aggression pact, a faction, a trade, an expeditionary force, a licence, debt and peace. A faction's acceptance
sounds for the inviter, or, on a join, for every member told; peace's for the asker and every played attacker still in the
war. A guarantee, an embargo, influence, alignment and offering lend-lease sound nothing. A trade's messages, hidden from a
player who has automated his trade, are silent with them.

**Where it lives here.** What sounds with a message is the message's own (`CPactMessage.Sound`, `CMessages.PactSound`);
the rest the simulation raises for a country (`CGameState.SoundCued`) - convoys in `CConvoyLosses`, a level in
`CResearch.RunDaily`, a declaration in `CDeclarations.Apply` - and the client plays a sound only where that country is
its player. The simulation names the sound and never plays it, so nothing of the game changes and a headless run hears
nothing.

**A right-click order** (`movement.md`, section 1; `fleetsandair.md`) [exe]:

- a land unit sent to a province it may go to sounds `army_move`; one refused sounds `error`; one halted where it stands is
  silent; a headquarters under the AI's plan sounds `army_move` for an objective or an axis set;
- a fleet sounds `navy_move` only where it is sent straight there - with Shift, or from a battle - and **before** its range is
  asked, so a fleet sent out of range sounds it too; a coast no sea reaches, with nothing aboard to land, sounds `error`;
- an air unit sounds `air_move` after its order, whatever comes of it - the orders window opened as a rule.

`buzzbeep`, `nuke` and `unit_upgraded` are in `sound.sfx` and **never played** [exe]. The settings window plays no sound to
try the volume, and **messages make no sound** [exe].

## 8. The battle window

**While a battle's window is up**, a battle is heard: when the sound last started has ended, the next is picked at random -
the roll's magnitude modulo the kind's count, counted from one - and played [exe]:

| kind | sounds |
| --- | --- |
| land | `land_combat_1` … `land_combat_6` |
| at sea | `naval_combat_1` … `naval_combat_3` |
| in the air | `air_combat_1`, `air_combat_2` |
| a bombing, of any kind | `bomb_1` … `bomb_3` |

So a battle's window is a continuous chain of its kind's sounds, the first starting as it opens, and **it goes on while the
game is paused** - no pause was seen asked [exe, *likely*]. The roll is the window's own generator, which the original seeds
from the clock: nothing of the game's state. (`CBattleSounds`, `CCombatWindow`)

## Ours

- **A checkbox sounds when it is let go**, as a button does; the original's sounds as it is pressed, which is when the
  original's checkbox ticks. A checkbox here ticks as it is let go, and the sound goes with the tick (`interface.md`).
- **`pan` is not applied**: a sound with one is reported once and played in the middle. No file of the base game's or Their
  Finest Hour's has one.
- `pitch` is applied as a playback rate against the wav's own rate.
- The battle window's roll is the process's generator.
- A wav that cannot be read is reported and silent. The original's, by its failure path, would play its first sample in its
  place and give every later sound its neighbour's volume, pitch and pan [exe, *inferred*]; no shipped file is missing.

## Not established

- Which button posts the reinforcement the original clicks for; it is a button's own click here.
- How often the original runs a button's up and down sounds, which no vanilla button has (section 5).
- What the window flag that gates a window's up and down sounds is.
