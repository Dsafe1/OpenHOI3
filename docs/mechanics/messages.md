# Messages

What the player is told when something happens: a popup, a line in the log, text on the map, or
nothing, by the message's type. Built first for the messages an answered event and a taken decision
raise; every other part of the game that raises one raises it through the same machinery when it is
built.

Sources: `reversing/findings/FINDINGS-messageshow.md` (a type's settings, the post, the popup, the log,
the variables, read 2026-10-12), `reversing/findings/FINDINGS-messages.md` (the registry and how a
message is raised) and `reversing/findings/FINDINGS-eventfire.md`, sections 1.4 and 2 (the messages
of an event answered and a decision taken). Nothing here was set beside a running game.

## 1. A type

`interface/messagetypes.txt` declares the types by name, each with seven keys:

| key | default | when a message of the type is posted |
| --- | --- | --- |
| `log` | **yes** | a line in the log (section 5) |
| `popup` | no | a popup (section 4) |
| `pausepopup` | no | a popup **too** - without `popup` - and in a game of one player the clock stops |
| `onmap` | no | `<TYPE>_MAP` as text on the map (section 6) |
| `option` | no | the popup's buttons: one, or two with a "Goto" |
| `idle` | no | **nothing**: read, and read by nothing |
| `categories` | `cat_other` | the log's six filters: `cat_combat`, `cat_diplomacy`, `cat_units`, `cat_province`, `cat_events`, `cat_other` - matched exactly, anything else being `cat_other` |

**A type declared twice is the second, whole.** **A type nobody declared is not an error**: it is the
`NULL_MESSAGE` type, which logs a line and nothing else - its key `NULL_MESSAGE_LOG`, which no table
has, so the line is that key. *Stricter*: a type no file declares is reported, as is a key a type does
not take.

A type's text is in the localisation by its name: `_HEADER` and `_1` to `_6` for the popup, `_LOG`,
`_MAP`, and `_BTN1` to `_BTN3` for the buttons. **A text that is exactly `*` is empty**, everywhere.

## 2. Who is told

**A message is raised only for the player**: every place that raises one asks first whether it is
about the player's country, and raises nothing otherwise - with one exception that matters here, the
events'. Posting it does not ask again.

**An event answered** - whoever answered it, when its option runs and **before its effects** - raises
one message:

| | type |
| --- | --- |
| the event's scope's own country is the player's | `EVENTHAPPENUS` |
| otherwise, an event of at most one option | `MAJOREVENTHAPPENOTHER` where `major`, else `EVENTHAPPENOTHER` |
| otherwise | `MAJOREVENTHAPPENOTHEROPTION` where `major`, else `EVENTHAPPENOTHEROPTION` |

A province's event has no country of its own in its scope, so **its owner is told `...OTHER`**, never
`US`. **A decision taken**, before its effects:

| | type |
| --- | --- |
| the scope's country is the player's | `DECISIONUS`, or `PROVDECISIONUS` with a province |
| otherwise, with a province | `PROVDECISIONOTHER` |
| otherwise | `MAJORDECISIONOTHER` where `major`, else `DECISIONOTHER` |

Their Finest Hour declares neither `PROV...` type, so those are logged as `NULL_MESSAGE_LOG`. **That is
all `major` does.** As Their Finest Hour ships them: `EVENTHAPPENUS` is logged; the two `EVENTHAPPENOTHER`
nothing; the two `MAJOREVENT...` logged and popped up; `DECISIONUS` popped up; `DECISIONOTHER` nothing;
`MAJORDECISIONOTHER` logged and popped up.

## 3. The variables

A message carries named values, and **each value is itself looked up as a key** - so a value that is
some key's name shows that key's text, and any other shows as itself. An event's and a decision's
message has, in order:

1. for each post of the **player's** government, the post's key - `$foreign_minister$` - as
   MINISTER_MESSAGE_ENTRY ("$TITLE$ $NAME$"), the post's title and its minister's name - which is how a
   header reads "Foreign Minister Ribbentrop reports on";
2. `COUNTRY` - the scope's country's name, or for a province's scope its owner's, or the province's;
3. an event's `EVENT`, `EVENTDESC` and `OPTION` - its title, description and the option chosen, each
   through the event's own text (`events.md`, section 11); a decision's `EVENT` and `DESC` - its
   `_title` and `_desc` - and `PROVINCENAME` where its scope has a province.

## 4. The popup

`DefaultPopup` from `interface/eu3dialog.gui`, one for each message, **none queued** - each stands on its
own, at the window's place and then **10 to the right and 10 down for each before it, and 40 down**, the
count going back to nought after ten, so they cascade over eleven places. A place whose window would
cross the screen's right or bottom edge is moved to the screen's size less 10.

- **Header**: `_HEADER` with its variables, its **first** `§W` turned to `§b`.
- **Line1 to Line6**: `_1` to `_6`, **every** `§W` turned to `§b`. **An empty line is left out**, and the
  rest stand one under the other from under Header - Line2 15 lower than where it would be, the others
  where they would - each the height of its text.
- **The background** is made as high as the last line's bottom and **120** more, and the buttons stand
  **70 above the bottom**.
- **Buttons**: with `option = no`, `CenterOK` alone, saying `_BTN1`, closing; with `option = yes`,
  `AgreeButton` saying `_BTN2` and closing, and `DeclineButton` saying `_BTN3`, the "Goto", **which
  closes only where what it goes to answers false** - an event's and a decision's go to nothing, and
  close. **Escape is the Goto** of a popup with two buttons.
- **The flags**: `LeftShield` shows the message's first country's flag and `RightShield` its second's;
  with one of them, both show it, and with neither the file's stays.
- A popup's window may be dragged (`interface.md`, section 7).

## 5. The log

**The message log** is `messagelog_window` in the `menubar` window of `interface/menubar.gui`: a list
box, `messagelog`, a close button and the button that opens it again, and six category checkboxes.

- **A line** is the message's **date** - " 13:00, 1 January, 1936", a space first - a space, and `_LOG`
  with its variables. It is wrapped to the `logtext` row's button less 10, and **each piece is a row of
  its own**; rows go at the end, the newest at the bottom.
- **200 messages are kept**, the oldest dropped; **the list is cut to 100 rows by one row for each
  message added**, so messages of several rows let it grow past 100.
- **A ticked category box hides its category**: its messages are kept and given no rows. Ticking or
  clearing one makes the list again from the oldest message kept, **stopping after 101** - so with more
  than that shown, the newest are left out until the next is added.
- A left click on a row of an event's or a decision's message does nothing.

## Ours

- **A type's own settings window is not built**, the one a right click on a popup or a log row opens.
  The in-game menu's panel and the player's file `messagetypes_custom.txt` are, since 2026-11-04,
  read at every start over `messagetypes.txt` and written by the panel's Close (`ingamemenu.md`,
  sections 4 and 5).
- **No text on the map** (`onmap`), and no other message than an event's and a decision's yet.
- A right click on a popup or a row - the original's way to its type's settings - does nothing.

## Not established

- How long a message's text stays on the map.
- What colour `§b` is in the original's table for the popup's fonts; here it is black, as everywhere
  (`CTextColours`, `docs/compatibility.md`, entry 5).
- Which popup draws on top of which.
- Whether the first post of the government is a placeholder, which the variables begin after.

## 9. A battle's message and its popup, and an election's

*From `reversing/findings/FINDINGS-smallbatch.md`, sections 2 and 5, and `FINDINGS-messageshow.md`, section 3.* `CMessages.CombatBegan`, `CMessagePopup`.

As a battle first runs, a player on either side is told: `ATTACKTHEM` or `ATTACKUS` with `NAVAL` or `AIR` before it by the kind, or for a bombing `BOMBTHEM` or `BOMBUS`. A convoy raid says nothing, and a battle with nobody left on a side nothing either. Whether it pops up, goes on the map or the log is the type's in `messagetypes.txt`, where only `ATTACKUS` pops up by default.

The six battle types use `CombatStartPopup`. Under the six lines the divider and the two lists of names stand at the same height, a side's first six units, each to twenty letters and then `AND_MORE`; the two totals stand under them by the height of the right-hand names; and everything below goes under the totals by the right-hand total's height. **Goto** shows the province's battle - for an air type its air battle - and the popup stays; with no such battle it closes.

*Ours:* a bombing's `DEFUNIT` is the defenders' summary, without the list of what the mission bombs, which nothing writes yet. A click on a battle's line in the log does not go to it.

`ElectionResultPopup` carries `_HEADER` and `_1` with no colour changed; a pie of every party but the null one at no less than one percent, so that a party with nothing shows one and the slices can come to more than a hundred, its tooltip the party and its percent; and the new cabinet in two lists split as the politics screen's. OK stands 53 above the picture's foot.
