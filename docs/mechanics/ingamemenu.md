# The in-game menu

`menubar.gui`'s window `menu` - a picture and `menu_panel` with six buttons, Save Game, Game Options, Resign, Message
Settings, Quit and Close - with two windows of its own, `menu_save` and `menu_message_settings`, and the dialog Resign and
Quit open. Read out of the original on 2026-11-04 (`reversing/findings/FINDINGS-ingamemenu.md`; the message types
themselves `reversing/findings/FINDINGS-messageshow.md`, and `messages.md` here), and set against the maintainer's
screenshot of the original's menu, which it agrees with. `confirmed` unless marked.

**[exe]** marks a rule the original's executable holds, **[mod: ...]** what a file may change.

## 1. Opening it

Three ways, all the same opening [exe]:

- **Escape** over the map, where nothing takes it first: **with anything selected the first Escape only lets the
  selection go** (`provinceview.md`, section 1), so with units selected it takes two presses. With the menu up, Escape is
  its Close's shortcut (section 3).
- **`topbar_menubutton`** on the top bar [mod: topbar.gui]: a press opens the menu. The code would shut it on a second,
  which the menu's hold on the screen never lets through (below).
- *The custom game bar's `menupause`*, which opens it the same way with a custom game's extra step. **There is no custom
  game here** (`CLAUDE.md`, *One canonical start*).

Opening it, in order:

1. it notes whether the clock was **already stopped**;
2. **where it was not, and the game is of one player, it stops the clock** - a game of several players is never paused
   by it. Every game here is of one player;
3. the window is shown, with **`menu_panel` on top, always** - never the save dialog or the settings it was left on;
4. the side menus are shut and **the selection is let go**, units and provinces, and with it the unit panels;
5. an open country view - production, technology, diplomacy, politics, intelligence - is shut.

**While it is up, nothing behind it takes a click or a key** - the maintainer's account of the original, 2026-11-04:
"The original blocks clicks and button presses to the background (so 'enter' doesnt close message popups)". So the map,
the panels and the top bar cannot be pressed, and no button behind the menu answers its shortcut; the menu's own
buttons and the dialog it opens do. Following from it: **the top bar's menu button cannot shut the menu** it opened, a
second press never reaching it, and Escape is the menu's own - its Close's shortcut, and where Close is not shown the
original's Escape, which closes the menu (section 2). *Ours*: the clock's keys - Space, plus and minus - and the map's,
which are not buttons, are held too; whether the original's are was not said.

**No button is greyed but Save Game, and that only in a custom game** - none here. **No button has a tooltip**, nor
`topbar_menubutton`: their `.gui` entries are empty and nothing in the executable answers for them [exe].

## 2. Closing it

**Close** (`menu_close_button`, shortcut `ESCAPE` [mod: menubar.gui]) runs the close, which asks whether either of the
menu's own windows - the save dialog, the message settings - or the game options screen is up:

- **if one is**, the menu is hidden and opened again as in section 1 - **back on the six buttons**, not on the map;
- **if none is**, the clock is started again only where it is stopped and the menu stopped it - that is, where it was
  running when the menu opened - and the menu is hidden.

**Looks wrong and is the original's** - confirmed by the maintainer in the original on 2026-11-04: going back to the six
buttons opens the menu again, and that opening notes the clock as stopped, which it is, by the menu. **So once Save
Game, Game Options or Message Settings has been opened, closing the menu leaves the game paused.**

**Opening a battle's window closes the menu** [exe], as Close does.

## 3. Resign and Quit

Both open one dialog, the original's `CQuitDialog` on the window `DefaultDialog` [mod: eu3dialog.gui]: the title is the
key with `_TITLE`, the text the key with `_DESC`, and **both shields are the player's country**. The menu stays up
behind it, the clock stopped.

| button | key | title | text |
| --- | --- | --- | --- |
| `menu_quit_button` | `QUIT` | "Quit Game" | "Are you sure you want to quit and return to desktop?" |
| `menu_resign_button` | `RESIGN` | "Resign Game" | "Are you sure you want to resign and return to the main menu?" |

The text is `frontend.csv`'s [mod: localisation]. **OK** (`AgreeButton`, `ENTER`): Quit ends the program, to the
desktop; Resign ends the game into the end-of-game screen of `backend.gui`, whose Exit puts the world back to its start
and brings up a new front end on the main menu (`reversing/findings/FINDINGS-frontend.md`, A.6). **Neither saves first**
[exe]. **Cancel** (`DeclineButton`, `ESCAPE`)
only shuts the dialog.

## 4. The message settings (`menu_message_settings`)

`menu_message_button` shows the window, hides its other windows and fills the list; the window is the menu's own and
pauses nothing more.

**The list, `settings`**: a `message_entry` row for **every** message type, in the order the types were first declared -
`interface/messagetypes.txt`'s, then any the player's file alone declares (section 5) [mod: messagetypes.txt]. A row's
`text` is `<TYPE>_SETUP` [mod: localisation]; its button `message` carries `*`, which renders empty. **A click on a type's
name does nothing**: the original's handler for it is attached to no element [exe].

**The four boxes** - **ticked where the setting is off**, the sprite's second frame, red; unticked where it is on, the
first, green [mod: the .gfx, the .dds]:

| box | the type's | in play |
| --- | --- | --- |
| `messageicon_pause` | `pausepopup` | a popup, and the clock stopped |
| `messageicon_log` | `log` | a line in the log |
| `messageicon_dialog` | `popup` | a popup |
| `messageicon_map` | `onmap` | the map's text - not built here (`messages.md`, *Ours*) |

A click on any of a row's boxes writes **all four** of that row's boxes into the type, at once, for every later message
of it [exe]. Nothing is written to the disk until Close.

**The seven tabs** are checkboxes, `messagecat_all` and the six of the categories. **None is ticked when the window first
opens.** A click that ticks one unticks the other six - radio buttons - and lists the types of its category, `all`
listing every one (`kind == all or type.category == kind`). A click that unticks the ticked one, where none is left
ticked, lists every type. **The fill ignores the tabs** [exe]: opened again, the window lists every type with the old tab
still ticked, until a tab is clicked.

**`reset`** ("Reset  defaults") deletes the player's file and reads `interface/messagetypes.txt` again over the types
[exe] - **and nothing else: the list is not filled again**, so its rows show the old settings until a tab is clicked or
the window opened again, and a click on a box of such a row writes its old four back into its type. A type only the
player's file declared keeps what it had.

**`close`** writes the player's file (section 5) and goes back to the six buttons (section 2). **It has no shortcut**, so
*inferred*: Escape there goes past it, to the game screen's own Escape, which closes the menu back to its buttons **without writing
the file**.

**Tooltips** [exe]: a tab is `MESSAGECAT_COMBAT` "Combat Messages", `_DIPLOMACY`, `_UNITS`, `_PROVS`, `_EVENTS`,
`_OTHERS`; **`messagecat_all` has none** - the original's code meant for it asks for `messagecat_others` a second time,
so "All Messages" never shows. A box is `MESSAGE_IS_LOG`, `_POPUP`, `_ONMAP` or `_PAUSE`, "This message type $TRUTH$
currently displayed ...", with `$TRUTH$` `MESSAGE_SETTING_TRUE` "is" where the setting is on and `MESSAGE_SETTING_FALSE`
"is not" where it is off - and **the pause box's says what the log box's does**, testing `log` and not `pausepopup`. The
rows' text, `message`, `reset` and `close` have none.

## 5. The player's file, `messagetypes_custom.txt`

In the player's folder - `Documents\Paradox Interactive\Hearts of Iron III\` for the base game, the folder settings.txt
is read from (`settings.md`); a mod's is its own, and mods are not played yet [exe].

- **Read at every start**, after `interface/messagetypes.txt` and in its grammar, so the player's file wins for every type
  it names. A type declared again is **overwritten whole, in place**: it keeps its place in the order, and what is not
  written takes the defaults. A type only the file declares is added at the end.
- **Written** by the settings window's Close, whole: every type in order, each as

      NAME=
      {
      log=yes
      onmap=yes
      popup=yes
      idle=no
      option=yes
      pausepopup=no
      categories="cat_diplomacy"
      }

  in Windows-1252, every line ended by a carriage return and a line feed, `idle` and `option` written as they are though
  the window has no box for them. The maintainer's file is the original's and reads so.
- **Looks wrong and is the original's**: once the file exists, **a mod's later change to a type's settings never reaches
  that player** until Reset - only types new to the mod take the mod's.

The file is the original's own, so a choice made here is the original's next time too, and the other way round.

## 6. The save dialog (`menu_save`)

**Built 2026-11-05** (`CInGameMenu`, `Content/Saves/CSaveFiles`; the save itself `savegame.md`, section 11), with
typing into `game_name` as the original's edit box takes it (`interface.md`, section 8). Opened, it hides the menu's
other windows, makes its list and its offered name again from nothing and **gives `game_name` the keyboard**, the
caret at the name's end [exe; where the caret goes, *Ours*]. A key typed there goes no further - into no shortcut and
not to the map - and Enter does not save: `save` has no shortcut.

- It lists `save games/*.hoi3` in the player's folder [exe] - and the install's `tfh` and base folders, PhysFS's search
  path (*inferred*, `FINDINGS-ingamemenu.md`, C.2), a name found twice the first folder's - **newest first** by the file's time in whole seconds - the
  original's sort is not stable, so files of one second come in no fixed order - leaving out folders and **any name
  with a byte above 127**. The autosaves are listed like any other. A row, `save_game_entry`'s checkbox `game`, shows the
  name **cut at its first `.hoi3`**; a click puts that into `game_name` and unticks the other rows. Nothing is saved or
  loaded by it.
- **The name offered**: the player's country's name, **its first space alone** made `_` [mod: localisation], then
  `%d_%02d_%02d_%02d` of the year, month, day and hour - `Germany1936_01_01_00`, `United_Kingdom1936_01_01_00` [exe]. A
  game of several players puts `mp_` in front.
- **`save`**: an empty box does nothing - the original trims the name and throws the trimmed one away, so **a name of
  blanks is saved**; `.hoi3` is added unless the name's last dot, after its first character, starts exactly `.hoi3` -
  `x.HOI3` gives `x.HOI3.hoi3`; no character is refused. Where a file of that name exists - asked over the install's
  folders as well - `DefaultConfirm` asks `CONFIRMSAVETITLE` "Save Game", `CONFIRMSAVETEXT` "Are you sure you wish to
  overwrite the file $NAME$?", `NAME` with its `.hoi3` [mod: misc.csv]; OK saves over it, Cancel shuts the question
  alone. Otherwise it saves at once. **No message says it was saved.** Then back to the six buttons.
- **`cancel`** (`ESCAPE`) goes back to the six buttons, not to the map.
- No tooltips, and nothing greys `save` or `cancel`.

## What looks wrong and is built so

1. Having opened any of the menu's windows, closing the menu leaves the game paused (section 2).
2. A click on a type's name in the message settings does nothing (section 4).
3. No tab is ticked at first, the fill ignores the tabs, and Reset leaves the rows as they were (section 4).
4. `messagecat_all` has no tooltip, and the pause box's tooltip is the log box's (section 4).
5. Escape in the message settings leaves without writing the file (*inferred*, section 4).

## Ours

- ~~**Resign is greyed**~~ - **Resign goes through the end-of-game screen since 2026-11-08** (`endgame.md`), to the main menu since 2026-11-06 (`frontend.md`). **Game Options was greyed too, and opens the game options window since
  2026-11-04** (`gameoptions.md`), **and Save Game, which opens the save dialog since 2026-11-05** (section 6).
- Saves of one second are listed by name, where the original's sort leaves them in no fixed order; and the `save
  games` folder is made where it is missing (`savegame.md`, *Ours*).
- The keys that are not a button's - the clock's and the map's - are held while the menu is up (section 1).

## Not established

- Whether a checkbox the original's interface has ticked can always be unticked by a click - so whether clicking the
  ticked tab unticks it. Here it does.
- Whether the interface marks a shortcut's key as taken, so that Escape on Close does not reach the map as well -
  *inferred* that it does, and here it does.
