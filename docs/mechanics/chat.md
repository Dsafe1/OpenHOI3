# The chat in play

Read 2026-11-20 (`reversing/findings/FINDINGS-ingamechat.md`) and built 2026-11-21, asked for as "the missing chat".
**[exe]** hardcoded in the original's executable, **[mod: file]** what a file may change, *Ours* where the reading
gave nothing or the original's way is not followed. `CChatLog`, `CChatChannels`, `CChatText` in
`lib/OpenHOI3.Interface/Chat`; `CChatView` in `src/Screens`; the lines' travelling in `src/Multiplayer`.

## 1. Two chats, and which one is built

The original's files declare two chats for a game in play, and **the one in `menubar.gui` is not the one players
type into** [exe]:

- **`menubar.gui`'s `chat_window`** - four recipient buttons `none`, `allies`, `enemies`, `all`, a close button, the
  box `chat_message` - is built, wired and hidden as the game is entered, and nothing ever shows it. The rows of
  players its buttons would tick (`player_entry`, `short_player_entry`, `chat_shield_start` and its offsets) are
  named by no code. **Not built here**: there is nothing of it to see.
- **`menubar.gui`'s `chat_list`**, rows of `chat_entry` at the top left with a CHATMESSAGE message beside them, is
  live in the original, and carries what the game itself says in play: that a player lags, that one is lost. **Not
  built here** (section 6).
- **`chat.gui`'s `chat_window`** is the chat players use, and is what is built: sections 2 to 5.

Both windows are called `chat_window`; the first is a child of `menubar` and the second a type of its own, so the
two never meet [mod: both files].

## 2. The window

**Made only in a game between machines** [exe]: a game of one has no window, no button and no key.

- **Closed**, as it is made: `chat_inbox_window` shown, a window at the upper left with one button, `open`
  [mod: chat.gui]. The button shows its first frame, **and its second while a channel the player is in holds a line
  not read** [exe]. The original also blinks it between the two for an alert whose cause is not read; not built.
  **The alerts' row begins `alerticon_mp_offset` further on in a game between machines**, clear of the button
  [exe; mod: alerts.gui].
- **Opened** by a click on `open` or by **Tab** [exe] - here where no other box is being typed in, *ours*. Opening
  hides the inbox, reads the current channel and gives the box the keyboard.
- **Closed** by the `close` button, by **Tab** - the button's shortcut in the original's file, taken here by the
  view itself, a mod's other shortcut for it not being followed (*ours*) - or by **Escape** [exe]. Enter does not
  close it.
- It does not pause the game and blocks nothing but the keys typed into its box [exe, *inferred*].
- **`channels`**, a box of `channel_window` tabs: one for each channel the player is in, its `name` the channel's.
  The current one's name is yellow and one with lines not read green - *ours*, what the original's tabs show not
  being read. A click on `select` makes it the current one. A tab's `close` is hidden: it is for the channels
  players make, which are not built.
- **`chat`**, a list of `chat_item` rows: the current channel's lines, each wrapped to the row's `text` box, the
  list at its end [exe, *inferred*].
- **`users`**, a list of `user_item` rows: each player's `name` and, on `country_icon`, his country's flag [exe,
  *inferred*]. **`whisper`** puts `/whisper <name> ` in the box (*ours*: its click is not read); `kick` is hidden,
  as the tabs' close is.
- **`chat_input`**: Enter sends, section 4.

## 3. Channels and users

**Five channels** [exe: the five and their order; mod: their names]: CHAT_CHANNEL_GENERAL, CHAT_AXIS, CHAT_ALLIES,
CHAT_COMINTERN, CHAT_NEUTRAL.

- **Every player is in General.**
- **A player is in his country's faction's channel**: Axis, Allies or Comintern where the faction's tag is `axis`,
  `allies` or `comintern`, **and Neutral for no faction and for any other tag** [exe: the three tags] - a mod's own
  faction shares Neutral. It follows the country: looked at every frame [exe].
- **A player whose country changes faction leaves the old channel at once** - *ours*: the original was not seen
  taking him out of it (`docs/enhanced.md`).
- **Ctrl and Tab** makes the next channel the player is in the current one, **with Shift the one before** [exe].
- **The users are the players of the game**, the host first. Whenever they change, General says CHAT_USERJOINEDCHANNEL
  "$NAME$ joined channel" or CHAT_USERDISCONNECTED "$NAME$ disconnected" on every machine - the original's texts;
  *ours* when they are said, which is not read.

**Not built** [exe has them]: channels players make (`/create`, `/invite`, `/kick`, `/ban`, CHAT_BETWEEN_X_AND_Y).

## 4. A line

**Enter in `chat_input`** [exe, *inferred*]:

1. The text is trimmed; **an empty line does nothing**.
2. **A line beginning with `/` is a command.** Built: **`/whisper <name> <text>`**, a line to one player, and
   **`/reply <text>`**, a line to whoever last whispered to this player - with the original's errors
   CHAT_ERROR_WHISPER_MISSINGARG, CHAT_ERROR_UNKNOWNUSER, CHAT_ERROR_REPLY_MISSINGARG and
   CHAT_ERROR_REPLY_MISSINGUSER. A name is matched whole and without case, the longest first, since a name may hold
   a space (*ours*). **The other nine - `/slap`, `/me`, `/invite`, `/create`, `/ban`, `/kick`, `/roll`, `/?`,
   `/help` - are not built** and answer as an unknown one does, CHAT_ERROR_UNKNOWNSLASH: their bodies are not read.
   An error is said in the current channel on this machine alone.
3. Any other line goes to the current channel.
4. **The box is emptied and the window stays open.** The line is kept: **Up and Down** bring back the lines sent.

Not built: Tab completing a word, and `/reply ` put in an empty box by a key.

**A row** is the receiving machine's own clock, who, and the text. The original's pieces are `[HH:MM]`, CHAT_SYSTEM
"System" for the chat's own lines and CHAT_WHISPER_TO "To $NAME$" for a whisper sent; **their order and colours are
ours** (`CChatText`): `[12:34] ` then the name in yellow, a colon and the text. A whisper that came has a star after
its sender's name - *ours*, after the mark the original's dead window gives a private line. A whisper is shown in
whichever channel is the current one, having none of its own. A channel keeps its last 200 lines (*ours*).

**A line that comes into a channel that is not on screen, or while the window is closed, is not read** [exe], and
the inbox button and the channel's tab say so.

## 5. How a line travels

*Ours* throughout: the original's chat has commands of its own and a sync for a machine that joins, not read.

- A client sends its line to the host; **the host sends a channel's line to the machines of the players in that
  channel and to no other**, the sender's among them, who is shown his own line as it comes back. A faction
  channel's players are those whose countries are in a faction of that channel as the game stands on the host. A
  whisper goes to its one machine, and back to its sender as "To NAME". (The original's dead window would have sent
  a private line to every machine and had the receiver hide it.)
- The host tells every machine the users whenever they change, and a machine that joins as it joins.
- **The chat is no part of the game**: it is not in the recording, a save or the midnight's hash, and a line said
  while a game is being sent is shown when it comes.
- **A machine's chat outlives the loads that bring the machines into step** (`CNetGame.Chat`), the window open or
  closed as it was. A machine that joins sees nothing said before it came.

| the lines (`CWire.Chat`, `C`, in play) | |
| --- | --- |
| `c <channel> <text>`, from a client | its player's line to a channel |
| `w <name> <text>`, from a client | its player's line to one player |
| `p <channel> <name> <text>`, to a client | a player's line in a channel |
| `i <name> <text>`, to a client | a line that player sent this one alone |
| `o <name> <text>`, to a client | its own whisper come back, with the name it went to |
| `s <channel> <key> <name>`, to a client | a line the chat itself says |
| `u name=TAG&...`, to a client | the users, the host first |

## 6. Not built

- **`chat_list` and the CHATMESSAGE message**: in the original, rows at the top left - at most ten, the oldest
  gone every eleven seconds - and a message of that type for each line the game itself writes: LAG_PAUSE and
  LAG_DECREASE_SPEED for a client days behind, and the lost-player dialog's text. Here nothing lags by days - the
  host waits within three hours (`docs/multiplayer.md`, section 5) - and who is waited for is the line under the
  top bar.
- The nine other slash commands, the channels players make, completion, the alert's blink (sections 2 to 4).
- The lobby's chat is another thing: `mechanics/frontend.md`, section 9.

## 7. Checked

Tests: the channel by a faction's tag, the unread marks, Ctrl and Tab both ways, a faction left, a whisper and
`/reply`, a typed line's reading with each error, Up and Down, a channel's cap and a row's text.

By two processes, 2026-11-21 (`--say=text|text` on a client types those lines three days in; `--netlog` writes
each line that comes to the log): the client's line to General on the host and back on the client; a whisper each
way, each machine shown the right side of it; "joined channel" on the host as the client came; and on a screenshot
of the host the window with its two tabs, the lines, the two users with their flags and the box, and - closed -
the inbox button with the alerts' row beside it. **Not seen**: the window on a client, Tab and Escape (the clicks
of a check press no Tab), a faction channel between two players of one faction, and nothing was set beside the
original's own window.
