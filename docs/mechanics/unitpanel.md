# The unit panel

What the window of a selected unit shows. Read out of the original on 2026-10-09:
`reversing/findings/FINDINGS-unitpanel.md`. `confirmed` means read; *ours* means decided
here. It was built from the reading and checked with real clicks. **It was set
beside the original on 2026-10-09**, one infantry division in both, and what that
screenshot showed is mended and marked *measured* below: where the bars stand, that the
entries touch, and where the name sits. The rest of the layout agreed with it.

**In one line.** The panel is one list. For the unit selected it is filled with a fixed
run of small windows - the units above it, a header, a status, an order, a strip of
buttons, its brigades, the units under it - and every one of them is written again each
frame from the unit as it then is.

**For one unit, and best for a land unit.** Several selected have the multi-unit panel and a row each instead
(`selection.md`, section 5); a fleet's and an air unit's own lines wait for those to move.

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
  | 1 | a unit above, the highest first - **its name green where an AI commands that unit itself** (since 2026-11-21; the outliner's rows stay as they are) | `sup_parent_unit_entry` | one for each |
  | 2 | the header | `unitpanel` | always |
  | 3 | the status | `sup_unit_status` | always |
  | 4 | the order | `sup_current_orders` | always |
  | 5 | a headquarters' row for an AI | `sup_objectives` | every unit above a division |
  | | three stances, power and assets | `sup_unit_stance` and four more | a headquarters an AI commands - **not built** |
  | 6 | the buttons | `sup_buttons_window` | always |
  | 7 | the brigades | `sup_subunits` | always |
  | 8 | the units under it | `sup_children` | when there are any |
  | 9 | what it carries | `sup_loaded` | a fleet's or an air unit's - **not built** |
  | 10 | each battle it is in | `combat_entry` | one for each: `combatview.md`, section 7 |

- An entry finds its elements by their names once, when it is made, and writes them every
  frame the panel is up. `confirmed`. Here the list is made again when the units above the
  selected one change or it gains or loses its first subordinate, and otherwise only
  written; the original makes it again on events that were not read. *Ours.*
- **The panel is up while a unit is selected** and comes down with the selection. With the
  deploy menu open it stands beside it, the order of battle browser between the two, as the
  original's screenshots show them (given 2026-11-13; `mapunits.md`, section 6);
  until then it gave way to the menu.
- The window's own `oob_button`, the tab down the screen's left edge, opens the order of
  battle browser. Not built: it is shown and cannot be pressed.

## 2. The units above

One row for each unit above the selected one, the highest first. A row is one button,
`unit`, with the unit's name on it, set once. **A click selects that unit and takes the map
to where it is.** `confirmed`

The rows are a plain walk up the order of battle, nothing else asked
(`reversing/findings/FINDINGS-unitpanel.md`, section 2, and `FINDINGS-transport.md`, section D.1, read 2026-11-14). **An air group aboard a carrier is in the order of
battle under its carrier's fleet** - hung there as it is read from an order of battle or a save, or rebased to the
carrier (`transport.md`, section 13.1) - so its rows are the fleet and whatever is above the fleet: the 5th Air wing
aboard USS Ranger shows "CONUS East Theater" and "USS Ranger Taskforce" on a screenshot of the original,
and HMS Furious's 1st Air wing "Home Command Theatre" and "Home Fleet" here. A click on the fleet's row selects the fleet
and takes the map to it, as for any row. A group whose fleet was merged into another has nobody above it and no rows.

The original writes the name in green for a unit an AI commands. Not built: there is no AI.

## 3. The header

The window `unitpanel`. The original uses the same window, by the same code, for each row
of the list of several selected units. `confirmed`

| element | what it is given | |
| --- | --- | --- |
| `unitname` | the unit's name. An edit box, the name changed here (`reversing/findings/FINDINGS-unitrename.md`): typed into in the single panel for the player's own unit, filled once as the header is made with the caret at its start, held to one line - a character that would wrap is erased at once - with `"` refused besides the font's seven; **what is typed goes out as the header goes**, when the selection changes or the panel is made again, trimmed, and only where it is not empty and not the name byte for byte, as a `CRenameUnitCommand` that tests nothing but that the unit is still there [exe]. Enter sends nothing. A row of several selected units shows the name and takes nothing. *Ours*: the original also renames the unit at once on the player's own machine, beside its command; here the command alone does it | typed into and sent as read, since 2026-11-12 |
| `counter_type` | the sprite `GFX_counter_` + the type of the unit's first brigade | built |
| `counter_size` | the mark of its rank, as on a line of the deploy menu (`mapunits.md`, section 6) | built |
| `unit_activity` | what it is doing, below | built in part |
| `unitstrength` | a land unit's men; a fleet's or an air unit's count of ships or wings, below | built |
| `unitleader` | its leader's name, in red where he has not the rank (`leaders.md`, section 4); `NO_LEADER` with none | built |
| `leader_photo` | `gfx/pictures/portraits/`, the leader's `picture`, `.tga`; made again only when the leader changes | built; the file's picture with no leader, or with a portrait the install has not got |
| `leader_types` | his kind and one more: a general 1, an admiral 2, an air marshal 3 - a general's with none | built |
| `leader_button` | opens the window a leader is chosen in (`leaders.md`, section 12); the portrait opens it too, below | built |
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

Built, all of it but the land unit aboard aeroplanes (2026-10-09, with the battle:
`landcombat.md`).

**The leader button** may be pressed but for a unit falling back, one in a battle, a fleet in a
sea province, one standing where an enemy of the player holds the province, and a land unit
aboard a ship or an aircraft (`reversing/findings/FINDINGS-leaderwindow.md`, section 9):
`confirmed`. Built, but for what is aboard something, which nothing is yet. **Nothing asks
whose unit it is**, and the original's refusal of an expeditionary force is undone by its own
last test. **The portrait is registered with the same handler and never disabled**, so a click on
it opens the window where the button cannot be pressed (`inferred`; `leaders.md`, oddity 20).

**The strength is a land unit's men**: `confirmed`

    men = the sum over the brigades of  strength x 100        (each brigade cut to a whole man)

written whole under 1000 and with a comma before the last three digits above: `8,997`. The
original writes it in red where the unit's strength is under the share at which it
shatters; shattering is not built.

**A fleet's and an air unit's is its count of ships or wings** (read 2026-11-14,
`reversing/findings/FINDINGS-unitpanel.md`, section 4): `confirmed`. Fleets and air units share
one routine for the figure, which writes the number of the unit's own sub-units in plain decimal -
`19` for the Home Fleet of 1936, `3` for the French transport fleet - with no colour, no
separator, and nothing asked of a ship's or a wing's strength: a ship nearly sunk counts as one.
The row of each unit in the panel of several shows the same. Built, and looked at with `--click`
(`transport.md`, section 14).

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
| `unitattrition` | what the province wears it down by, whole percent; `?%` with no brigades | its first brigade's figure, whole, since 2026-11-14 (`upkeep.md`, section 3a) |
| `unitbase` | `SUPPLIED_FROM`, a space, and the capital its province is supplied from; ` ???` where no friend holds that. A fleet or an air unit: its base, then `(`, its range and `km)` - or ` ???` with no base | a land unit: its country's capital. A fleet and an air unit: built; the range is written whole, *ours* |
| `speed` | below | built |
| `unit_cw_strip` | a land unit's width: frame 1 under 2, then 2, 3, 4 for under 4, 6, 8, and 5 beyond | built |
| `unit_weight` | a land unit's weight aboard ship, whole | built |
| `supply_status`, `fuel_status` | two bars, below | full |
| `unitstatus_moving` | shown on the march | built |
| `unitstatus_distance` | a land unit's reach of its headquarters: frame 1 in reach, 2 out of it (`leaders.md`, section 8) | built |
| `unitstatus_combinedarms`, `_dugin`, `_delay` | shown for a mix of brigades that counts as combined arms before any technology, a unit dug in, an attack delay running | built (`landcombat.md`) |
| `unitstatus_nuke` | shown for a nuclear order | hidden |

- A unit's width and its weight are its brigades' `combat_width` and `transport_weight`
  added up.
- **The speed is not what an hour adds.** It is the top speed times everything that scales
  it (`movement.md`, section 5), without the define that turns it into an hour's march:
  infantry on dry plains shows `4`, and walks 0.2 an hour. Whole when it is a whole number,
  to two decimals when not, and the text `KPH` straight after it: `4KPH`, `2.25KPH`.
- **The supply and fuel bars are the province's, not the unit's**:

      days = SUPPLYPOOL_DAYS, whole, at least 1
      bar  = what lies in the province / (days x what the units in it need a day)

  and full where they need nothing - as in a capital, which counts no need of its own. The
  province is the one the unit draws on, and what its units need is what the day's supply
  pass counted (`supply.md`, sections 11 and 13). Built since the pass is.

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
- Built: the three about a battle, `MOVE_ORDER_D`, and for everything else `NO_ORDERS`,
  "No Order". *Ours:* what the original calls a unit with nothing to do was not read; that
  key is in its files. And for an attacker with no path left, `WHERE` is the battle's own
  province.
- **An order's own text names the province the order is for**, its end and not its next
  step (`fleetsandair.md`, section 5): `confirmed`

      a move                       MOVE_ORDER_D with WHERE          "Move to Königsberg."
      a fleet's change of base     the order's name, " :", the province    "Rebase :Königsberg"
      an air unit's                ORDER_AIR_REBASE_SHORT_DESC with PROV   "Rebase(Königsberg)"

  *Ours:* a fleet sent straight somewhere has no order, and reads as a land unit on the
  march does - what the original's empty order says of it was not read.
- **Where the way ends and when the unit will be there is a tooltip** in the original, over
  the header's activity strip: "Will arrive in $PROV$ on $DATE$" - again the next province.
  The panel's tooltips are not built yet (`tooltips.md`), so the readout beside the panel
  says both for now, for the whole way. The readout is of a selected province, so it is there only when one was selected
  before the unit was.

**The button**: a land unit on the march that is not retreating is halted, by the same
command a click on its own province gives (`movement.md`, section 1). **Any other unit has
its order taken back** - every order but an air unit's change of base - and is left with
no order and with the way it was on: a fleet sent somewhere keeps sailing. `confirmed`
(`reversing/findings/FINDINGS-unitpanel.md`, section 6). **An air unit with no order flies
home by itself** once it has no way to go and is in no battle and no bombing
(`aircombat.md`, section 1): so a mission taken back over enemy ground stays over its bombing
or its battle until that ends, and then goes home. Until 2026-10-10 the button here halted a
marching unit of any kind and did nothing for anything else - testing found it did
nothing for a mission.

## 6. The brigades

The window `sup_subunits`: a heading, and a list of one row for each brigade in the unit's
own order. `confirmed` That is the order of their types' `priority`, highest first (`units.md`, section 3, *The order
of a unit's sub units*): the original's list copies the unit's brigades into a list of its own through the same
insertion by priority, which leaves the unit's order as it is, since it is already so. `confirmed`
(`reversing/findings/FINDINGS-subunitorder.md`, section 3)

- `header`: for a land unit `LAND_COUNT`, "$NUM$ Brigades". A fleet's is `NAVAL_COUNT` with
  its capital ships, screens and others, and an air unit's `AIR_COUNT` with its fighters and
  bombers. Built - but which ship counts as which was not read. *Ours:* a capital ship is
  one its type calls `capital`; the others are submarines (`is_sub`) and anything that
  carries (`transport_capability`); the rest are screens; a bomber is a wing its type calls
  `is_bomber`.
- `positioning`: empty but for a fleet.
- **The window is as high as its rows**: its label's height and its rows', up to the
  height the file gives the window - five rows in the base game - and the list scrolls
  past that. `confirmed`
- **And it makes its list as high as those rows**, each time its height is asked, so how many rows show before the
  list scrolls is the *window's* height less the label's over a row's, and the height the file gives the list plays no
  part once there is a row. `confirmed`, read 2026-11-21 (`reversing/findings/FINDINGS-unitpanel.md`, section 8) after
  a BlackICE fleet of nine ships showed seven rows and a strip of map under them: BlackICE's window is 273 high, eight
  rows, and its list 226, seven. The base game's two figures, 176 and 176, give five either way. Only this window does
  it; the units under it and what it carries keep the lists their files give. *Mod-able*: the window's `size` in
  `single_unitpanel.gui`; the rule is the executable's.
- **A fleet with fewer than three units aboard has twice the room** - its passengers and the air units on its
  carriers counted together, the rows of what it carries (`transport.md`, section 12): ten rows in the base game,
  sixteen in BlackICE. `confirmed`, the same reading. Hardcoded, the three and the doubling both.

A row is the window `subunit_entry`:

| element | what it is given | |
| --- | --- | --- |
| `counter_type` | the sprite `GFX_counter_` + its type | built |
| `counter_size` | the lowest mark | built |
| `subunit_name` | its name | built |
| `subunit_type` | its type's name | built |
| `subunit_level` | which model of its type it is, as a Roman numeral, I for the first | built, see below |
| `subunit_amount` | a brigade's men: its strength x 100, as the builders write a strength (`builders.md`); **a ship's or a wing's strength as a whole percentage**, "100%" - the brigade's own text, which the reorganise window's row shows too (`reversing/findings/FINDINGS-reorgwindow.md`, A.3; fixed 2026-11-04, when ships read "10,000") | built |
| `unit_experience` | its stars, below | built |
| `unit_cw_strip` | its width, as in section 4 | built |
| `org_bar` | 100 x its organisation / the most it may have, officers and all (`organisation.md`) | built |
| `str_bar` | 100 x its strength / its type's `max_strength` | built |
| `unitstatus_reserve` | shown for a reserve | built |
| `unit_pride_star` | a star for a pride | built |
| `upgrade_button` | shown where the brigade may be changed where it stands (`upgrades.md`, section 5) | hidden |

- **The stars**: none under an experience of 1; otherwise the frame is two more than the
  experience over ten, rounded to the nearest whole and to the even one from a half, and
  no higher than the eleventh. The strip goes up half a star a frame, so an experience of
  1 shows half a star, 45 two and a half, and 90 or more all five.
  The original adds a define to a pride's experience first; not built.
- **The model** is the one the brigade was made as. *Ours:* the original works out each
  time which model the brigade's technology levels fit best.
- A row for an upgrade in progress stands above the brigades (`upgrades.md`, section 5).
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
  `edit_theatre`. `confirmed`. The first four make a headquarters (`attaching.md`, section 4); `edit_theatre` opens the
  theatre editor (`attaching.md`, section 7).
- **`attach_button` for a unit with none above it, `detach_button` for one with.**
  `confirmed`. Shown, and cannot be pressed.
- `load_button` for a land unit that is aboard nothing, `unload_button` for one aboard a
  fleet; **both work** since 2026-10-15 (`transport.md`, section 12): load where a fleet is
  found for it in its port, unload where the fleet is in port. The aeroplane buttons are shown
  as the original shows them and cannot be pressed - nothing carries by air yet - and
  `disbandbutton` as the file has it, not pressable. **`newunitbutton` works** since 2026-11-04 (section 7.1).
- **A fleet's and an air unit's panel ends in what it carries** (`sup_loaded`, after the units
  under it): `transport.md`, section 12.

### 7.1 The new-unit button

Read 2026-11-04 (`reversing/findings/FINDINGS-newunit.md`). `confirmed`. **Two buttons in one**: a plain press makes an
empty unit to hand brigades to, Ctrl and a press split the unit.

**Greyed**, at the first that holds:

1. a land unit with one brigade or fewer - **two or fewer where it has a leader**;
2. in a battle;
3. an AI agent runs it or a unit above it;
4. on loan (nothing is lent here);
5. a fleet or an air unit carrying anything;
6. a land unit aboard a fleet or a plane;
7. an attack delay;

and otherwise pressable where the unit has more than one brigade. All exe-hardcoded.

**A plain press** - one at a time; a second does nothing until the first unit has come - makes **an empty unit beside
it**: a division for a land unit of any level, else a fleet or an air unit, in its province and at its base. Once it
exists **it is added to the selection and the reorganise window opens**, the selected unit on the left and the new one on
the right (`reorganise.md`) - which disbands the new one at its close if nothing was moved into it. And where the
selected unit has a commander who may take **the selected unit** - the original asks that and not of the new one - the
new one is put under him.

**Ctrl and a press** split the unit, where it has more than one brigade, is in no battle, is going nowhere, is at its
own base if it is a fleet or an air unit, and holds no headquarters brigade if it is a land unit:

1. **a new unit for every brigade but one**, of the old one's level, in its province, at its base, under its commander
   where he may take it;
2. into each, **the old unit's first brigade**, by the reorganise window's tests (`reorganise.md`, section 4) - so the
   old unit keeps its last; and a first brigade that may not go is refused every time. Each new unit is named by its
   brigade and looks for a battle where it stands;
3. **a land unit and every land unit made lose their leaders**; a new fleet or air unit is given the best free one where
   its country chooses its own;
4. the selection becomes the old unit and the new ones, in that order.

**Ours**: a new unit a refused brigade left empty is taken out at once, where the original leaves it for its hourly pass,
which nothing here has yet; the new units take their numbers as the command runs, where the original reserves them as
it is posted; and the selection, the reorganise window and the attaching are the panel's, done when it sees the units
the command made - the original's command changes the player's selection itself.

## 8. The units under it

The window `sup_children`, only for a unit with any: a heading, the men of all of them, and
a list of one row each. `confirmed`

- `header`: how many there are at each level, the highest first, those that are not none
  with `, ` between: the number, a space, and `NUM_GROUPS`, `NUM_ARMIES`, `NUM_CORPS` or
  `NUM_DIVISONS` - "1 Corps, 2 Division(s)".
- `str`: the men of every land unit under it at any depth, without its own.
- `select_all_childunits`, the ribbon, adds every land unit under the unit to the selection, the unit staying selected;
  pressable for a land unit only (`selection.md`, section 5.3). A Ctrl-click on a row brings that unit's whole command.
- The window is as high as its rows, as the brigades' is.

A row is the window `childunit_entry`: `name`, the unit's name - and ` (`, its leader's,
`)` where it has one; `str`, its own brigades' men; `order`, the line of section
5; `objective`, an AI's, empty; `counter_type`, `counter_size`, `org_bar` and `str_bar` as
the header's. **A click selects that unit and takes the map to it.**

**The original leaves an air unit based on a carrier out of the list**, and out of the count that decides whether the
window is there: the groups aboard a fleet's carriers are under the fleet in the order of battle (section 2), and the
fleet's list of what it carries shows them instead (`transport.md`, section 12). A group under a fleet whose carrier
sank is no longer based on it, and is listed. `confirmed`

## 9. A headquarters' row

The window `sup_objectives`, for a corps, an army, an army group and a theatre, between
the order and the buttons. Built after a corps' panel was set beside the
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

Both faces are built (2026-11-09, `reversing/findings/FINDINGS-hqrows.md`). `automate_checkbox` takes away the highest agent over the unit or, with none, makes one for it, as a command (`CAutomateUnitCommand`).
**A corps, an army and an army group are handed over too** (section 9.1, since 2026-11-21); a theatre's headquarters
heading no theatre keeps the box greyed - the original's would read a null theatre there. `reorg_hq_checkbox` sets the plan's flag. The aggression box and slider are commands too, and a battle's tactic is picked by the leader's unit's aggressiveness: the first unit from it upward with an active plan by its stance (-1, -0.5, 0, 0.5, 1), or with none, the first with an aggression of its own.

**Under an agent, on it or above it**, five rows follow, a headquarters only. A stance row each for the ground, air and naval stance, all `sup_unit_stance`: the button says the stance and its tooltip its description, and is greyed where a unit **above** this one holds the agent. It opens `sup_stance_change_window` over the row, five rows for the ground and three each for air and sea, the stance in force not marked; a second press fills it again and does not shut it, and a pick shuts it. A ground pick goes through the agent, its reserve let go; an air or naval pick is one change of the plan. `sup_power_ratios`: each side's power over the larger side's, the larger bar about full, under 0.100 both empty; the row takes no click and shows no tooltip: the original's routine has one for each bar, but the two text boxes lie over them and hovering there in the original shows none (2026-11-09). `sup_desired_assets`: `WE_NEED`, and each group's representative type the plan is short of as a number and its counter, or `TRADE_NOTHING`; its tooltip lists them by name.

### 9.1 A headquarters below a theatre handed to the AI

(`confirmed` but where marked, `reversing/findings/FINDINGS-hqrows.md`, 7a; `reversing/findings/FINDINGS-aiplans.md`,
slot 76; `CPlanBuilder`, `CAgents.Adopt`.) Ticking the box on a corps, an army or an army group switches its plan on, and
**every stance begins defensive** - ground, air and naval - as for a theatre (*ours*, `compatibility.md`, entry 32: the
original's keeps what its plan held); after that they are what the player picks on the rows of the headquarters holding
it. In the AI's next pass the unit is given an agent of **no theatre**, appended last, and its area is made at once by
the plan builder. A plain right click with it selected sets its objectives as a theatre's (`mapunits.md`, section 12).
Its hour is any agent's (`agents.md`, section 3) - its subordinates, its land block, its air and its fleets - but what
needs a theatre does nothing: the area of section 8, the command ladder, the focus areas, the force planners. **The
player's country gets no stances or objectives from the AI**: those stay his.

**The area** - the plan builder's main path, every figure the engine's and none a define:

1. Every front of every theatre of every country the agent may operate in, its own among them, is a candidate.
2. For each objective whose province a friend does not hold, each front province beside land in an area that is held by
   the objective's holder, or by an enemy where the objective's holder is an enemy too, is scored: its distance to the
   objective plus 10, times 0.1 where it is in the theatre the unit stands in, and times 0.1 again where it is in the
   unit's own area or one beside it. The lowest score, the first of equals, chooses a province and its front; every
   front a province was scored on is collected.
3. **How much of that front**: the unit's divisions, `mine`, and the land units of other agents standing on or bound for
   the front, `others`; `round(mine x the front's provinces / (mine + others))`, at least one and no more than `mine`,
   provinces - the chosen one and those nearest it.
4. Then every province of every other collected front in an area where this agent's units are at least as many as all
   other agents' together - a tie its own, and for a played country, whose other units have no agent, every one.
5. The area is posted as the plan's, and what follows a theatre's new area follows it: its front provinces, its
   approaches, its positions' scores.

**With no front scored** the original's fallback runs, and its two commands leave a plan that had an area with none; one
that had none is given the front provinces in an objective's area within 600 of it beside an area of fewer than five
provinces (*inferred*: which neighbour's area is meant was not read). **It is made again** where an objective is set or a
path drawn for the unit or one under it, and as the country's theatres are refreshed. **Ours**: the original reaches it
at a refresh only where it comes before the agent covering that theatre in the country's list, each played theatre having
one from the first hour; here a played theatre has an agent only once it is handed over, so every agent below a theatre
is reached at every refresh. **The drawn path's frontier province** (the builder's step 2) is not built: whether it feeds
the main path was not read.

Tried with `OpenHOI3.SaveCheck automate <install> GER POL 6 0 corps` on 2026-11-21: Germany's VII. Armeekorps, five
divisions, given Warszawa, is handed twelve front provinces in Silesia and East Prussia, and its five divisions march
for them from the first day.

## 10. Tooltips

Read 2026-10-12, `reversing/findings/FINDINGS-unitpaneltooltips.md`; built in
`lib/OpenHOI3.Interface/Tooltips` - `CUnitHeaderTooltips`, `CUnitStatusTooltips`,
`CUnitOrderTooltips`, `CBrigadeTooltips` and `CLeaderTooltip` - and tried with `--click`.
`tooltips.md` has how a tooltip is asked for and shown. **Each entry answers for its
elements by name**, and an element no entry owns says nothing. `confirmed` unless marked.

**A text box over a button.** In the original the topmost element under the mouse answers,
whatever it is. Here a text box lets the mouse through so that the button under it keeps its
clicks; the text boxes these tables name answer first where the mouse is over them, and the
button keeps the clicks (*ours*, `CGuiObject.PassiveTooltip`).

### 10.1 A unit above, the header and a unit under it

A unit above: `SELECT_PARENT_UNIT`, anywhere on its row.

The header, by element - a pair is the text and what comes after a moment:

| element | what it says |
| --- | --- |
| `unit_activity` | the first that holds: in a battle `UV_ATTACKING` or `UV_UNDER_ATTACK`; falling back `UV_RETREAT`; held by an attack delay `UW_ATTACK_DELAY`; on the march `ESTIMATE_ARIVAL`; its weakest brigade's strength under 0.1 `UV_NO_STRENGHT`; its weakest organisation under 1 `UV_NO_MORALE`; unable to act `UV_NOT_MOBILE`; otherwise nothing |
| `remove_unit_from_selection_button`, `only_unit_from_selection_button` | `DESELECT_UNIT`, `OSELECT_UNIT`, each with its `_DRO` |
| `leader_button` | `UW_LEADER_BUTTON_IRO`, `UW_LEADER_BUTTON_DRO` |
| `leader_types` | **the key `xxx`**, both halves - the original's |
| `unitname` | `UW_UNITNAMES_IRO`, `UW_UNITNAMES_DRO` |
| `exp_force` | `UW_XP_FORCE_IRO` and `_DRO`: not built, nothing is an expeditionary force |
| `counter_type` | a line for each brigade: `£` and its type's key - which draws its counter - a space and its name |
| `counter_size` | a unit whose counter is a brigade's (a lone brigade, an air unit) `UNIT_BRIGADE_NAME` and `UNIT_BRIGADE_DESC`; any other its level's `_NAME` and `_DESC` of `UNIT_THEATHRE`, `UNIT_ARMYGROUP`, `UNIT_ARMY`, `UNIT_CORP`, `UNIT_DIVISION`, `UNIT_NAVY`, `UNIT_AIR` |
| `unitstrength` | a land unit: `UW_TOTAL_STR_IRO` with the men of its most, then each brigade's line, `- `, its men in red under half its most, yellow under its most and white at it, `/` and its most; a fleet or an air unit: a line for each ship or wing short of its most, green from 90%, yellow from 66%, red below, after `UW_FOLLOWING_HURT` - or `UW_ALL_FIT` where none is |
| `unitleader`, `leader_photo` | the leader's tooltip, 10.5 |
| `org_bar` | `CURR_COMB_ORG` and the average organisation to one place, a line break, then `+§Y` and what a brigade of the unit's mean morale regains an hour to three places, nothing closing it; no second line for a unit fighting or aboard a fleet at sea (since 2026-11-14) |
| `str_bar` | `CURR_COMB_STR` and the strength over the most, as a percentage to one place |

A unit under it says the same of its counters, its level and its two bars, and of `order`
its order's description, 10.3.

**The hours of an attack delay** are the delay whole, times the hours the unit has attacked
over `UNIT_ATTACK_DELAY_PERIOD` where those are fewer, and never under one; the date is that
many hours on. **A date** is written ` 13:00, 1 January, 1936` - with a space in front, and
not the top bar's form. **On the march** the date is when it reaches the next province, and
the province is that one.

**The organisation bar's second line** is the hour's factor worked out again and times the unit's mean morale and the
fifth of it an hour brings back (`organisation.md`, section 3; `COrganisation.ShownRegain`; read 2026-11-14,
`reversing/findings/FINDINGS-unitpaneltooltips.md`, section 3.5). Not quite the hour's own: **the roads count at sea**,
so a fleet at sea is shown a hundredth of what it regains, and **a unit given no supplies is shown nothing** where the
hour takes organisation away; both the original's, kept and listed in `docs/enhanced.md`. The leader window's own part, which the header asks first
while that window is up, is the window's (`leaders.md`, section 13). *Ours*: a unit is unable to act when it has no brigades; what
the original asks was not read.

### 10.2 The status

| element | what it says |
| --- | --- |
| `unitlocation` | `UW_LOC_IRO`, and `SHORTCUT` and the key of the button that finds the unit after a moment |
| `unitattrition` | the first brigade's attrition by its causes, below |
| `unitbase` | a land unit `UW_SUPPLIES_FROM` and, after a moment, what the element itself shows; a fleet or an air unit `UW_BASED_FROM` and, after a moment, `UW_BASE_NAVAL_SUPPLY_INFO` or `UW_BASE_AIR_SUPPLY_INFO`, below (since 2026-11-14) |
| `unit_weight` | `TRANSPORT_WEIGHT` |
| `unit_cw_strip` | `UNIT_COMBAT_WIDTH`, `: ` and the width to one place in yellow |
| `unitstatus_delay` | `UW_STRATDEPLOYMENT_DELAY` for a unit under a strategic redeployment, any other `UW_ATTACK_DELAY`, both as the header's (since 2026-11-14) |
| `unitstatus_nuke` | `UW_CARRYING_NUKE` |
| `unitstatus_dugin` | `UW_DUGIN` with the days dug in and the cap: `UNIT_DIGIN_CAP` and the country's technologies' `dig_in_cap` (since 2026-11-14), each whole |
| `unitstatus_combinedarms` | `CA_BONUS_DETAILS_TOTAL`, then on lines of their own the mix's own `CA_BONUS_DETAILS_BASE`, what technology adds `CA_BONUS_DETAILS_TECH` and what the leaders' traits add `CA_BONUS_DETAILS_TRAIT`, the last two only above nothing; each a percentage to two places |
| `unitstatus_moving` | on the march and in no battle, `ESTIMATE_ARIVAL` |
| `speed` | `UW_SPEED` and a line break; where something scales its speed, ` / ` and the speed it has with `KPH`; a line for each brigade, its counter, its name, ` - `, its type's speed, a space and `KPH`; a line break; and where something scales it, `SPEED_MODIFIER` with the whole multiplier as a percentage to two places, green above one and red below, then for each term not one, in this order, a line break, `- ` and its line with the term the same way: `SPEED_MODIFIER_COMBAT`, `_FUEL`, `_TERRAIN`, `_TRAIT`, `_ADJACIENCY`, `_WEATHER`, `_LOCAL_MOD`, `_MOTORIZED_BONUS`, `_RETREATING` (since 2026-11-14) |
| `unitstatus_distance` | `UW_DIS_OUT_OF_RANGE` where a unit above is out of radio range; for a headquarters, `UW_EFFECTIVE_RADIO_STRENGTH` with its level's name and how far it reaches in full; then for each headquarters up the chain, `UW_DIST_TO` with its province, its level's name, its name, **the name of the unit below it in the chain** and the distance between them, green where all its command gets through and yellow otherwise - each line ending in a line break |

**A headquarters out of reach is yellow, never red**: the original's red is for a reach under
nothing, and a reach is never under nothing. Kept as read.

**A base and how well it serves** (read 2026-11-14, `reversing/findings/FINDINGS-unitpaneltooltips.md`, section 5;
`CBaseCrowding.Describe`): `NAME` the base's province - for an air group aboard a carrier its fleet's base where the
fleet has one, else where the carrier is; `VAL` the share of its work the base does, as a percentage to one place;
`SHIPS` or `PLANES` how many ships or wings are based there, whole; `DAILY` what it has room for, to two places. Room
and share are the crowded base's (`organisation.md`, section 4): a port's naval base by `NAVAL_BASE_EFFICIENCY` and its
holder's `naval_base_efficiency`, an airfield's level, an air group where its carrier is as much as is aboard; the share
held between a quarter and the whole - and **a quarter where nothing is based**, where the hour's crowding takes the
whole: the original's. Hardcoded but the words, the define and the modifier [mod].

**The digging-in cap** is the one the day's digging holds a unit to (`landcombat.md`, section 4): the define and the
technologies' `dig_in_cap`, which no file of the base game declares [mod].

**`unitattrition`** breaks the first brigade's attrition into its causes (since 2026-11-14,
`CUnitStatusTooltips.Attrition`; `reversing/findings/FINDINGS-unitpaneltooltips.md`, section 11).
Nothing for a brigade that is not land, and nothing for a unit with no brigades. In a friend's
province away from the front, `IN_FRIENDLY_FOR_ATT` alone. Otherwise `IN_FRONT_PROV_ONLY_10` at
the front, each cause then counted at a tenth, or `IN_HOSTILE_FOR_ATT`; a line break and the
province's sources of `attrition`, each times that base (`modifiers.md`, section 8), and a line
break where the text does not end in one. Then, each times the base and to two places:
`ATTR_INFRA: §R` and what the infrastructure lacks, a `+` above nothing, where it lacks any;
`ATTR_WEATHER: §R+` and the weather's, where above nothing; the terrain's name, `: §R+` and the
ground's - its own and the brigade's adjuster - where above nothing; each closed by `§!` and a
line break. Where the technologies' `maximum_attrition` is not nothing, a blank line,
`ATTR_TECH: `, green below nothing or `§R+` above, a hundred times it to two places and `%§!`
with a line break. Last a blank line, `§Y`, `TERRAIN_ATTRITION`, `§!`, `§R+` above nothing and
the whole figure to two places with `%§!`. **The weather written is the weather before the figure
holds it at nothing**, so a cold that winter effects take past nothing writes no line and adds
nothing - the original's. All hardcoded but the words and the defines the causes read [mod].

**The speed's terms are worked out again by the tooltip, not taken from the multiplier**, as the original's are
(`reversing/findings/FINDINGS-unitpaneltooltips.md`, section 11; `CMovementSpeed.Terms`), and they do not always multiply
to it: a unit in a battle that attacks in none is given its quickness held at one where the multiplier gives it a
hundredth, and the weather and the province's `local_unit_speed` are two lines where the multiplier adds them into one
term. Kept as read. Hardcoded but the words and the defines and modifiers each term reads [mod].

**The two bars** say `UW_SUPPLY_STATUS2` and `UW_SUPPLY_FUEL2`: `CON` what the unit uses a day, two places; `VAL` the share it got last day, whole; `FROM` the province it draws on - the one it stands in, or a fleet's or air unit's base; `DAYS` what lies there over what **all the units standing there** use a day, whole, and 0 where they use none. Hardcoded but the words [mod].

### 10.3 The order and what an order calls itself

`cancel_order_button` says `CANCEL_CURRENT_ORDER` - every order built may be called off, and
`non_cancellable_order_desc` waits for one that may not. Anywhere else, the order's
description: `MOVE_ORDER_DESC`, `ORDER_REBASE_DESC`, `ORDER_AIR_REBASE_DESC`,
`ORDER_RESERVE_DESC`, `ORDER_PATROL_DESC`, `ORDER_INTERCEPT_DESC`, `ORDER_SORTIE_DESC`,
`ORDER_CONVOY_ESCORT_DESC`, `ORDER_CONVOY_RAID_DESC`, `AIRSUP_DESC`, `INTERCEPT_DESC`,
`GROUNDATTACK_BOMB_DESC`, `INTERDICT_BOMB_DESC`, `NAVALSTRIKE_BOMB_DESC`, `STRAT_BOMB_DESC`,
`LOG_BOMB_DESC`, `AIRFIELD_BOMB_DESC`, `INSTALLATION_BOMB_DESC`, `PORTSTRIKE_BOMB_DESC` -
the original's table, which has fourteen more for orders not built. **A unit with no order
says nothing**: the original's idle order describes itself as empty. A unit sent straight
somewhere is on a move here (*ours*, as section 5).

### 10.4 The brigades, the buttons and a headquarters' row

The window over the brigades: a fleet's `positioning` says `POS_STACK_PENALTY` - the hull
limit, what each hull over it costs as a percentage, and the cost in all, held between nothing
and `NAVAL_STACK_POS_PENALTY_MAX`. A brigade's row: `unit_cw_strip` its type's width, as the
status's; `unitstatus_reserve` `UW_RESERVE_INFO` with what the country's
`reserves_penalty_size` takes away; anything else **its summary**:

    (§Y + the model's name + +* where marked + §W), and no line for a type with no model
    PRIDE_OF_FLEET, for the pride of a fleet
    SUBTT_STRENGTH: §Y + its strength + §W/ + its most - men for a brigade, else whole and %
    SUBTT_ORG: §Y + its organisation + §W/ + the most it may have, each with the places it needs
    SUBTT_EXP: §Y + its experience, the pride's bonus with it, to three places + §W%
    for a ship or a wing: MAXIMUM_SPEED §Y + its type's speed to two places + §W KPH, and
          RANGE §Y + its range whole + §W KM, each only where it is not nothing
    a line for each technology it carries: its name, " : §Y", the level's name - or YES or NO
          for a technology of one level - and, where it is on its way to the next level,
          §W(+ the way it has come as a percentage to two places %)

**The model** is the one nearest the levels the brigade carries, of its builder's models, else its owner's, and is
marked `+*` where the first its levels match exactly is not that one - not, as this said until 2026-11-14, "a model the
country no longer builds": nothing here asks what the country builds (`units.md`, section 5). **A model's name** is
that country's text `GER_infantry_brigade_1`, for a member of a faction that does not lead it the leader's text, else
the game's `infantry_brigade_1`, else the type's name and the model's number plus one in Roman numerals (the faction
leader's since 2026-11-14). The brigade's numeral in its row is the model nearest its levels of its **owner's** models.
**A level's name** is nothing at level 0; else the country's text `GER_infantry_activation_3`,
else the game's, else `MODEL_TAG` and **a year**: the technology's `start_year` at level 1 and
`first_offset` plus `additional_offset` for each level past 2 - so Their Finest Hour's infantry
technologies run 1918, 1934, 1936 and on by two. That the figure after a level is the way to
the next one is `likely`. A brigade's upgrade button and the line of a row that stands for an
upgrade being made are built (`upgrades.md`). *Inferred*: that the speed and the range are yellow.

The strip of buttons: each checkbox says what a click would do - `UW_TURN_ON_PRIO` or
`UW_TURN_OFF_PRIO`, `_REINF`, `_UPGRADE` - by the unit's own setting; `create_corps` and its
three fellows `UW_CREATE_CORPS` and the others with `UW_CREATE_ABOVE_DRO`; `edit_theatre`,
`disbandbutton`, `newunitbutton` (with `SPLIT_UNITS_DESC` and the brigades) and `oob_button`
their keys. A button that cannot be pressed says why as the original does: `attach_button`
`UW_NOT_POSSIBLE_TO_ATTACH`; the load and unload buttons `UW_LOAD_IS_VALID` or
`UW_LOAD_NO_FLEET_FOUND`, `UW_UNLOAD_VALID` or `UW_UNLOAD_NOT_VALID` (`transport.md`, section 12);
and since nothing carries a unit by air here, `UW_LOAD_PLANE_FAIL` - or `UW_LOAD_PLANE_FAIL_LOW_ORG`
where a brigade has under `PARATROOP_MISSION_ORG_REQ` of its organisation - and
`UW_UNLOAD_PLANE_BASE_ONLY`.

A headquarters' row: `aggression_checkbox` `AGGRESSION_OFF`; the slider's four pieces
`AGGRESSION_LEVEL` with `AGGRESSION_NEUTRAL` - the original's from -0.3 to 0.3, where it stands;
`automate_checkbox` why the unit is not handed to an AI - a division `CANT_AUTO_DIVISIONS`, a
theatre `CANT_AUTO_NO_AREA` (none has an area here), a fleet or an air unit `ONLY_AUTO_ARMIES`,
anything else `PLAYER_CONTROLLED` - with `AUTOMATED_DESC` after a moment; `reorg_hq_checkbox`
`ALLOW_HQ_REORG`.

### 10.5 A leader

    his rank's name - LAND_RANK1..4, SEA_RANK1..4 or AIR_RANK1..4, INACTIVE at none -
          + " §Y" + his name + "§W\n"
    where the unit he commands is a fleet or an air unit: LEADER_COMMAND_SIZE, " §G" where
          his rank is enough for that unit and " §R" where not, " ", how many ships or wings
          his rank commands, " ", SHIPS_LABEL or PLANES_LABEL by that unit, "§W\n" - nothing
          where he commands nothing (corrected 2026-10-12 with the leader window's tooltips,
          `leaders.md` section 13: this said an admiral or an air marshal)
    an admiral: LEADER_POSITIONING + " §Y" + a tenth of his skill as a whole percentage + "%§W\n"
    LEADER_SKILL + " §Y" + his skill, " (" + LEADER_SKILL_AT_MAX + ")" at his most, "§W\n"
    LEADER_XP + " §Y" + how far he is to his next skill, a percentage to two places + "%§W\n"
    where he has traits: "\n" + ACTUAL_TRAITS_DESC, and "\n- §Y" + each trait's name + "§!"
    where he is part of the way to a trait: "\n" + TO_GAIN_TRAITS_DESC + "\n", then a line
          "- " + TRAIT_GAIN_PROGRESS for each, between line breaks

**How far he is to his next skill** is worked in fifteen binary places: his experience less his
skill's threshold, shifted up fifteen, over the next threshold less his own, both shifted up
fifteen; that times a thousand shifted up fifteen, and shifted down thirty - thousandths. The
thresholds are those of `leaders.md`. Nothing from skill 12; at skill 11 the next "threshold" is
the word after the original's table, a 1, so the share is nothing or less - kept as read. A
trait's progress shows above nothing and short of the whole. **The original lists them in the
order the traits lie in its memory; here in the order of the traits file** (*ours*).

## What is not built

- **Tooltips** the original has and section 10 lists as not built; the battle row's, which are
  a battle's.
- **Typing a name** into `unitname`.
- **Several units selected**: the list of their rows and the window over it
  (`multi_unitpanel`).
- **A headquarters' rows for an AI in command**: its objectives, the three stances, the
  power ratio, the assets it wants; and everything the row of section 9 would do.
- **What a unit carries.**
- **The windows that come up from it**: reorganising, attaching, what a brigade is upgraded
  to, the order of battle browser. The leader window is built (2026-10-11, `leaders.md`,
  section 12).
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
