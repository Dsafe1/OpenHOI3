# The country screens: what the ones the top bar opens have in common

The production and technology screens, and the four the original has beside them -
diplomacy, politics, intelligence, theatres - are one kind of thing in the original, with
a base they share. This is that base, written as rules this project can implement. No
addresses and no offsets. Each rule says how sure it is - `confirmed`, `likely`,
`inferred` in the fact base's vocabulary. Where this project chooses its own behaviour
the paragraph is marked **Ours**.

Source: `reversing/findings/FINDINGS-countryviews.md`, section 1, read for this. **Nothing
in it was checked against a running game**, where the top bar's reading had the strings
out of one to agree with.

What each screen shows is `productionscreen.md` and `technologyscreen.md`; the sliders both
have are `sliders.md`; how an interface file becomes something on screen is
`interface.md`.

## 1. Two windows

`confirmed` A country screen is two top-level windows of the interface files, shown
together and in this order:

| | production | technology |
| --- | --- | --- |
| behind | `topcolor_production` | `topcolor_technology` |
| in front | `country_production` | `country_technology` |

The one in front is the screen: 1120 by 768, placed from the centre of the display, with
every element the code looks for. The one behind holds a single icon, `topcolor_icon`,
whose picture is a strip 64 wide and 512 high that fades downwards in the screen's own
colour.

**Ours:** that strip is drawn across the whole width of the display, as a haze under the
top bar. Drawn once where the file puts it, it is a stripe in the corner. How the original
spreads it was not read.

**Ours:** both windows are made, and the screen's elements found, as it is **first opened**,
not as the game is entered - the eight screens the top bar opens took a quarter of a second
of a game's start (`loadingscreen.md`, section 4). When the original makes its screens was
not read.

## 2. The picture follows the country

`confirmed` The icon `bg_countryskin` is given the picture
`gfx/interface/skins/country_bg_TAG.dds`, with the played country's tag, **if the install
has that file**. If it has not, the icon keeps what its sprite names - the generic one.
The install has such a file for many countries.

The top bar does the same with `topbar_TAG.dds` from the same folder, in the same step - read
2026-11-14 (`topbar.md`, section 1): as the original enters a game it asks each country screen
for its picture and then sets the bar's. **Nothing asks again**: the console's `tag` gives
none, and no other caller of a screen's picture was found (read 2026-11-15, `console.md`,
section 3), so after a `tag` the six screens keep the first country's - here too, by the
country the game was entered with (`CCountryView.SkinCountry`). The ledger is not one of the
six: its picture is made each time it opens, for the country played. *Ours:* a screen takes its
picture when it is first opened, where the original's are given as the game is entered.

## 3. The line that says who runs it

`confirmed` The text box `automation_setting` shows which of the player and the AI runs the
screen: the text of `OPT_NORMAL` or of `OPT_AICONTROL`, and on the technology screen also
`TECH_AUTOMATED` and `LEADERSHIP_AUTOMATED` for the two halves of it. The buttons
`mode_arrow_left` and `mode_arrow_right` change it. A screen the AI runs is greyed.

**Ours:** there is no AI, so the line always reads `OPT_NORMAL` and the arrows do nothing.

## 4. Up and down

`confirmed` `close_button` takes the screen down; the files give it the shortcut `ESCAPE`.
The screen fills its elements on every pass while it is up, and a box set to what it
already shows does nothing.

**One toggle opens and shuts every country screen** - the top bar's six buttons, a screen's own
`close_button`, the alerts, a message's Goto and the rest all call it. `confirmed` (read
2026-11-14, `reversing/findings/FINDINGS-countryviews.md`, section 7): every screen that is up
comes down; the side menus close - the deploy menu among them; then, where the screen asked for
was the one up, nothing more opens, and otherwise it comes up. **Either way the selection is
cleared last** - every selected unit and province let go, so the unit panels and the province
view close, as Escape lets them go. So only one screen is up at a time, and a screen never stands
over a selection. The Statistics screen, opened over a country screen, shuts it through the same
toggle, and so clears the selection then; opened over the map it leaves the selection alone
(`statistics.md`; `reversing/findings/FINDINGS-topscreens.md`, section B.1). Built so since
2026-11-14, on the maintainer's report that the selection stayed; until then a screen opened over
it and the deploy menu stayed open.

**A screen stands over an event's window and a message's popup**, as the maintainer saw the original do on
2026-10-14: they come up under it, over the map and the panels, and the top bar stands over all of them. Not read:
where the original keeps them, so the order of the rest is ours.

## 5. Ours

- **A screen is a class of its own in `src/Screens`, over a base, `CCountryView`**, as in
  the original. What each element says is worked out in `OpenHOI3.Interface`, with no
  engine in it, and tested.
- **An element the window does not have is skipped and named in the log**, except those
  looked up by a name made from the game's data - a folder's button, a building's - where
  having none is no fault.
- **`--screen=production` and `--screen=technology`** after `--` on the command line open
  a screen at once, for looking at it without a mouse.

## 6. The picture behind them

`menubar.gui`'s `bg_main_menus` (`GFX_bg_main_menus`) is up behind a country screen and the Statistics, and only them [exe]. It stands at the screen's corner, scaled by the larger of the screen's width over its width and the screen's height over its height, where that is above one, so a picture larger than the screen is cut off at the right and bottom [exe]. It covers the map and the bottom bar - the log, the map modes' buttons and the rest - and only the chat is put over it, the screen, the top bar and the alerts coming after [exe]; the map takes no click through it. The maintainer set this beside the original on 2026-11-09: the log and the map modes' buttons do not show there, and what stays over the picture is as it is here.

A country screen also takes down the minimap, `minimap_bg` and `minimap_overlay` while it is up [exe], as well; the Statistics do not.

`reversing/findings/FINDINGS-minimap.md`, *The picture behind the country screens*.

## What is not established, in one place

1. **What the top bar's buttons do**, and whether opening one screen closes another.
2. **How `topcolor_icon` is spread.**
3. **What the mode arrows do**, there being no AI to hand a screen to.
4. **The theatres screen.** Theatres shares the base and was not read; diplomacy was, on 2026-10-12, and is
   `diplomacyscreen.md`, politics on 2026-10-14 (`politics.md`, section 11), intelligence the same day (`espionagescreen.md`).
5. **Whether a screen stops the game or the map.** Here the clock runs on and the map
   under the screen takes no clicks.
