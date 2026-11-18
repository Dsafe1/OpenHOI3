# The front end: the main menu and the lobby

What the player sees before a game and after one: the original's main menu, and its lobby as far as one start needs - a
new game and a country chosen, or a save loaded. Read out of the original on 2026-11-06
(`reversing/findings/FINDINGS-frontend.md`, A the main menu and B the lobby) and built by the decision of the same
day, "Pared-down lobby": the lobby of `lobby.gui` with the start and the saved games, where `CLAUDE.md` had said it was
not coming back. `confirmed` unless marked. **[exe]** a rule the executable holds, **[mod: ...]** what a file may change.

## 1. When it comes up

1. **The program starts on it** - the main menu - unless a developer switch says what game to play: `--country=`,
   `--load=` or `--replay=` start that game straight away, as before.
2. **A game left by Resign comes back to the main menu** [exe: never the lobby], through the end-of-game screen (`endgame.md`).
3. Every visit is a new front end, its panels made anew (A.1, A.6).

## 2. The main menu

`temp_frontend.gui`'s `country_selection_panel` [mod] - the tiled `Background`, the opaque `frontend_backdrop` over the
whole of the map, the engine's logo and `version_label` - with `mainmenu_panel` shown on it.

| button | does here |
| --- | --- |
| `single_player_button` | the lobby (section 3) |
| `settings_button` | `settings.gui`'s `menu_settings`, the game options window's four tabs (`gameoptions.md`) |
| `exit_button` | ends the program at once - no question, nothing saved [exe] |
| `nudge_button` | hidden, as the original always hides it [exe] |
| `multi_player_button`, `tutorial_button`, `credits_button` | greyed: not built |
| `gg_micro_button` "Buy Bonus Items" | greyed, its tooltip ours: no store is opened (*Ours*) |
| the four link buttons | hidden (*Ours*) |
| `toggle_quick_normal` | the Quick Start panel in the main menu's place, `simple` kept in step (`bookmarks.md`, section 4) |

No button has a tooltip [exe]. A click on the map does nothing here; the map is under the backdrop and not seen (A.3).

## 3. The lobby

`lobby.gui`'s `lobby` [mod], the front end's own window hidden so the map shows behind it. **On opening** [exe]: the world
made at the start date, the start selected, and the Historical Start tab shown; nothing in the save list ticked and
nothing loaded (B.1). The camera over the map's (2900, 1600), its rows counted from the bottom [exe] (A.2), at a zoom of
ours, 1.43, which frames Europe from Ireland to Turkey as a screenshot of the original does (*measured*).
**That zoom is fixed**: the wheel does nothing on the lobby's map, decided on 2026-11-09. A chosen country is
lit whole, every province it owns taking the lift a selected province takes, in place of the one clicked (decided on
2026-11-09; the amount of the lift is the in-game selection's, set beside no screenshot of the lobby).

**The chat log** `chatlog` [exe] (B.1, B.2): a row of `temp_frontend.gui`'s `lobby_chat_text` a line, a paragraph cut
where it is wider than the row - CONNECTED "Connected." as the lobby opens, the start's name in yellow and its
description each time it is chosen, then PLR_CONNECT "Player $NAME$ joined the game." with the player's name. Its edit
box is hidden in a game of one.

**The players' box** `multiplayer`, shown in a game of one too: FE_NUM_PLAYERS "Number of Players: 1", `checksum` the
version label, `OpenHOI3` and the original's four letters (`checksum.md`, section 5), and a row of `multiplayer_entry` for the player - `player_shield` the chosen country's, `name` the
player's name, which is `settings.txt`'s `lastplayer` (*inferred*: the original makes its human from the application's
name, and a screenshot of the original and a reference file both say "Player").

**The tabs**: `newgame_tab` shows `bookmarks`, `savedgame_tab` `save_games`; a pressed tab takes frame 2 and the other
frame 1. `scenario_tab` and `cgm_tab` are hidden (*Ours*).

**The bookmarks** (B.2; `bookmarks.md`, sections 1 and 2): a row of `temp_frontend.gui`'s `bookmark_entry` for each
historical bookmark of `common/bookmarks.txt` [mod], in its order - its `icon` the button's sprite, its localised `name`
as `title`, its date as ` 0:00, 1 January, 1936` (B.2) as `date`, frame 2 while it is chosen. The lobby opens with the
first chosen. Choosing one: the world at its date held to the defines' range (a loaded save thrown away), and **a
`shield` for each of the bookmark's `country` entries** [mod: lobby.gui]:
each unturned shield's corner `shield_offset` from the last, the row centred on `shield_position` from the bottom middle,
and each turned a quarter about its middle by its type's `rotation`, so that the flags stand upright and apart (the
middle one's middle at x 960 and all at y 845, as on a screenshot of the original - *measured*); each with SHORTCUT_SHIELD
and choosing its country, the map going to its capital. With no bookmark read the list is one row of the defines'
start date alone, and no shields (*Ours*).

**The saved games** (B.3): the player's `save games` folder and the install's, newest first, made afresh as the tab is
pressed - as the save dialog lists them (`savegame.md`, section 11). A row is `savegameentry`: checkbox `save_game`,
`shield` the save's `player` read off the head of the file - the date first, `player` second, or third after `scenario` -
`title` the file name before `.hoi3`, `date` the head's date as a bookmark's. **Choosing a row loads the save at once**
[exe] - the row ticked and the others not, the start unchosen and the shields gone - and the save's world is on the map,
**its `player` chosen and the map centred on that country's capital** [exe], **at the lobby's fixed zoom**: the original
keeps one camera for the front end, so a save loaded moves it and does not zoom it. Choosing another loads that. *Ours:*
each world gets a map view of its own, and the new one takes the old one's camera before it is centred; until 2026-11-14
it did not, and a save loaded came up over the whole map, centred there on the capital at that zoom - reported in
testing.

**Choosing a country** (B.4): a click on the map chooses **the owner of the province** - not its holder - where it has
one [exe]; a save's country may be changed so, its own player then left to the AI. A shield of the start chooses too.

**The panel** `singleplayer` (B.5), hidden with no country chosen: `player_shield` its shield; `selected_nation_label` its
name; `selected_fog` **the government's name**; `icon_factions` with its faction's frame where it is in one, hidden
otherwise; `head_of_state` the name in the head of state's post, `photo_head_of_state` that minister's portrait; `wars_label` **ATWAR
"At War." where it is at war with anyone, else ATPEACE "At Peace."** [exe]; `year_label` WORLD_YEAR, the start's name or
SAVED_GAMES "Saved Game" and the world's date. No tooltip on any of it [exe]. `victoryconditions_button`: greyed (not
built).

**Play** (B.6) may be pressed where a country is chosen that owns a province, and on the saved games' tab a save is
loaded, or on the start's tab none is; its tooltip gives the first that fails - NOT_SELECT "All players must select a
country.", NOT_EXIST "$TAG$ does not exist at this time.", CANT_START_REASON_SELECT_SAVE_GAME, or
CANT_START_REASON_SELECT_BOOKMARK. Pressed: a new game is set up from the world as the lobby holds it - the two days and
the rest (`tick.md`, section 6) - with the chosen country played; a save goes on as loaded, the chosen country played.
**Back**: the main menu, the world put back to the start and a loaded save thrown away [exe].

## 4. The options

Read 2026-11-06 (`reversing/findings/FINDINGS-frontend.md`, C the panel and D what it does in play) and built the same day.

### 4.1 The panel

`options.gui`'s `option_window` [mod: only the base game's `interface/` has it], **a window of its own beside the lobby**,
where its file puts it, at the upper right. Shown as the lobby opens and after each save loaded, its boxes then written from
the world's choices - a loaded save's come back into it; hidden by Back and Play [exe]. No tooltip [exe]. Seven rows of the
original's, five with a box:

| row | label | values, in order [exe] |
| --- | --- | --- |
| `difficulty` | FE_DIFFICULTY | DIFF_VERY_EASY, DIFF_EASY, DIFF_NORMAL, DIFF_HARD, DIFF_VERY_HARD - 0 to 4, a new game's Normal |
| `arcade` | FE_ARCADE | STANDARD_MODE, ARCADE_MODE - **its arrows greyed**, Arcade not being built (*Ours*) |
| `diplomacy`, `production`, `politics`, `intel` | FE_SETTING_* | OPT_PLAYER_CONTROL, OPT_AICONTROL - the area's flag, 0 or 1 |
| `technology` | FE_SETTING_TECHNOLOGY | OPT_PLAYER_CONTROL, OPT_AICONTROL, TECH_AUTOMATED, LEADERSHIP_AUTOMATED - the Technology setting, 0 to 3, **its flag left alone** [exe] |

The Armies row and a seventh, automatic deployment, are the original's too and have no box or arrows anywhere - a mod
cannot show them [exe]. **Each arrow wraps** both ways [exe], and each press writes OPTION_CHANGED "$FROM$ has changed
to: $TO$" in the lobby's chat, **the label's colon kept**: "Difficulty: has changed to: Hard" [exe]. A loaded save's
difficulty and game mode may be changed [exe]. `CLobbyOptions` in `src/FrontEnd`, `CLobbyOptionsText` in
`Interface/Screens`.

### 4.2 Where each is kept

**Difficulty and game mode** are the game's (`CGamePlaySettings`), saved as `gameplaysettings = { setgameplayoptions = {
difficulty mode } }`. **The rest is the player's record** (`CPlayerAutomation`), saved at the top level after `player`:
`ai = { ... }` the seven flags in the order diplomacy, production, technology, politics, intelligence, Armies, deployment -
each 0 the player's or 1 the AI's, but the sixth, which holds the Armies level, 7 Player Control, 3 Corps, 2 Armies, 1
Army Groups, 0 Theatres - then `automate_trade`, `automate_sliders` 0 to 3 and `automate_tech_sliders`, the Technology
setting, 0 to 3 [exe]. A fresh record is all nought with the Armies level 7 (*inferred* in the reading). A loaded flag
other than 0 or 1 is refused [exe]. Every played country goes by the one record [exe: the local player's, asked for every
human's country] (`savegame.md`, section 17).

### 4.3 What they do in play

**The difficulty does two things and nothing else** [exe]: (1) a static modifier each country gets with the day's rebuild
(`modifiers.md`; `CCountryModifiers`), a played country's level's `_player` block and every other's `_ai` -
`very_easy`, `easy`, `hard`, `very_hard`, Normal none - which block goes with which level [exe] and what is in them [mod:
common/static_modifiers.txt]; (2) `BM_DIFFICULTY`, a combat modifier on every unit of a played country, nobody else's, on
land, at sea, in the air and on both sides of a bombing, attacking and defending: +2, +1, 0, -1, -2 for levels 0 to 4
[exe] times `COMBAT_DIFFICULTY_IMPACT` [mod: common/defines.lua, military, 0.2] - and the AI's odds of an attack weigh it
the same way. **"Played" is any played country**, not the local player alone [exe]. `CDifficulty` in `Sim/Settings`.

**The game mode is supply alone** [exe]: Arcade draws every unit's supplies from its capital and leaves the network out.
Not built; *Ours*.

**An area handed to the AI gives the played country that area's minister**, working as any other country's
(`CAutomation.AiRuns`) [exe]:

| area | what runs for the played country |
| --- | --- |
| diplomacy | the foreign minister (`foreignminister.md`), and the install's Lua trading for it each hour (`trade.md`) |
| production | the production minister (`ministers.md`); **and the AI strategy's walk of its missions and victory conditions runs for a played country only while this is the AI's** (`aistrategy.md`) [exe] |
| technology | the tech minister by the Technology setting: 1 the leadership sliders and research, 2 research, 3 the sliders [exe] |
| politics | the politics minister (`politics.md`, section 12) |
| intelligence | the intelligence minister (`espionage.md`, section 15) |
| Armies | nothing [exe] |
| deployment | a played country's finished units placed by the AI **every hour**, where an AI country's are placed daily [exe] |

A minister is made for the area as the player hands it over and dropped as he takes it back (`CAutomation.SetMinisters`):
made with a new minister's draw on the game's generator - its first work one to three hours on, the intelligence
minister's one to ten, a foreign minister's first work skipped, as at the start - in the order foreign, production,
intelligence, technology, politics (*inferred*: the order of the record's fields). **Events are not answered for the
player** whatever is handed over [exe].

**The slider automation** (`automate_sliders`) sets the player's own sliders **at the end of each day**, while production
is his and the level is above nought, by the script's `BalanceProductionSliders` with his level - 1 Prioritize Production,
2 Prioritize Upgrades, 3 Prioritize Reinforcement - and **his lend-lease share as it stands** where the AI's call is handed
nought [exe] (`ministers.md`, section 5). With production handed over the AI's call is made instead, at level 0.

### 4.4 In play: the screens' arrows

Read 2026-11-06 (`FINDINGS-frontend.md`, C.4). Each country screen's header has `automation_setting` between
`mode_arrow_left` and `mode_arrow_right` [mod: `country_*.gui`] - **the same record as the lobby's**: diplomacy,
production, politics and intelligence their flags, the text OPT_NORMAL "Normal" (where the lobby says "Player Control")
or OPT_AICONTROL; the technology screen the Technology setting, Normal, Ai Control, Research AI, Slider AI, **and its flag
set to whether the setting is above nought, which the lobby never does** [exe]. **The left arrow steps up and the right
one down, each wrapping** [exe]. Each press hands the area over or takes it back at once, its minister made or dropped
(`CSetAutomationCommand`). The arrows and the close button stay usable whatever the setting [exe].

**What the AI running a screen takes from the player** [exe]. In the original these controls **stop taking the mouse
and look exactly as before** - a click on them passes through; nothing is greyed. **Here they are also greyed**, the
sliders' bars and knobs among them, by a choice made here (`docs/compatibility.md`, entry 19):

| screen | taken |
| --- | --- |
| politics | the whole window |
| production | the whole window but `auto_deployment_checkbox` and `theatres_assign_button` |
| diplomacy | `decisions_listbox` and `diploaction_list` alone; the country list stays usable |
| intelligence | `own_priority`, `country_info`, `own_country_info` and every row's `priority` - but inside `country_info` the production queue and `country_info_sections`, whose Production and Ministers buttons switch what the panel shows, stay usable |
| technology | by the setting: **Ai Control** the screen and the leadership sliders, **Research AI** the screen, **Slider AI** the sliders; where the screen is greyed its folder buttons, the tree, the skills and the queue stay usable |

The theatre screen has no line. **The production screen's second selector**, `slider_automation_setting` between
`slider_arrow_left` and `slider_arrow_right`, is the slider automation: AUTOMATE_MANUAL "Manual Control",
AUTOMATE_PRODUCTION, AUTOMATE_UPGRADES, AUTOMATE_REINFORCEMENT - the left arrow up, the right down, wrapping, a level above
nought setting the sliders by it (`CSetSliderAutomationCommand`) [exe]; **while it is above Manual Control the sliders
are greyed** and cannot be moved, and **handing production to the AI puts it back to Manual Control** - both ours, the
original's sliders staying as they are and its level kept (`docs/compatibility.md`, entry 19). Its
**`auto_deployment_checkbox`** is the
seventh flag, the finished units placed every hour while it is ticked [exe]. **The sliders are not sent** while the AI runs
production, nor the technology screen's under Ai Control or Slider AI [exe]. `CAutomationText` in `Interface/Screens`
says the lines.

## Ours

- ~~**The Quick Start panel is not offered**~~ - offered since 2026-11-15, `simple` picking the first panel
  (`bookmarks.md`, section 4).
- ~~**Only the start's bookmark is offered**~~ - every historical bookmark since 2026-11-15 (`bookmarks.md`); **the Scenarios
  and Custom Games tabs are greyed**, each with a tooltip of ours saying why (`docs/compatibility.md`, entries 1 and 17).
- **With no bookmark read, the list is one row of the defines' start date**, where the original's would offer nothing to
  press.
- **The GamersGate store's button, "Buy Bonus Items", is greyed** with a tooltip of ours saying no store is opened (decided on 2026-11-06), **and the four links to the publisher's pages are hidden.**
- **`version_label`** says `OpenHOI3` where the original writes its version name, then in brackets the four letters of its
  file checksum, the original's for the same files - `OpenHOI3 (GSKH)` for Their Finest Hour (`checksum.md`, since
  2026-11-13).
- **The seed of a new game is the game's own**, where the original takes one from the clock at Play (`docs/compatibility.md`,
  entry 11's reasoning: a game is replayable from its seed).
- **The chat log has no lines of a save's transfer**, the original's "Savegame transfer started..." and the rest.
- **Choosing a save holds the frame while it loads** - two or three seconds - where the original's transfer runs over a
  few frames with a line in the chat.
- **The options are the front end's until Play**, which gives them to the game, where the original's press writes the game's
  and the player's record at once; nothing reads them before Play, so it comes to the same. **The chat's line is written at
  every press**, where the original's is *inferred* to be written for each choice then differing from the game's.
- **Arcade cannot be chosen**: its arrows are greyed, its supply from the capital not being built (`docs/compatibility.md`,
  entry 18).
- **A save's Armies level is kept at Play**, where the original's lobby is *inferred* to write it from the sixth flag and so
  lose it; nothing reads it in play.
- **A loaded save played as another country**, or with other options, has its ministers made or dropped by who now does
  each country's work, and its modifiers made again, at Play.
- **The screens' arrows and the deployment box post a command**, where the original writes the player's record directly -
  every change to the game here is a command (`CLAUDE.md`). It runs as soon as it is drained, which a paused game does too.
- **A slider automation raised above nought sets the sliders at the next hour**, where the original's arrow sets them at
  once: the script is the AI's to call, and it is called in the hour's production step so that a replay plays it back in
  its place (`docs/replay.md`).
- **What the AI holds is greyed** (`docs/compatibility.md`, entry 19), where the original's controls only stop taking the
  mouse. Greying is the element's own setting or, failing one, the nearest above it (`interface.md`, section 7), where the
  original hands its flag down to every element at the moment it is set - so a row made later in a greyed list is greyed
  here and an element a screen enables each frame stays greyed under a greyed window.

## Not built

The multiplayer menu, the tutorial list, the credits, the victory conditions window, the end-of-game screen, the title
theme, F11's screenshot.
