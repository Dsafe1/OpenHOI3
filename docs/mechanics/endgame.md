# The end of a game, and the windows of the victory conditions

How a game ends and what shows how near it is. Read whole in `reversing/findings/FINDINGS-endgame.md` (2026-11-08); the rules of the conditions themselves are `factions.md`, sections 8 and 9.
`Interface/Screens/CEndGameText` and `CVictoryWindowText` say what is shown; `src/FrontEnd/CEndGameScreen`, `src/Screens/CVictoryConditionsView` and `src/FrontEnd/CVictorySelectWindow` show it. Read from the original and tried with `--click`; they have not been reviewed in play.
**[exe]** a rule the executable holds; **[mod: file]** what a file may change; *Ours* where this departs.

## 1. The dialog

`VCConfirm` [mod: `interface/eu3dialog.gui`]. It names a faction itself [exe]: of the game's factions in `common/faction_aims.txt`'s order, the one with most of its chosen conditions met, **the last on a tie**, so that with nothing met it is the last of all. The title is `CONFIRM_VC_VICTORY` where that is the player's own faction and otherwise `CONFIRM_VC_DEFEAT`; a country in no faction always sees Defeat. The text is `CONFIRM_VC_TEXT` with the faction's name. Resign is the menu's Resign; Continue only closes it.

## 2. The ways a game ends

Checked each frame (`CSession.CheckTheGamesEnd`).

- **The victory conditions.** The day's test finishing the game raises the dialog and pauses, once. Continue leaves the game paused, and the player unpauses and plays on, the game marked finished and asked no more [exe].
- **The end date.** Once the clock is past the defines' `end_date` by an hour - strictly after - the same dialog comes up, in the same words, whoever has achieved what. **The game is held paused every frame from then on, so that it cannot be played past the end date**; the only way on is Resign [exe].
- **A player with no provinces** goes straight to the end-of-game screen, no dialog, unless the country is REB [exe].

The dialog comes once in a session, by any of those; a save does not keep that, so a load can show it again.

## 3. The end-of-game screen

`back_end` [mod: `interface/backend.gui`], reached by Resign, by the dialog's or the menu's. **The game stands still behind it** until Exit, and it is filled once as it comes up, from the world as it then stands: it does not ask who won before.

Over a black backdrop (*Ours*: the original's device is cleared; the screen's picture does not reach the edges of a wide canvas): the player's shield and name, the result, and for each of `allies`, `axis` and `comintern`, in that order and by those tags [exe]:

| element | shows |
| --- | --- |
| `<f>_icon` | the faction's `icon` as a frame |
| `<f>_label` | its name |
| `<f>_members` | a row `backend_faction_member_entry` with a `flag` for each member |
| `<f>_goals` | a row `end_vc_entry` for each chosen condition: `name`, `date` only where met as "year Mon day", `background` frame 2 where met and 1 where not |
| `<f>_vc` | `END_VICTORY_CONDITIONS`, met of chosen |
| `<f>_vp` | `END_VICTORY_POINTS`, its progress in whole points |
| `<f>_progress` | a bar, its points x 100 over the three factions' sum, cut; the bare points x 100 where they have none between them |

`factions` is declared and nothing fills it, and `loser_background` stands under everything, untouched [exe]. **The screen has no tooltips.**

**The result** [exe], for each faction with `met` the count of its chosen conditions met today and `vp` its points, against the other two:

    if met >= to_win and met is strictly the most:  tier 0 if vp > the other two's sum, else tier 1
    else if vp is strictly the most:                tier 2 if vp > the other two's sum, else tier 3 if met is strictly the most

The first tier of 0, 1, 2, 3 reached wins; its faction's `<f>_won` picture shows. Tier 0 says the faction's ideology group's name and `WORLD_ORDER`, whoever is playing; the others `STRATEGIC_`, `OPERATIONAL_` and `TACTICAL_` with `VICTORY` for the player's own faction and `DEFEAT` for another; nobody reaching one is `STALEMATE`, no picture. A resign on the first day of 1936 is a Stalemate.

So the dialog's "Victory!" and the screen's result can disagree, and a condition lost before the end stops counting: as read.

Exit puts the front end back. *Not built*: the music - `victorymarchverdi.mp3` for the player's own winner and `diesiraeintro.mp3` otherwise - there being no sound here.

## 4. Resign

Goes through section 3 since 2026-11-08, where it had gone straight to the main menu.

## 5. The window in play

`vc_window` [mod: `interface/vc_interface.gui`], opened by `menubar_goals_button` beside the minimap, tooltip `VC_BUTTON_TOOLTIP`, and shut by its `close_button` or the button again; **the game runs on under it** [exe]. It and the Strategic Effects window shut each other.

Each frame the picture of the player's faction is shown by its exact tag - `vc_bg_axis`, `vc_bg_allies`, `vc_bg_com` - and the rows made where the faction has changed. For any other faction no picture and no list, and the file's own `vc_nofaction_label` is left to show. Once a day: for each of the three, `vc_<f>_progress_text`, met and a slash and the number chosen (15, where 12 win), and `vc_<f>_progress`.

The rows `vc_entry` are the player's faction's chosen conditions. `vc_entry_bg` is frame 2 where met and 1 where not, and nothing else changes; the date is not shown. Any element of a row answers with the condition's trigger, written out with no country in scope (`scripttext.md`).

## 6. Alert 14

Up while a faction but the player's has met at least three quarters of what wins - 9 of 12 - asked every three seconds with the other alerts (`alerts.md`). The tooltip is `VC_ALERT`, and its delayed half `VC_DANGER` with the faction and its count, **each faction writing over the last, so that only the last is named** [exe]. A click opens the window where it is shut and does nothing where it is open.

The alerts stand in the order they came up, the first at `alerticon_startposition` and each after it an `alerticon_offset` on (`mapunits.md`, section 5).

## 7. The lobby's window

`victory_conditions_window` [mod: `interface/victory_conditions.gui`], from `victoryconditions_button`, which is enabled only with no save loaded and a country that is **its faction's first member** [exe] (the scenarios' "or anyone" has no use here), and while greyed says `VC_BUTTON_DISABLED_SAVE` or `VC_BUTTON_DISABLED_INVALID`. Play cannot be pressed while it is up.

A row `condition_entry` for every condition of the faction in the file's order, `active` ticked where chosen, `name`, and the trigger as its tooltip; the header `VC_FACTION_HEADER`. A click toggles the box, **with no limit** [exe]; `info` is ticked and the number, the ticked in red where they differ. `accept_button` is never greyed, its tooltip `VC_ACCEPT_DISABLED` only while they differ. Accept fits the ticks to the number - too many lose some at random, too few take random others of the faction's - and the window goes; Cancel only hides it, and the next opening ticks from what is chosen.

*Ours*: the lobby's world has not yet made a new game's own choice, so the window makes it first, and a faction chosen here is left alone by the one a new game makes; the draw is a generator of the moment as the original's is, the world keeping the names it gave; and the ticks' order is not kept - the conditions stand in the file's, where the original's list has random additions at its end.

## 8. A text box measured from the centre

`CENTER` text boxes have an arithmetic of their own [exe] (`interface.md`, section 6; `reversing/findings/FINDINGS-textdraw.md`): set centred, a line starts at `border + box/2 − widest/2 − line/2` from the box's left, so that a single line ends at the middle of the box and is not centred in it. Set left or right it starts at the border. The screen's lines stand in their columns by this.
