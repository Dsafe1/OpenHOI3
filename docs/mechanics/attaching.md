# Attaching, detaching and making a headquarters

*Spec for the unit panel's three kinds of button and the multi-unit panel's two: `reversing/findings/FINDINGS-attachbuttons.md`, `FINDINGS-selection.md` section B.4. Read out of the original, tried with `--click`, and the maintainer has not looked at them. Files: `single_unitpanel.gui`, `unitpanel.gui`. Figures are exe-hardcoded unless a define or a file is named.*

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

`detach_button` posts `CDetachUnitCommand` and nothing else. The unit goes to the top of its country's order of battle with what is under it, and the player is told `DETACHEDUNIT`. `detach_multiunit` posts one for every selected unit but a theatre, in selection order, **with no test that it has a commander**; the selection stays.

## 4. A new headquarters

The single button posts `CCreateHigherCommand` for the unit, and **the new headquarters is not selected**.

`highercomm_multiunit`: every selected unit but a theatre, or none where the highest ranked (first of equals) has a commander's commander who could not take it. Pressed: a brigade's men for **each** unit, or nothing; else the selection is cleared and one command posted, and the new headquarters is selected.

The command makes one headquarters one level above the highest ranked; `hq_brigade` at a twentieth of its strength; a twentieth of its men charged. Under the top unit's commander, or his commander where he is already at the new level. Each other unit that the headquarters may take goes under it too.

**An army group's** is a theatre: named by `NAMED_THEATHRE` after the best province of the theatre where it stands, or `THEATRE_NAME`, and a new theatre of the game's next number, holding its headquarters and nothing else.

## 5. What is the original's and looks wrong

A press asks for a whole brigade's men and charges a twentieth; a detach of several has no test of a commander; the order is the insertion's. *Ours:* the ids a player's headquarters takes are taken as the command runs, where the original reserves them as it is posted, so that a replay comes out the same. `edit_theatre` and `disbandbutton` are not built.
