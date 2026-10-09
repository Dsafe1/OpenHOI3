# The unit panel

What the window of a selected unit shows. Read out of the original on 2026-10-09:
`reversing/findings/FINDINGS-unitpanel.md`. `confirmed` means read; *ours* means decided
here. It was built from the reading and checked with real clicks. **The maintainer set it
beside the original on 2026-10-09**, one infantry division in both, and what that
screenshot showed is mended and marked *measured* below: where the bars stand, that the
entries touch, and where the name sits. The rest of the layout agreed with it.

**In one line.** The panel is one list. For the unit selected it is filled with a fixed
run of small windows - the units above it, a header, a status, an order, a strip of
buttons, its brigades, the units under it - and every one of them is written again each
frame from the unit as it then is.

**Only for one unit, and best for a land unit.** Selecting several at once is not built,
and a fleet's and an air unit's own lines wait for those to move.

---

## 1. The list

- The window is `single_unitpanel` of `single_unitpanel.gui`, and all it holds that
  matters is the list box `list`. Each thing in the list is a window of its own, from that
  file or from `unitpanel.gui`. `confirmed`
- **The list is one of two in the game whose entries keep their own heights**
  (`interface.md`, section 2): each goes straight under the one before and touches it.
  *Measured*: on the screenshot the border lines of the two rows of units above, the
  header, the brigades' heading and its three rows fall on the same pixel rows in both,
  eleven lines over 290 rows, with nothing between entries - the list's `spacing` of 2
  plays no part. Two of the entries - the brigades and the units under it - are as high
  as their rows, so what is under them moves when they change.
- **The run of entries**, in this order: `confirmed`

  | | entry | window | when |
  | --- | --- | --- | --- |
  | 1 | a unit above, the highest first | `sup_parent_unit_entry` | one for each |
  | 2 | the header | `unitpanel` | always |
  | 3 | the status | `sup_unit_status` | always |
  | 4 | the order | `sup_current_orders` | always |
  | 5 | a headquarters' row for an AI | `sup_objectives` | every unit above a division |
  | | three stances, power and assets | `sup_unit_stance` and four more | a headquarters an AI commands - **not built** |
  | 6 | the buttons | `sup_buttons_window` | always |
  | 7 | the brigades | `sup_subunits` | always |
  | 8 | the units under it | `sup_children` | when there are any |
  | 9 | what it carries, and each battle it is in | `sup_loaded`, `combat_entry` | **not built** |

- An entry finds its elements by their names once, when it is made, and writes them every
  frame the panel is up. `confirmed`. Here the list is made again when the units above the
  selected one change or it gains or loses its first subordinate, and otherwise only
  written; the original makes it again on events that were not read. *Ours.*
- **The panel is up while a unit is selected** and comes down with the selection. It
  stands where the deploy menu does and gives way to it. *Ours:* which of the two the
  original shows was not read.
- The window's own `oob_button`, the tab down the screen's left edge, opens the order of
  battle browser. Not built: it is shown and cannot be pressed.

## 2. The units above

One row for each unit above the selected one, the highest first. A row is one button,
`unit`, with the unit's name on it, set once. **A click selects that unit and takes the map
to where it is.** `confirmed`

The original writes the name in green for a unit an AI commands. Not built: there is no AI.

## 3. The header

The window `unitpanel`. The original uses the same window, by the same code, for each row
of the list of several selected units. `confirmed`

| element | what it is given | |
| --- | --- | --- |
| `unitname` | the unit's name. An edit box: in the original the name is changed here | shown, not to be typed in |
| `counter_type` | the sprite `GFX_counter_` + the type of the unit's first brigade | built |
| `counter_size` | the mark of its rank, as on a line of the deploy menu (`mapunits.md`, section 6) | built |
| `unit_activity` | what it is doing, below | built in part |
| `unitstrength` | its men, below | built |
| `unitleader` | its leader's name, in red where he has not the rank; `NO_LEADER` with none | always `NO_LEADER` |
| `leader_photo`, `leader_types`, `leader_button` | the leader's picture, his kind, and the button that changes him | as the file has them; the button cannot be pressed |
| `org_bar`, `str_bar` | two bars, below | built |
| `exp_force` | the flag of whose expeditionary force it is | hidden |
| `remove_unit_from_selection_button` | takes the unit out of the selection | built |
| `only_unit_from_selection_button` | disabled and hidden when one unit is selected | hidden |

**The activity strip** has six frames - nothing, a white arrow, a red star, a red arrow, a
green arrow, a red cross - and the first of these that holds decides: `confirmed`

    in a battle                  the red arrow attacking, the red star defending
    retreating                   the white arrow
    an attack delay running      the red cross
    on the march                 the green arrow
    nothing to act with          the red cross     (no brigades; or a land unit aboard aeroplanes)
    otherwise                    nothing

Built: the green arrow, the cross for a unit with no brigades, and nothing.

**The strength is men**: `confirmed`

    men = the sum over the brigades of  strength x 100        (each brigade cut to a whole man)

written whole under 1000 and with a comma before the last three digits above: `8,997`. The
original writes it in red where the unit's strength is under the share at which it
shatters; shattering is not built. A fleet's and an air unit's figure was not read, and
theirs is empty here.

**The two bars** are whole hundredths, cut: `confirmed`

    organisation = 100 x the brigades' organisation, averaged / the most
    strength     = 100 x the brigades' strength, added up / their types' max_strength, added up

    the most = the brigades' types' default_organisation, averaged
               x (1 + the country's land_organisation)        (naval_, air_ by its kind)

- Each divisor is raised to its dividend where it is the smaller, so a bar is never past
  full.
- **The header's organisation bar does not know about officers.** A brigade's own bar in
  the list below is drawn against what it may have, which the officer ratio decides
  (`organisation.md`); the header's is drawn against the plain figure. A division of a
  country short of officers shows a header bar that is not full over brigade bars that are.
- The original works both out in floating point. *Ours:* in whole numbers, which can
  differ by one hundredth of a bar where the quotient is within rounding of a whole one.
- A bar is declared lying down and **turned a quarter by the file's `rotation`**, so it
  stands on its end and fills upwards (`interface.md`, section 7).

## 4. The status

The window `sup_unit_status`. `confirmed` for what each element is given:

| element | what it is given | |
| --- | --- | --- |
| `unitlocation` | the name of the province it is in | built |
| `unit_location_button` | takes the map there | built |
| `unitattrition` | what the province wears it down by, whole percent; `?%` with no brigades | `0%`: attrition is not built |
| `unitbase` | `SUPPLIED_FROM`, a space, and the capital its province is supplied from; ` ???` where no friend holds that. A fleet or an air unit: its base, then `(`, its range and `km)` - or ` ???` with no base | a land unit: its country's capital. A fleet and an air unit: built; the range is written whole, *ours* |
| `speed` | below | built |
| `unit_cw_strip` | a land unit's width: frame 1 under 2, then 2, 3, 4 for under 4, 6, 8, and 5 beyond | built |
| `unit_weight` | a land unit's weight aboard ship, whole | built |
| `supply_status`, `fuel_status` | two bars, below | full |
| `unitstatus_moving` | shown on the march | built |
| `unitstatus_distance` | a land unit's reach of its headquarters: frame 1 in reach, 2 out of it | always frame 1 |
| `unitstatus_combinedarms`, `_dugin`, `_delay`, `_nuke` | shown for a combined arms bonus, a unit dug in, an attack delay, a nuclear order | hidden |

- A unit's width and its weight are its brigades' `combat_width` and `transport_weight`
  added up.
- **The speed is not what an hour adds.** It is the top speed times everything that scales
  it (`movement.md`, section 5), without the define that turns it into an hour's march:
  infantry on dry plains shows `4`, and walks 0.2 an hour. Whole when it is a whole number,
  to two decimals when not, and the text `KPH` straight after it: `4KPH`, `2.25KPH`.
- **The supply and fuel bars are the province's, not the unit's**:

      days = SUPPLYPOOL_DAYS, whole, at least 1
      bar  = what lies in the province / (days x what the units in it need a day)

  and full where they need nothing. Here a province does not keep what its units need -
  the supply network is not built (`upkeep.md`, section 2) - so both bars are full.
  *Ours*, until then.

## 5. The order

The window `sup_current_orders`: a line of text, `orders`, and a button,
`cancel_order_button`.

**What the line says**, the first of these that holds: `confirmed` but for the three about
a battle, which are `inferred`

    an order that describes itself, or a unit that is not a land unit     the order's own text
    retreating, with a path             RETREAT_TO_OI      "Retreating to $WHERE$."
    a path, and in a battle             ATTACKING_TO_OI    "Attacking $WHERE$."
    in a battle, attacking              ATTACKING_TO_OI
    in a battle                         DEFENDING_OI       "Defending."
    a path                              MOVE_ORDER_D       "Move to $WHERE$."
    too few men left                    RECOVERING_OI      "Currently Reinforcing"
    got no fuel and burns some          NEED_FUEL_OI       "Waiting for Fuel"
    got no supplies and uses some       NEED_SUPPLIES_OI   "Waiting for Supplies"
    otherwise                           the order's own text

- **`WHERE` is the next province of the path, not its end.** A unit sent five provinces
  away reads "Move to" the first of them, and then the second.
- Built: `MOVE_ORDER_D`, and for everything else `NO_ORDERS`, "No Order". *Ours:* what the
  original calls a unit with nothing to do was not read; that key is in its files.
- **An order's own text names the province the order is for**, its end and not its next
  step (`fleetsandair.md`, section 5): `confirmed`

      a move                       MOVE_ORDER_D with WHERE          "Move to Königsberg."
      a fleet's change of base     the order's name, " :", the province    "Rebase :Königsberg"
      an air unit's                ORDER_AIR_REBASE_SHORT_DESC with PROV   "Rebase(Königsberg)"

  *Ours:* a fleet sent straight somewhere has no order, and reads as a land unit on the
  march does - what the original's empty order says of it was not read.
- **Where the way ends and when the unit will be there is a tooltip** in the original, over
  the header's activity strip: "Will arrive in $PROV$ on $DATE$" - again the next province.
  Tooltips are not built, so the readout beside the panel says both for now, for the whole
  way.

**The button**: a land unit on the march that is not retreating is halted, by the same
command a click on its own province gives (`movement.md`, section 1). `confirmed`. The
original also uses it to take back an order of another kind.

## 6. The brigades

The window `sup_subunits`: a heading, and a list of one row for each brigade in the unit's
own order. `confirmed`

- `header`: for a land unit `LAND_COUNT`, "$NUM$ Brigades". A fleet's is `NAVAL_COUNT` with
  its capital ships, screens and others, and an air unit's `AIR_COUNT` with its fighters and
  bombers. Built - but which ship counts as which was not read. *Ours:* a capital ship is
  one its type calls `capital`; the others are submarines (`is_sub`) and anything that
  carries (`transport_capability`); the rest are screens; a bomber is a wing its type calls
  `is_bomber`.
- `positioning`: empty but for a fleet.
- **The window is as high as its rows**: its label's height and its rows', up to the
  height the file gives the window - five rows in the base game - and the list scrolls
  past that.

A row is the window `subunit_entry`:

| element | what it is given | |
| --- | --- | --- |
| `counter_type` | the sprite `GFX_counter_` + its type | built |
| `counter_size` | the lowest mark | built |
| `subunit_name` | its name | built |
| `subunit_type` | its type's name | built |
| `subunit_level` | which model of its type it is, as a Roman numeral, I for the first | built, see below |
| `subunit_amount` | its men: its strength x 100, as the builders write a strength (`builders.md`) | built |
| `unit_experience` | its stars, below | built |
| `unit_cw_strip` | its width, as in section 4 | built |
| `org_bar` | 100 x its organisation / the most it may have, officers and all (`organisation.md`) | built |
| `str_bar` | 100 x its strength / its type's `max_strength` | built |
| `unitstatus_reserve` | shown for a reserve | built |
| `unit_pride_star` | a star for a pride | built |
| `upgrade_button` | shown where its type may be upgraded to another | hidden |

- **The stars**: none under an experience of 1; otherwise the frame is two more than the
  experience over ten, rounded to the nearest whole and to the even one from a half, and
  no higher than the eleventh. The strip goes up half a star a frame, so an experience of
  1 shows half a star, 45 two and a half, and 90 or more all five.
  The original adds a define to a pride's experience first; not built.
- **The model** is the one the brigade was made as. *Ours:* the original works out each
  time which model the brigade's technology levels fit best.
- A row for an upgrade in progress, which the original adds under the brigades, is not
  built.
- A click on a row selects the brigade in the original. Not built.

## 7. The buttons

The window `sup_buttons_window`.

- **Three checkboxes are the unit's own, and work**: `prio_checkbox` - it comes before the
  others for reinforcement and for upgrades - `reinf_checkbox` and `upgrade_checkbox`,
  each set from the unit every frame (`upkeep.md`). `confirmed`. A click posts
  `CSetUnitSupportCommand(country, unit, which, on)`. *Ours:* the command, and that it
  touches the unit alone; what the original's click posts was not read, nor whether a
  headquarters' box is passed down.
- **One of five buttons by the unit's level**: a division `create_corps`, a corps
  `create_army`, an army `create_armygroup`, an army group `create_theatre`, a theatre
  `edit_theatre`. `confirmed`. Shown, and cannot be pressed.
- **`attach_button` for a unit with none above it, `detach_button` for one with.**
  `confirmed`. Shown, and cannot be pressed.
- `load_button` for a land unit that is aboard nothing, `unload_button` for one that is.
  `confirmed`. The two aeroplane buttons are shown the same way, *ours*, and
  `disbandbutton` and `newunitbutton` as the file has them. None can be pressed.

## 8. The units under it

The window `sup_children`, only for a unit with any: a heading, the men of all of them, and
a list of one row each. `confirmed`

- `header`: how many there are at each level, the highest first, those that are not none
  with `, ` between: the number, a space, and `NUM_GROUPS`, `NUM_ARMIES`, `NUM_CORPS` or
  `NUM_DIVISONS` - "1 Corps, 2 Division(s)".
- `str`: the men of every land unit under it at any depth, without its own.
- `select_all_childunits` selects them all. Not built: shown, and cannot be pressed.
- The window is as high as its rows, as the brigades' is.

A row is the window `childunit_entry`: `name`, the unit's name - and its leader's in
brackets, which no unit has yet; `str`, its own brigades' men; `order`, the line of section
5; `objective`, an AI's, empty; `counter_type`, `counter_size`, `org_bar` and `str_bar` as
the header's. **A click selects that unit and takes the map to it.**

The original leaves an air unit that is aboard a carrier out of the list. Not built.

## 9. A headquarters' row

The window `sup_objectives`, for a corps, an army, an army group and a theatre, between
the order and the buttons. Built after the maintainer set a corps' panel beside the
original's on 2026-10-09 and the row was missing. It has two faces: `confirmed`

| element | under the player's hand | an AI in command |
| --- | --- | --- |
| `automate_checkbox`, which hands the headquarters to an AI | clear | ticked |
| `tac_aggro_bg`, `tac_aggro_slider_bg`, the red and blue bar | shown | hidden |
| `aggression_slider` | shown | hidden |
| `aggression_checkbox` | shown | hidden |
| `objectives`, what the AI has been told to take | hidden | shown |
| `reorg_hq_checkbox`, whether the AI may reorganise it | hidden | shown |

- **How aggressive a headquarters is** is a figure from -1 to 1 and a switch saying the
  figure is its own. With the switch off the slider shows the figure the headquarters is
  under and cannot be moved; with it on, moving the slider orders the new figure.
- A row of a unit above (section 2) marks a unit that has a figure of its own with a
  star after its name: red above 0.1, blue below -0.1, grey between.

**Here there is only the first face, and none of it can be pressed**: there is no AI to
hand a headquarters to, and nothing an aggression would change. The box is clear, the
slider stands in the middle, and the switch is off.

## What is not built

- **Tooltips**, every one. The original's unit panel says most of what it knows in them.
- **Typing a name** into `unitname`.
- **Several units selected**: the list of their rows and the window over it
  (`multi_unitpanel`).
- **A headquarters' rows for an AI in command**: its objectives, the three stances, the
  power ratio, the assets it wants; and everything the row of section 9 would do.
- **What a unit carries**, and **the battles it is in**.
- **Leaders**: the picture, the choosing, the red name.
- **The windows that come up from it**: the leader list, reorganising, attaching, what a
  brigade is upgraded to, the order of battle browser.
- **Scrolling the panel** when it is longer than the screen.

## What is not established

- **what the panel looks like beside the original's for anything but an idle infantry
  division**: a headquarters with units under it, a unit on the march, a unit of another
  country. The one screenshot also shows what is known to be missing - the leader and
  his picture, a brigade's upgrade button, the buttons this one greys because they
  cannot be pressed - and that **the original's letters are heavier**: `0%` and `4KPH`
  are in a bold face there and a regular one here, which is the fonts' and not the
  panel's (`fonts/readme.md`);
- **the rules behind two things measured on it**: how a turned icon is placed and where
  an edit box puts its text (`interface.md`, section 7). Both fit the screenshot and
  neither was read;
- **which of the header's two selection buttons does what**: the reading left two handlers
  and two buttons unpaired. The one built takes the unit out of the selection, by its name;
- **what the null order calls itself**, which is the line an idle unit shows;
- **whether the checkboxes of a headquarters reach the units under it**;
- **when the original makes the list again**.
