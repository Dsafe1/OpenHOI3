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
| `onmap` | no | `<TYPE>_MAP` as text on the map (section 6), built 2026-11-21 |
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

### 4.1 The Goto

*From `reversing/findings/FINDINGS-messagegoto.md`.* What the Goto does is handed over by the place that raises the message, so it goes with the type (`CMessageGotos`) and no file can change it; the file says only whether the button is there. **A left click on a row of the log does the same**, and takes no notice of the answer.

| what it does | the popup afterwards | types |
| --- | --- | --- |
| centres the map on the message's province, else the acting capital of its first country, else of its second | stays | 119 types: every diplomatic, trade, aid and faction message, the battles over, `ANNEX`, `DISBAND`, `PROVINCELOST`, `REVOLT`, `PARTISANS`, the undergrounds' |
| shows the province's battle and centres on it, an air battle for the two `AIR` types | stays; closes with no battle | the six `ATTACK` types, `SHATTERED` |
| shows the province's bombing and centres on it | stays; closes with none | `BOMBTHEM`, `BOMBUS` |
| centres on the first country's acting capital | stays | `INDEPENDANCE`, `GIESWAPOTHER` |
| centres as the first row | stays; closes with nowhere to go | `INVADEDUS`, `PARATROOPEDUS` |
| closes the orders window and clears the selection, and then by type | stays | the 21 of `CMessageGotos.Screens` |
| clears the selection, selects the message's unit - by its number, whoever holds it now - and centres on it | closes, whatever it found | the three `UNITARRIVED` (`movement.md`, section 6, step 10), `EXPRETURNED`, `EXPRETURNEDWE` (`expeditions.md`, section 3) |
| nothing | closes | events, decisions, laws and ministers, triggered modifiers and the like |

The fourth-from-last row's "then by type": `DIPLOEXPIRE` selects the capital of the agreement's other side and opens the diplomacy screen on it; `TECH_ONCE` and `TECH_REPEAT` turn the technology screen on or off - **with it already up a Goto shuts it**, the original's; `BUILDING_DONE` centres as the first row; the others do nothing more.

Which province a message is about is `CPactMessage.Province`, set where it names one. `INVADEDUS` takes the first landing unit's province: *inferred*, the raise was not read for it.

**A message carries a unit by its number** (`CMessage.Unit`, `CPactMessage.Unit`), as the original's carries a persistent id, which its unit Goto looks up in the whole game: an expeditionary force returned is the lender's by then, and the host's player told `EXPRETURNEDWE` has it selected all the same (`reversing/findings/FINDINGS-expedition.md`, the addition of 2026-11-14). `PATROLTRACK` carries its patrol too, and its Goto is the province's (`navalmissions.md`, section 3.1). The three `UNITARRIVED` are raised since 2026-11-14, and `EXPRETURNED` and `EXPRETURNEDWE` carry their unit since the same day. `TECH_ONCE` and `TECH_REPEAT` are in the table and nothing raises them yet. `LEADERDIED`, `SHIPSUNK`, `MINISTER_DEATH`, `REINFORCE` and `RETURN_EXILE_US` are declared and raised by no code in the original either.

### 4.2 The settings of a type

A right click on a popup's background, its OK, Agree or Goto button, or on a row of the log, opens `MessageSettingWindow`
(`messagesettings.gui`) for that message's type: `title_text` is the type's `_SETUP` text, and four boxes are ticked from
the type - `log_button`, `popup_button`, `onmap_button`, `pause_button` (`pausepopup`). There is no box for `idle` [exe].
It stands at the mouse's x and 150 above its y, moved back only by what it passes the screen's right and bottom by [exe].
**OK writes the four into the type and the player's `messagetypes_custom.txt` at once**; Cancel closes. Each right click
opens another window. A change is to the type, so it holds for every later message of it.

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
- *Ours*: the list's rows are made once a frame, however many messages the frame brought (since 2026-11-21). Each
  message made every row again until then, and fifty units detached together stalled the game on fifty lists of a
  hundred rows - **measured**: 46 detached in BlackICE, 0.9 s in the making of rows with the log nearly empty. What
  the list ends as is the same.

## 6. On the map

[exe; `reversing/findings/FINDINGS-messageshow.md`, sections 5 and 5.1.] Built 2026-11-21 (`CAnimatedMapTextTypes`
in `Content/Map`, `CMapTextRise` and `CMapTextLayout` in `Interface/Map`, `CMapTexts` in `src/Map`), looked at once
with a text made by hand; not set beside the original. ~~No type of Their Finest Hour says `onmap = yes`~~ **56 types of Their Finest Hour's
`tfh/interface/messagetypes.txt` say `onmap = yes`**, and 57 of BlackICE's - `UNITDEPLOYED`, `DETACHEDUNIT`,
`ATTACHEDUNIT`, `SHATTERED`, `BUILDING_DONE`, the declarations of war and the rest (counted 2026-11-21; the sentence
struck through was written off the wrong file). The player's "Show on map" in a type's settings changes it type by type.

- **The text** is `<TYPE>_MAP` with the message's variables, over the middle of the message's province's bounding
  box - with no province, the first country's acting capital, else the second's. An empty text is still made.
- **Its type** is the `animatedmaptext` named `MessageMapText` of the interface's `.gfx` files, of which only
  `speed` (0.005 in `core.gfx`; 0.01 unsaid) and its `textblock`'s `color` (yellow) do anything [mod]; the block's
  `font`, `size`, `position` and `format` are read by the original and used by nothing. **The font is the
  bitmapfont named `AnimatedMapText`**, whatever the block says [exe the name; mod its `fontname`], drawn in the
  block's colour and not the font's own.
- **Its life is counted in frames** [exe]: each frame its time grows by the game's speed, nought to four, and one -
  the clock running or paused - its height is `speed x 2 x time`, and it is gone the frame its height passes 2:
  drawn 200 frames at the slowest speed, 100, 66, 50, and 40 at the fastest.
- **Drawn** [exe]: 3 world units up for each unit of height, so six in all; whole for three quarters of the rise,
  then fading evenly to nothing while it grows from its size to twice it; a font pixel is 0.05 world units, half a
  map pixel to the world unit, so it is sized in the world and grows with the zoom; it faces the screen and nothing
  hides it. Seen only with the camera under 500, and it ages unseen above that.
- **Laid out** [exe] by the font's draw in the world: cut to 99 characters; measured as lines broken at 320 font
  pixels - at each space, where the line with that word and its space is strictly wider - and then **drawn in one
  row**, starting half the widest line left of its point, a line's top 15 font pixels above it.
- **Several texts over one province overlap**; the original's queue that would space them is fed by nothing.
- Nothing else writes such a text: the other types `core.gfx` declares - `ProvinceTaxText`, `CombatLoss` - are named
  by nothing in the original.

**What looks wrong and is built so** (`docs/enhanced.md`): the life in frames, shorter on a faster machine and at a
higher game speed, running while paused; a long text measured as several lines and drawn as one, off to the right;
the text growing as it fades.

*Ours*: it rises straight up the screen, the camera's tilt not asked; a colour escape in the text is taken out, where
the original colours by it; and with no `MessageMapText` declared nothing is written, where the original does not
test for it.

## Ours

- **A type's own settings window is not built**, the one a right click on a popup or a log row opens.
  The in-game menu's panel and the player's file `messagetypes_custom.txt` are, since 2026-11-04,
  read at every start over `messagetypes.txt` and written by the panel's Close (`ingamemenu.md`,
  sections 4 and 5).
- ~~**No text on the map** (`onmap`)~~ - built 2026-11-21, section 6.
- A right click on a popup or a row - the original's way to its type's settings - does nothing.

## Not established

- How long a message's text stays on the map.
- ~~What colour `§b` is in the original's table for the popup's fonts; here it is black, as everywhere
  (`CTextColours`, `docs/compatibility.md`, entry 5).~~ The font's own `color_override`, else the shared
  `colorcodes` table, as everywhere since 2026-11-19 (`interface.md`, section 6): black in both games.
- Which popup draws on top of which.
- Whether the first post of the government is a placeholder, which the variables begin after.

## 9. A battle's message and its popup, and an election's

*From `reversing/findings/FINDINGS-smallbatch.md`, sections 2 and 5, and `FINDINGS-messageshow.md`, section 3.* `CMessages.CombatBegan`, `CMessagePopup`.

As a battle first runs, a player on either side is told: `ATTACKTHEM` or `ATTACKUS` with `NAVAL` or `AIR` before it by the kind, or for a bombing `BOMBTHEM` or `BOMBUS`. A convoy raid's battle says nothing, and a battle with nobody left on a side nothing either; a raid has three messages of its own, `CONVOY_TOO_STRONG`, `CONVOY_ATTACK_ON_US` and `WE_ATTACK_CONVOY` (`navalmissions.md`, section 8.2; `CMessages.ConvoyRaid`). Whether it pops up, goes on the map or the log is the type's in `messagetypes.txt`, where only `ATTACKUS` pops up by default.

The six battle types use `CombatStartPopup`. Under the six lines the divider and the two lists of names stand at the same height, a side's first six units, each to twenty letters and then `AND_MORE`; the two totals stand under them by the height of the right-hand names; and everything below goes under the totals by the right-hand total's height. **Goto** shows the province's battle - for an air type its air battle - and the popup stays; with no such battle it closes.

*Ours:* a bombing's `DEFUNIT` is the defenders' summary, without the list of what the mission bombs, which nothing writes yet. A click on a battle's line in the log does not go to it.

`ElectionResultPopup` carries `_HEADER` and `_1` with no colour changed; a pie of every party but the null one at no less than one percent, so that a party with nothing shows one and the slices can come to more than a hundred, its tooltip the party and its percent; and the new cabinet in two lists split as the politics screen's. OK stands 53 above the picture's foot.

### 9.1 A battle decided

*From `reversing/findings/FINDINGS-battleover.md`, read 2026-11-22.* `CBattleOverMessages.BattleOver`, raised by
`CGameState.CombatConcluded`. Everything here is the executable's [exe] but the texts, which are the localisation's
[mod: `unit_messages.csv`], and whether a type pops up, is logged or stands on the map [mod: `messagetypes.txt`].

As a battle on land, at sea or in the air is decided - once, after the relation a land battle changes and **before the
battle's on_actions** - the player is told `LANDBATTLEOVER`, `NAVALBATTLEOVER` or `AIRBATTLEOVER`: one message, only
where he is among the countries of one side and both sides have a country. A sea battle with an empty side says
nothing and still runs its on_actions (`events.md`, section 8).

Whose side he is on is asked of the attackers first; whether he *lost* is the attackers' result turned round where he
is among the defenders. After the standing values (section 3):

| | land | air | sea |
| --- | --- | --- | --- |
| `OUREXHAUSTION`, `ENEMYEXHAUSTION` | the constant `WE: §Y0.00`, both | - | - |
| `WINORLOSE` | the text of `YOU_WON` or `YOU_LOST` | same | same |
| `MESSENGER` | `MESSAGE_HEAD_MARS` | same | same |
| `NAME` | the text of `BATTLE_OF`, a space, the province's name | same | same |
| `USLOSS`, `THEIRLOST` | each side's losses, cut to a whole number | same | - |
| `USNUM`, `THEIRNUM` | each side's size: every type's tally cut to a whole number, added | same | - |
| `OTHER` | the other side's units' countries by name | same | same |
| `RESULT` | `RESULT_WON` or `RESULT_LOSS` | same | same |
| `OTHERRESULT` | the other of the two | same | `COMBAT_FLEE` where we won, `COMBAT_TRIUMPH` where we lost |
| `SHIPS` | - | - | our side's sunk ships and two line feeds |
| `THEIRSHIP` | - | - | the other side's sunk ships |
| `WINNER` | - | - | `COMBAT_WINNERTYPE_OUR` or `_THEIR` |

A side's sunk ships are the text of `NONE`, or for each ship a line feed, `@` and its owner's tag, a space, `£` and
its type's key, a space and its name - a flag, a counter and a name a line (`tooltips.md`). A side's size on land is
its men, a hundred to a point of strength of every brigade as it joined; in the air a whole one a wing.

The message is the battle's province's; its countries on land and in the air are the player and the defenders' first
country, whichever side he is on, and at sea the attacking side's - the player where he attacks, else the attackers'
first - and the defending side's the same way. Its Goto goes to the province (section 4.1).

*Kept as the original's and listed in `docs/enhanced.md`:* the two exhaustion values say the same constant whatever
happened.

*Ours:* the countries of a side are those still standing on it (`CCombatSide.Standing`), as the message of a battle's
start and the last battle's losses take them.

### 9.2 A bombing ended

*From `reversing/findings/FINDINGS-bombend.md`, read 2026-11-22.* `CBattleOverMessages.BombingEnded` and `Damage`,
raised by `CGameState.BombingEnded` as a bombing is taken off the list, its units still on it. All [exe] but the
texts and the type's settings, as above.

Nothing is said of a convoy raid, of a bombing whose bombers have no country or no unit left, or whose first unit has
no order. The player among the bombers' countries is told `BOMBENDTHEM`; failing that, among the bombed side's,
`BOMBENDUS`; anyone else nothing. **It is told whatever was hit.** After the standing values: `PROV` the province's
name, `ATTACKER` the bombers' first country's name and `ATTACKER_ADJ` its adjective, `ORDER` the name of the first
bomber unit's order, and `DAMAGE` and `DAMAGE_SHORT`, which are the same text. Both of the message's countries are the
bombers' first, on the bombed player's message too; its Goto goes to the province.

The damage, a line each, each ending in a line feed:

1. every building in the building file's order of which something was destroyed: `BOMB_DAMAGE_BUILDING` with `NAME`
   its name and `SIZE` the levels to three places, cut;
2. each good - supplies, fuel, money, crude oil, metal, energy, rare materials - of which something was taken from the
   stockpile: `BOMB_DAMAGE_STOCKPILE`, `NAME` the good's name (its `*_IRO` text) and `SIZE` as above;
3. each good, in that order, of which production was destroyed: `BOMB_DAMAGE_PRODUCTION`.

Then one last line with no line feed, by the first bomber unit's order and by *d*, the strength the bombs dealt times
10 over 1000, cut - so under a tenth of a point it is nought:

- ground attack or interdiction and *d* not nought: `BOMB_DAMAGE_MEN`, `SIZE` *d* written as men (`builders.md`), which
  comes to **the strength dealt in whole points** - 46.039 dealt reads 46;
- naval strike, port strike or carrier protection: `BOMB_SINK_SHIP` with `SUNK` the ships sunk, as 9.1 writes them,
  where any were; else `BOMB_DAMAGING_SHIPS` where *d* is not nought;
- any other order, nothing.

A text of under three characters is the text of `NONE`.

*Kept as the original's and listed in `docs/enhanced.md`:* `DAMAGE_SHORT` is the whole text again - the original makes a
copy for it and hands over the long one.

*Ours:* the ships a bombing sinks are kept on the bombed side here and read from there (`bombing.md`); the original
keeps them with the bombers. The original writes *d* back over the strength dealt before its strategic-warfare tally
reads it; here the tally scales its own share (`strategicwarfare.md`) and nothing is written back.

## 10. A unit put on the map

*From `reversing/findings/FINDINGS-messages.md`, "A unit put on the map", read 2026-11-21.* `CDeployment`.

**The player is told `UNITDEPLOYED` of every unit of his put on the map** [exe]: one he places, and one that joins a
unit in the field - a finished upgrade's brigade joining the unit its line was ordered for among them
(`upgrades.md`, section 4), told before the join's own `TRANSFEREDSUBUNIT`. Nothing else is asked: not the unit's
kind, not who placed it. Nobody is told of another country's.

- `UNIT` the unit's name and `LOCATION` the province's, after the country's own variables [exe];
- the message's province is the one the unit is put in, so its Goto goes there and its text is written over it;
  the first country is the owner and there is no second [exe];
- the type logs it and writes it on the map and does not pop up, in Their Finest Hour's file and BlackICE's [mod:
  `interface/messagetypes.txt`]; its `_MAP` text is "$UNIT$ deployed." [mod: the localisation].

Not raised until 2026-11-21, when a deployment was seen to write nothing on the map.
