# Attaching, detaching and making a headquarters

*Spec for the unit panel's three kinds of button and the multi-unit panel's two: `reversing/findings/FINDINGS-attachbuttons.md`, `FINDINGS-selection.md` section B.4. Read out of the original, tried with `--click`, and they have not been reviewed in play. Files: `single_unitpanel.gui`, `unitpanel.gui`. Figures are exe-hardcoded unless a define or a file is named.*

`CAttachText` (`Interface/Screens`), `CAttachmentPanel`, `CUnitButtonsEntry`, `CMultiUnitPanel` (`src/Screens`), `CAttachUnitCommand`, `CDetachUnitCommand`, `CCreateHigherCommand` (`Sim/Commands`). The shortcuts are the file's: attach `a`, detach `d`, a create button `c`.

## 1. When they may be pressed

A unit with no commander shows `attach_button`, enabled where it is not all carrier air groups and some unit of its country may take it. A unit with one shows `detach_button`, hidden for all carrier air groups.

One of `create_corps`, `create_army`, `create_armygroup`, `create_theatre` shows by the unit's level - a division's, a corps', an army's, an army group's. It may be pressed unless the unit's commander is exactly one level up, and only where the country has a **whole** `hq_brigade`'s `build_cost_manpower` (a unit file's), though a twentieth is charged.

## 2. The window of commanders

`attach_button` makes `attachment_selection_panel` anew each press, so a second press rebuilds it and does not shut it. It stands at the file's place and pauses nothing. `back_button` hides it, as a row's press does; it goes for good when the panel it came from is rebuilt or put away.

**Listed:** every unit of the country that may take this one, by the same test as dragging in the browser; no limit of distance or province. **In this order,** each as made taken and inserted from the tail: walk back while the row there sorts after the new one, and go after where it stopped.

    sorts after (a, b):   subject land:  a's rank lower-numbered than b's -> yes
                          otherwise:     a's rank higher-numbered than b's -> yes
                          then, either way: a further from the subject than b -> yes

So land is shown lowest ranks and nearest first, and anything else highest ranks and nearest first, roughly: the second test runs even where the ranks say the other way, so **it is not a sort, and is built as written**.

A row: `name`; `location`, the province, ` (`, the distance whole, `km)`, not localised; `counter_type` the first brigade's where its sprite exists; `counter_size` by level; `unit_pride_star` for a first brigade that is a pride. `assign` posts `CAttachUnitCommand` and hides the window. Neither window nor row has a tooltip. The command raises `ATTACHEDUNIT` for the player where the unit has a name, and that also goes for one made by the new-unit button only once it is named.

## 3. Detaching

`detach_button` posts `CDetachUnitCommand` and nothing else. The unit goes to the top of its country's order of battle with what is under it, and the player is told `DETACHEDUNIT`. **Both this message and `ATTACHEDUNIT` are about the province the unit stands in** [exe] (`reversing/findings/FINDINGS-attachbuttons.md`, section 5): their Goto goes there and their text on the map is written over it - over the capital until 2026-11-21, the province not having been given. `detach_multiunit` posts one for every selected unit but a theatre, in selection order, **with no test that it has a commander**; the selection stays.

## 4. A new headquarters

The single button posts `CCreateHigherCommand` for the unit, and **the new headquarters is not selected**.

`highercomm_multiunit`: every selected unit but a theatre, or none where the highest ranked (first of equals) has a commander's commander who could not take it. Pressed: a brigade's men for **each** unit, or nothing; else the selection is cleared and one command posted, and the new headquarters is selected.

The command makes one headquarters one level above the highest ranked; `hq_brigade` at a twentieth of its strength; a twentieth of its men charged. Under the top unit's commander, or his commander where he is already at the new level. Each other unit that the headquarters may take goes under it too.

**An army group's** is a theatre: named by `NAMED_THEATHRE` after the best province of the theatre where it stands, or `THEATRE_NAME`, and a new theatre of the game's next number, holding its headquarters and nothing else.

## 6. Disbanding

*From `reversing/findings/FINDINGS-smallbatch.md`, section 1, and `FINDINGS-selection.md`, section B.4.*

`disbandbutton` may be pressed unless the unit is a land unit aboard a fleet or a plane, a theatre headquarters whose theatre holds provinces, run by an AI agent of its own, or in a battle. It raises `DefaultConfirm` with `CONFIRM_DISBAND_TITLE` and `CONFIRM_DISBAND_DESC`, which goes by itself when the unit does and not when the selection changes. Ok posts `CDisbandCommand`.

`disband_multiunit` raises `DISBAND_ALL_TITLE` and `DISBAND_ALL_DESC` with the number selected, and goes by itself if that number changes. Ok disbands every unit but those aboard, a theatre holding land or one with an agent - a unit in a battle is taken here - and clears the selection first. A unit on loan is left too in the original; nothing is lent here yet.

## 5. What is the original's and looks wrong

A press asks for a whole brigade's men and charges a twentieth; a detach of several has no test of a commander; the order is the insertion's. *Ours:* the ids a player's headquarters takes are taken as the command runs, where the original reserves them as it is posted, so that a replay comes out the same.

## 7. The theatre editor: `edit_theatre`

`reversing/findings/FINDINGS-attachbuttons.md`, section 9, `confirmed` but where marked. A theatre headquarters' button, which
paints provinces into its theatre on the map; it opens no window.

- **Shown** on a theatre headquarters alone and **never greyed**. Its tooltip is `UW_EDIT_THEATRE` with `UW_EDIT_THEATRE_DRO`
  delayed and the shortcut line, `t` by the `.gui`.
- **A press turns it on and off by the button's own picture**: up, the editor opens for the selected headquarters and the
  button shows its second frame; down, it closes and the first frame comes back. The press posts nothing.
- **Open, it is the original's map mode 17**: the map in the political mode's style, 2, whatever mode was up, and its own
  colours - every land province grey, 0.5, or 0.25 where the player knows it below 2; each of the player's theatres, the
  k-th of its list, in the map's k-th region's colour at full or half brightness by the same test, or the plain grey where
  there is no k-th region. The minimap's buttons are left as they were. The colours are made as the editor opens, and the
  box paints over them.
- **Painting is a left drag, and only a drag** - a press and a move of 2 map units or more. While the drag lasts, the box is
  gathered again whenever the pointer has moved **more than 2 whole map units** either way: every province the player
  controls, not already in the theatre and in an area, whose label point - its bounding box's centre - lies in the box grown by
  2 on every side, joins it, painted in the theatre's colour; one that leaves the box is taken out and painted back. **At the
  release** what is under the box goes to the theatre of the selected unit - the theatre heading its chain - as one
  `CUpdateTheatreCommand`, and the gathering is emptied; the paint stays until the editor is opened again. Nothing is selected.
- **A plain click is the ordinary click**, and so is a right click - the headquarters marches to the province.
- **It closes** by the button, or by itself once the selection is no longer exactly one theatre headquarters: a click on
  anything else, Escape, or a box of units. Closing gives the theatre whatever is still gathered.
- **The command** takes each province in order off the theatre it is stamped with, where that exists, puts it on this
  theatre's provinces and stamps it; then refreshes the owner's theatres **without placing or dropping** - no province
  without a theatre placed, none it no longer holds dropped - and the rest as ever: stamps, areas, fronts, an agent told to go
  from a theatre left empty, the theatres looked after. It checks nothing of the sender, the owner or the controller and asks
  only that the list is not empty; a theatre gone takes nothing. An edit persists: the played country's refreshes place only
  provinces with no theatre (*inferred*, `theatres.md`, section 8).
- **The Ctrl+R way in is dead in the original** - typed, it opens the editor for one frame - and is not built.

Checked with `--click` on 2026-11-13: Germany's `Ob.kdo. des Heeres` opened the editor over the terrain mode - its two theatres
blue and red, the rest grey - and a box over central Germany's blue provinces gathered 33 and gave them to its red theatre;
an hour later, the editor closed and opened again, they were red from their stamps.

*Ours:* the box gathers the provinces in the order of their numbers, where the original walks a list of the country's that
was not read, and its first sample is where the press was, the original's start of it not read; the box is drawn as a
selection box always, where the original draws it in the theatre's colour brightened by 1.1, or (0.8, 0, 0) for a theatre past
the regions; the sea keeps its own colour; and a minimap button pressed while the editor is open changes the mode under it
while the editor's colours and style stand, where the original's map mode would be the button's.

## 8. How many units a command holds

*The original's rule from `reversing/findings/FINDINGS-attachbuttons.md`, section 3; the enhanced game's is ours, after BiceLib's as the mod's `docs/bicelib/features.md` describes it. `CCommandLimit` (`Sim/Units`), asked by `CDeployment.MayTakeSubordinate`.*

**The original** [exe]: a land unit goes under a commander of a higher rank, and under an army group, an army or a corps only while that one has fewer than **five** land units under it. A theatre takes any number. The test is asked wherever a unit is offered a commander - the attach button and its window, a drag in the order of battle browser, a finished unit joining one in the field, a new headquarters taking the other selected units, a split's new units - and **the attach command itself does not ask it**. A faithful game's rule, whatever its files say.

**An enhanced game** takes the five from the mod's data:

    limit(commander) = max(1, whole part of (base + the country's modifier of the kind + its technology's))

    level        base, defines.lua's military section     kind of modifier
    corps        OPENHOI_CORPS_UNIT_LIMIT                 corps_unit_limit
    army         OPENHOI_ARMY_UNIT_LIMIT                  army_unit_limit
    army group   OPENHOI_ARMY_GROUP_UNIT_LIMIT            army_group_unit_limit

- **A base the file does not give is five** [mod: `defines.lua`], so an enhanced mod that says nothing plays the original's rule.
- **The country is the commander's.** Its modifier is everything section 4 of `modifiers.md` adds - a triggered modifier, a law, a minister, a strategic resource - and its technology is each technology's figure a level times its level [mod: any file a modifier is written in]. The two are summed before the whole part is taken, as a division's most brigades are.
- **Never less than one**: a commander with nothing under it always takes a unit, as BiceLib's does.
- **`OPENHOI_LONE_BRIGADE_TAKES_NO_PLACE`** at one or more [mod: `defines.lua`]: a division of exactly one brigade may always be put under a commander, and those already under it are not counted. A headquarters' one brigade is no division's.
- **Nothing is detached when a limit falls**, and nothing is kept for the rule: it is worked out as it is asked, from the defines and the country's modifier, which a save and the hash already hold.
- **The three kinds are kinds of an enhanced game only** (`modifiers.md`, section 2): a faithful folder's file that names one has it reported and dropped, as the original drops it. They are written as whole figures better for more, under `OPENHOI_MODIFIER_CORPS_UNIT_LIMIT` and its like; a technology's tooltip says its figure under `OPENHOI_CORPS_UNIT_LIMIT_TECH` and its like. The words are the mod's.

**The AI's ladder of commands asks none of this** (`agents.md`, section 4): it keeps a command at four of a kind and takes a fifth, by its own count, in both games - the original's code, which BiceLib leaves alone too. So under BlackICE's base of four an AI's corps may hold five. Whether it should follow the limit is not decided (`docs/enhanced.md`).

*Not built:* a leader's trait that changes the limit, which BiceLib has and BlackICE leaves off. Checked with `OpenHOI3.SaveCheck commandlimits` on 2026-11-26: BlackICE's bases four each, its twelve triggered modifiers read, and Germany given the technologies of BiceLib's table at their levels comes to that table's figures - a corps 8, an army 7, an army group 6. Not reviewed in play.
