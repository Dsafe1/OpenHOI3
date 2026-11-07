# Tooltips

How the original shows a tooltip: the box, what is asked each frame, the delay before a
tooltip's second half, the lines between the halves, the wrapping, where the box goes and what
keeps it on screen. **What each screen's tooltips say is not here**: each is a reading of that
screen's own routine, and they come screen by screen after this.

Source: `reversing/findings/FINDINGS-tooltips.md`, read 2026-10-11. The maintainer set a province's
tooltip beside a screenshot of the original's on 2026-10-11 - "the style looks good" - and on it
the box is its text with the border all round, as built: the text starts 16 in from the art's
edge across and down, lines 15 apart, and ours is 2 pixels taller at the bottom, which was left.

## 1. The box

One text box shows every tooltip: the `ToolTip` `textBoxType` of `interface/core.gui` - Arial14,
`gfx/interface/tiles_dialog.tga` behind it, a border of 16, `maxWidth` 400. It is made once as a
game is entered and is hidden while there is nothing to show. `confirmed`.

*Ours:* how the original draws a text box's `textureFile` was not read. The box draws it as a
cornered tile whose corners are the box's border, its edges and middle repeated. The art is an even
panel inside a frame 6 pixels wide, so any corner from 6 to 48 draws the same.

## 2. A tooltip

A tooltip has three texts and a point:

- **`text`**, shown at once;
- **`delayed`**, shown once the mouse has been still (section 4), under a line;
- **`delayed_extra`**, shown with it, under a line of its own when anything comes before it - where
  a button's shortcut goes (section 6);
- **a point**, and whether the box **hangs below** it or **stands above** it.

## 3. Each frame

`confirmed`, in this order:

1. the tooltip under the mouse is asked for (sections 5 and 7);
2. when the mouse has been still (section 4) and `delayed` is not empty:

       text += "\n--------------\n" + delayed

3. when it has been still and `delayed_extra` is not empty:

       if text is not empty:  text += "\n--------------"
       text += "\n" + delayed_extra

   The line is fourteen dashes;
4. when that is the text shown already, the box is only placed again (step 8);
5. it is trimmed of newlines, tabs, carriage returns and spaces at both ends;
6. **empty hides the box**;
7. otherwise the text is broken into lines at most

       maxWidth - 3 * border

   wide - 352 with `core.gui`'s figures - and the box **grows to fit it**, with no limit on its
   height: the file's `maxHeight` of 256 is not applied. That the second figure is the border is
   `inferred`;
8. the box is placed (section 8).

## 4. The delay

The moment the mouse last moved is kept **truncated to a whole second** of a clock that counts
seconds from when the game was entered. The mouse counts as still once the clock is **more than one
second past that whole second**. So the second half of a tooltip comes **anywhere from just after
the mouse stops to one second after**, by where in its second the stop fell. `confirmed`; it reads
as an oversight, and is built so.

## 5. An element's tooltip

The element under the mouse is asked for its tooltip. In the original that is one routine for the
whole game, which asks the open windows in turn whether the element is theirs, then goes by the
element's name; each screen's answer is that screen's reading. **An element nothing answers for has
an empty tooltip** - there is no fallback to anything the interface files say (section 9).

**Which element is under the mouse** is the topmost one in the original, a text box as much as a
button. Here a text box lets the mouse through, so that a button under it keeps its clicks; a
screen that wants a text box to answer names it (`CGuiObject.PassiveTooltip`), and that text box
is asked first wherever the mouse is over it while the button keeps the clicks - the unit panel's
do (`unitpanel.md`, section 10). **A progress bar lets the mouse through too**, so a screen names
its bars the same way - a unit's `org_bar` and `str_bar` in the panel and in a battle's window -
and the test of whether the mouse is on one is made in the element's own turned box, the panel's
two bars being drawn turned on their side. *Ours*: how the original hands a click and a tooltip to two
elements that overlap was not read.

**Its point** is the element's corner on the screen, moved by the tooltip's own offset (nearly
always none). Then:

    if the box hangs below and the point is lower than 0.8 of the screen's height:
        it stands above the point instead
    otherwise:
        the point moves 64 down

So **a tooltip hangs 64 below the top of the element it belongs to, from its left edge** - or, for
an element in the lowest fifth of the screen, stands on its top. `confirmed` for the arithmetic;
that the corner is in screen coordinates is `inferred`.

## 6. A button's shortcut

When a button gives a shortcut in its file (`shortcut = "F2"`) and its tooltip's `delayed_extra` is
empty, `delayed_extra` becomes the localised `SHORTCUT` - "§WShortcut: §Y" - followed by the
shortcut as the file writes it. So every button with a shortcut has at least a delayed line saying
so, whether or not anything else answers for it. `confirmed` for the rule; that the string is the
file's own spelling is `inferred`.

## 7. The map's tooltip

With no element under the mouse and the mouse over the map, the tooltip is the province under
the mouse's own - one routine with a part for each map mode - and its point is **50 left of the minimap's upper left corner and 20 above it**, the box standing above it - wherever
on the map the mouse is. Nothing under the mouse gives an empty tooltip. `confirmed`: corrected on 2026-11-07
(`reversing/findings/FINDINGS-minimap.md`, section 8; `minimap.md`, section 6), where this said 50 left of the mouse
and 20 above it - the original's routine takes the minimap's corner, which the fact base had read as the camera's.

**What it says** was read whole on 2026-10-11 (`reversing/findings/FINDINGS-provincetooltip.md`)
and is built (`CProvinceTooltip`), all of it in `text`, in this order:

1. **A selected army's line**, between newlines - the first of these that holds, where
   `enemy` is the province's controller being the army's owner's enemy:
   - on land it may not stand in: `UTM_NO_INFRA` where its country may enter the province,
     else `UTM_NO_ACCESS`;
   - in a battle and may not retreat there: `UTM_NOT_LEGAL_RETREAT`;
   - enemy, and its strength against the most its brigades have had under its shatter
     threshold: `UTM_MAY_SHATTER`;
   - enemy, carried by no ship, no supplies received and some used: `UTM_LACK_OF_SUPPLIES`;
   - enemy and unable to fight: `UTM_TOO_WEAK_TO_FIGHT` - **except a headquarters, which is told
     nothing and is asked nothing more**;
   - enemy, using fuel and none received: `UTM_LACK_OF_FUEL`.
2. **The name**, and ` (` the owner's name `)` where it has an owner.
3. **The map mode's part**:
   - *terrain*: on land the flags, `" - ( @" + owner + ")"` where owner and controller are one,
     else `" - ( @" + controller + "/@" + owner + ")"` - **the controller first**; then the
     terrain's name, and its figures, each `key §Y figure §W` and a newline: movement cost
     always, attack, defence and attrition where not nothing;
   - *political*: on land the flags and the terrain's name; nothing more at sea;
   - *supply*: `supplymap.md`, section 3;
   - *air*: `airmap.md`, section 7;
   - *simplified terrain*: the terrain mode's;
   - *infrastructure, victory points, resources, region, diplomatic, intel, theatre, naval*: `mapmodes.md`, the mode's
     section; *strength* and *revolt risk* add nothing.
4. **What is selected**: an air unit's or a fleet's distance from its base and its range,
   `"\n" + distance + "km (" + range + "km)"`, by sea for a fleet; or a selected province's,
   `PROV_DISTANCE_FROM`.

`@` and a country's three letters in a text **draw its flag**, 23 by 11, from 3 below the top of
the line, and the pen moves on 24. `confirmed`. *Ours:* the original draws the flag out of an atlas
of all of them, which was not read; here it is the country's file in `gfx/flags` shrunk to that
size, and a tag nobody has draws nothing where the original draws the flag of the country numbered
1. **A text box draws them too**, and a unit's counter below: the original's diplomacy screen writes
`"@" + tag + name` into its relation list's text boxes (`diplomacyscreen.md`, section 5.3). Its text
renderer is one for both, inferred from that use.

`£` and the letters and underscores after it **draw a unit's counter**: the sprite `GFX_counter_` +
that name, of which only the middle - 0.26 to 0.75 across, 0.23 to 0.55 down, the unit's symbol
without its frame - is drawn, 30 by 16 from the top of the line, and the pen moves on 30. A name
with no sprite draws nothing and still moves the pen. `confirmed`
(`reversing/findings/FINDINGS-productiontooltip.md`, section 7). A build queue line's tooltip uses
it (`productionscreen.md`, section 8).

*Ours:* the original's tooltip also says the player's and its allies' objectives on the province, but
in the air and naval modes, which are not built here. A tooltip for every province is built,
where the original empties it for one the session does not simulate - none, in a campaign.

### 7.1 Over a counter

**Over a counter the map's tooltip is the unit's own** (`reversing/findings/FINDINGS-countertooltip.md`;
`CUnitCounterTooltip`), the one on top of the stack under the mouse, and stands where a province's does, above the
minimap's corner. Built 2026-11-07, read out of the original and tried with `--click`; the maintainer has not looked at it.

**Which units** [exe]: a fleet in port the fleets in that port, an air unit at its base the units based there, anything
else the units in its province - less land units at sea, and fleets in port under a hovered land unit. **Every country's,
with no test of who sees them and no cap** - so an enemy known little of makes the player's own units beside it `?` too.

**The head line**, where any unit is listed: `@` and each owner's tag and a space, in order of first appearance; then
under 5 of what the player knows of the hovered unit (`mapunits.md`, section 3) `?`, else the best leader's name, or
`NO_LEADER` [mod] with none - going down the list, a unit's leader replaces the one so far where his rank is at least as
high and his skill higher, so it is not simply the highest ranked.

**The body** [exe: the forms and the thresholds; mod: each type's `priority`]:

    under 3:   "? £unknown", and nothing more
    otherwise a line for each brigade type among the units, in the unit database's order:
      8 and 9:  N £type and a strength bar for each brigade, a space after each five
      6 and 7:  N £type, a space before a count of two figures and not before one
      3 to 5:   ? £type where the type's priority is 6 or more, else ? £unknown - a line each

`£` and a name draw the type's counter (section 7). **`{` and two digits draw a strength bar**, that frame of
`GFX_tooltip_strength` [mod: interface/tooltipicons.gfx] - eleven frames of 16 by 20 - the pen moving 8; a brigade's frame
is the tenths of its most strength, cut, and full from 99% [exe], its most with the reserves' penalty.

*Ours:* the units in the order of the countries and then of each one's units, where the original's is the order they
entered the province, so of two leaders of equal standing which one is named may differ; and a bar stands 3 above its
line's top, where the original's place against the line was not read.

## 8. Placing the box

`confirmed`:

    if it stands above the point:   y = point.y - its height
    if x + its width  > the screen's width:   x = the screen's width  - its width
    if y + its height > the screen's height:  y = the screen's height - its height

Nothing keeps it off the left or the top edge.

## 9. The interface files' own keys

Every element of a `.gui` file accepts `pdx_tooltip` and `pdx_tooltip_delayed`, and a button
`tooltip`, `tooltipText` and `delayedTooltipText`. The original looks a `pdx_tooltip` up in the
localisation as it reads the file, and a key it lacks is passed over **silently** - it builds the
complaint and throws it away. **Not one of these keys has a value in any file of Their Finest
Hour's or of BlackICE's**, so every tooltip a player sees is made by code. What the original does
with one when the mouse is over its element was not read.

*Ours:* read past, as before. A mod that sets one gets nothing here, which is what it gets until
that is read.

## Kept as read, though they look odd

1. **The delay is anywhere from nothing to a second** (section 4).
2. **Nothing keeps the box off the left or the top of the screen** (section 8).
3. **A headquarters too weak to fight is told nothing** on enemy ground, and its fuel is not
   asked about either (section 7).
4. **An occupied province's flags are the holder's and then the owner's**, though the owner's
   name is the one beside the province's (section 7).

## Not built

- **The console's `tooltips`**, which turns every tooltip off and on, and its tooltip debug,
  which adds a province's tag and number: there is no console.
- **What each screen's tooltips say.** The map's is built (section 7), the top bar's
  (`topbar.md`, section 14), the production screen's (`productionscreen.md`, section 8), the
  technology screen's (`technologyscreen.md`, section 9), the sliders' (`sliders.md`, section 6),
  the four builder windows' with the practical icon's (`builders.md`, section 11), the unit
  panel's (`unitpanel.md`, section 10), the deploy menu's (`mapunits.md`, section 10), the orders
  windows' (`fleetsandair.md`, section 8), a battle's (`combatview.md`, section 12) and the leader
  window's (`leaders.md`, section 13). Every screen built has its tooltips; a new one comes with
  its own.

## Not established

- ~~**What the minimap does to the map's tooltip.**~~ Answered 2026-11-07: the tooltip stands over the minimap's
  corner wherever the mouse is, which is why the maintainer's screenshot has it there (section 7; `minimap.md`).
- How the original breaks a tooltip's lines - at spaces, as a text box's own are broken here.
- How it draws a text box's texture (section 1).
- Where the original puts the box's text inside it; the box here is its text with the border
  all round.
