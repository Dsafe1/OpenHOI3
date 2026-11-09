# The four windows a unit is ordered from

What the original's division designer, ship builder, air builder and brigade builder show,
what their buttons do and what they order, written as rules this project can implement. No
addresses and no offsets. Each rule says how sure it is - `confirmed`, `likely`, `inferred`
in the fact base's vocabulary. Where this project chooses its own behaviour the paragraph is
marked **Ours**.

Source: `reversing/findings/FINDINGS-builders.md`, read for this spec, and
`reversing/findings/FINDINGS-buildertooltips.md` for the tooltips (section 11). What happens to an
order once it is given is `buildqueue.md`; how an interface file becomes something on screen
is `interface.md`; the screen they come up over is `productionscreen.md`.

**The four windows were built from the reading alone, on 2026-10-08, and the maintainer has
tried them twice since.** What he found is section 10, with what each was. Nothing has been
measured against a screenshot of the original, and the last section says what a closer look
would settle.

## 1. The four, and how one comes up

| window | file | opened by the production screen's | orders |
| --- | --- | --- | --- |
| `division_designer`, with `brigade_picker` beside it | `divisiondesigner.gui` | `build_division_button` | a division of two brigades or more |
| `ship_builder_window` | `shipbuilder.gui` | `build_ship_button` | one ship |
| `air_builder_window` | `airbuilder.gui` | `build_air_button` | one wing |
| `brigade_builder_window` | `brigaderbuilder.gui` | `brig_attach` | one land brigade on its own |

They belong to the production screen, and one is up at a time. `confirmed`

**A builder window stands on the production screen's frame.** Bringing one up hides
everything in the production screen's own window and then shows eight of its elements
again, by name: the icons `bg_countryskin`, `bg_mainshadow` and `production_topitem`, the
button `close_button`, the texts `headline_prod` and `automation_setting`, and the buttons
`mode_arrow_right` and `mode_arrow_left`. The builders' own art is nearly all transparent
and is drawn to go over exactly that. Closing the builder brings the screen back.
`confirmed` for what is shown; that closing restores it is how the game is remembered.

Each window is filled again on every frame it is up. `confirmed`

## 2. What a country may build

A list shows, in the order the game holds its unit types, every type of the window's branch
that **is buildable** (`is_buildable` is not `no`) and that the country **may still have one
more of**. How many more it may have:

    none            if its file says active = no and no technology the country has a level of
                    names it in activate_unit
    none            if its file's usable_by does not name the country
    none            if it has an available_trigger that is false for the country
    no end          if it has neither minimum_of_type nor max_percentage_of_type
    otherwise       the greater of   minimum_of_type - have
                                and  all * max_percentage_of_type - have,     never under none

where `have` is the country's brigades of the type - in its units, in its queue (a line
counting for every unit still to come) and waiting to be placed - and `all` is its brigades
in its units, on the map only, **of the type's group**. `confirmed`, the counting since
2026-10-16 (`reversing/findings/FINDINGS-ministers.md` C.5, the production script asking the
same figure). A type's group, the original's own and hardcoded: a ship is the carrier's
where it carries aircraft, the heavy cruiser its own, a capital ship the battleship's, a
transport or a submarine its own, any other the destroyer's; a wing is its own; a land type
with no width, or the headquarters, its own; one softer than a half the armour's; one that
may drop, or the mountain or marine brigade, its own; and the infantry's otherwise. So the
alpini are four in every hundred of the infantry's group, not of all land brigades.

**So an elite brigade is rationed.** The base game's `alpini_brigade` has `minimum_of_type =
6` and `max_percentage_of_type = 0.04`: Italy may have six, or four in every hundred brigades
of the infantry's group, whichever is more.

*Ours:* an `available_trigger` is script and is not evaluated yet; a type that has one is
taken to pass.

## 3. A list

Every line is the type **as the country would build it today** - with the country's own
level of each of the type's technologies - and shows its name, its counter (the sprite
`GFX_counter_` and the type's key, if there is one), and its columns. The decimals are cut,
not rounded.

**A land brigade** (`brigade_entry_2` in the designer's picker, `brigade_entry_3` in the
brigade builder):

| element | figure | decimals |
| --- | --- | --- |
| `str` | `max_strength * 100`, in thousands with a `k` from a thousand up | |
| `org`, `cw` | `default_organisation`, `combat_width` | 0 |
| `sa`, `ha`, `aa` | soft, hard and air attack | 0 |
| `piercing`, `armored` | `ap_attack`, `armor_value` | 0 |
| `def`, `tou`, `ad` | defensiveness, toughness, air defence | 0 |
| `sof` | `softness * 100` and `%` | 0 |
| `speed` | `maximum_speed`: whole, or two decimals for one under 2 with a fraction over 0.09 | |
| `sup`, `off` | suppression, officers | 0 |
| `sc`, `fc` | supply and fuel consumption | 2 |
| `ic`, `mp` | what one costs the country a day, and in men | 2 |
| `time` | the days one takes the country | whole |

**A ship** (`build_ship_entry`): `org`, `con_a`, `sea_a`, `sub_a`, `air_a`, `shore`,
`sea_d`, `air_d`, `sur_det`, `air_det`, `sub_det`, `visibi`, `trans`, `range` and `off`
with no decimals; `distance` as `distance * 100` with none; `speed` as above; `hull`, `sc`,
`fc`, `ic` and `mp` with two; `time` whole; `pos` as `positioning * 100` with one and `%`.

**A wing** (`build_air_entry`): `org`, `hard_a`, `soft_a`, `sea_a`, `air_a`, `strat_a`,
`surf_d`, `air_d`, `sur_det`, `air_det`, `trans`, `range` and `off` with no decimals;
`speed`; `sc`, `fc`, `ic` and `mp` with two; `time` whole.

All `confirmed`. The three costs are `buildqueue.md`, section 2, for one of the type with
the levels it would be built with - so the hundredth a level is in them.

**Sorting.** A column's button is `sort_` and the column's element (`sort_sa`); `sort_prio`
sorts by the type's `priority`, which no column shows. **The first click on a button sorts
largest first, the next smallest first**, each button keeping its own turn, and equals stay
in the order the game holds them. A list starts by priority, largest first. The sort goes
by the brigade's own figure: the `ic` column sorts by the type's cost, not by what the
country would pay. `confirmed`

**A land brigade's line is coloured by combined arms group**: its button takes a sprite's
name and the number of the type's group, counted from one, when there is a sprite of that
name - `GFX_divdesigner_listbutton` in the designer's picker, `GFX_brigade_listbutton` in
the brigade builder, five of each in the base game. A ship's and a wing's lines are all one
colour. `confirmed`

## 4. The division designer

**The picker is drawn over the designer.** They are two windows, shown and hidden together,
and the designer's art is a tint across the whole of its window, the picker's half
included: under it the picker is greyed and takes no click. *Ours* for the order - that the
original draws the picker last was not read, and nothing else can be so.

**Putting one together.** `select` on a line of the picker adds that brigade while the
design has fewer than the most a division may have:

    most = BRIGADES_IN_DIVISION + what the country's technologies give in division_size

Four in the base game, five with `superior_firepower`. **The design is kept in the order of
its types' priorities, largest first**, equals in the order they were clicked - so its first
brigade, which the division is named for (section 8), is the one of the highest priority.
`brigade_queue` shows the design, a `brigade_entry` each with its name, its counter and a
`cancel` that takes that brigade out. `confirmed`

**Its figures**, each brigade as the country would build it today:

| element | what | decimals |
| --- | --- | --- |
| `size_value`, `allowed_value` | how many brigades, and the most | |
| `str_value` | the strengths added up, as men (below) | |
| `org_value` | organisation, **averaged** | 0 |
| `cw_value`, `sa_value`, `ha_value`, `aa_value`, `def_value`, `tou_value`, `ad_value`, `sup_value` | added up | 0 |
| `sc_value`, `fc_value` | added up | 2 |
| `sof_value` | softness **averaged**, times 100, and `%` | 0 |
| `speed_value` | **the slowest** brigade's: whole if it is a whole number, else two decimals | |
| `piercing_value`, `armored_value` | **the best** brigade's | 0 |

An average is the sum over the count, cut. `confirmed`

**Men**: a hundred to the point of strength. Under a thousand as it is; under a hundred
thousand with a comma before the last three digits (`7,000`); under two million in
thousands with a `K`; above in millions to two decimals with an `M`. `likely`

**Combined arms.** `common/combined_arms.txt` lists the groups, each with a `value` and
some with `base = yes`; a unit type's `unit_group` names its group. For the design:

    a group counts   if a brigade of the design is in it
                     and some brigade of the design is in a base group
    bonus = for each group that counts: its value + what the country's technologies
            give under the group's own name

`combined_arms_indicator_` and the group's number shows that frame of its strip when the
group counts and the frame as many places further on - the greyed half - when it does not.
`combined_arms_bonus_value` is the bonus times 100 with no decimals and `%`. **A design with
no brigade of a base group - infantry, in the base game - shows no bonus at all.**
`confirmed` Whether combat applies the same rule was not read.

**What building it teaches.** `on_complete_box` holds an `on_complete_entry` for each
brigade: `on_complete_icon` takes the sprite `icon_` and the key of the brigade's practical,
and `on_complete_impact` is `+` and the type's `completion_size` with only the decimals it
needs (`+0.2`). `likely`

**Ordering.** `accept` is disabled unless the design may be built:

    from two brigades to the most
    every brigade's type buildable
    for each type n times in the design: at least n * serial * parallel more of it allowed

and, like every window's, when the country has not the men (section 6). **A division of one
brigade cannot be ordered here.** `confirmed`

## 5. Templates

A country's file gives it divisions ready to order under `default_templates`: a block a
template, holding its brigades' types. The designer has twelve buttons,
`divdesign_button1` to `12`; each template takes one, in order, with the text of the
template's name (a localisation key). A button puts its template's brigades in the design,
in the order the file has them, and shows frame 2 while it is the one on show.
`save_template` is shown only then, and keeps the design as that template. `confirmed`,
except what saving does, which is `inferred` from the command it posts.

**The spare buttons are not hidden**: Germany has ten templates, and its last two buttons
keep the text the interface file gives them and do nothing.

## 6. The order panel

All four windows have it, by the same element names in each one's own file.

- **`serial_value`** is how many units a line builds one after another; **`paralell_value`**
  how many lines the order makes. Both start at 1. `serial_plus`, `serial_minus`,
  `parallell_plus` and `parallel_minus` - the spellings are the files' - change them by
  one, or by ten with Ctrl held: `likely`, on the word of the buttons' own tooltip
  (section 11) - the key the code asks was not traced, and this said shift until that was
  read. **A button held down acts at once, again
  after 0.3 s, and then every 0.1 s.** A minus button is disabled under 2 and a plus button
  at 99; a step of ten down stops at 1. `confirmed`
- **`ic_cost_value`** is one unit's IC a day times `parallel`; **`mp_cost_value`** one
  unit's men times `serial * parallel`; both to two decimals. **`build_time_value`** is one
  unit's days times `serial`: up to 365 as the number, a space and the text of `DAYS`, and
  above as the days over 30, a space and the text of `MONTHS`. `ic_cost_label` is the text
  of `PW_IC_COST`. `confirmed`
- **The light.** `selected_brigade_green`, `_orange` and `_red`, one shown:

      spare = the IC the production share has - the cost of every line of the queue
      green    if spare - what this order costs a day >= 0
      orange   else if spare > 0
      red      otherwise

  `confirmed`
- **`accept`** reads `START_PRODUCTION`. It is disabled with nothing chosen, when the
  country's manpower is under `mp_cost_value`'s figure, and for each window's own reasons.
  It posts the order and closes the window; `cancel` closes it. `confirmed`

## 7. The ship, air and brigade builders

Each is a list (section 3), a panel for the type picked, and the order panel. Picking a
line fills the panel: a label, a picture, one line of the type's figures in `stats_pos` -
the same columns as the list - and `on_complete_icon` and `on_complete_impact` as the
designer's.

**The label and the picture** (`reversing/findings/FINDINGS-messagegoto.md`, section 3). The model is the one the
country would build today (`CHistoricalModels.PickCurrent`); the label is `(§Y` + its name + `§W)`, with `+*` after the
name where the country's technologies are not exactly that model's, and the type's own name where it has no model.
The picture is the model's, as the brigade card's: `<TAG>_<type>_<n>.tga`, `<type>_<n>.tga`, `<type>_0.tga` in
`gfx/models`, the first that exists. **With nothing picked the label and the description are emptied and the picture
stays what it was**; where no file exists it stays too. The brigade builder's `selected_brigade_description` lists the
brigade's technologies, a line each, as the unit panel's summary does. The designer's `selected_photo` is its first
brigade's, and an empty design leaves it. `confirmed`, but that the list's first is the first brigade added, `likely`.

**A ship is the one thing whose equipment is chosen.** Picking a type sets a level for each
of its technologies - the country's own - and lists them in `models`, a
`shipbuild_model_entry` each with its `title`, the level chosen in `name`, and a `select`
that opens `shipbuild_model_selection`: the technology's name, and a
`shipbuild_possible_model_entry` for **every level from nothing to the country's** - its
`name` the level's own, as a brigade's summary names one (`NO` at nothing; the country's
text, the game's, or `Model` and a year), and its `effect` what that many levels do for this
type of ship alone, each figure on its own line, so that nothing is the short name and no
figure [exe] - the one chosen marked by `current_model`, each with a `change` button that takes that level and
closes; `back_button` closes without. **A ship may be ordered at any level its country has
reached or below, technology by technology, and each level left out makes it a hundredth
cheaper and quicker.** In the `stats_pos` line a figure that the chosen levels have changed
from what the country would build today is written in yellow. `confirmed`, the two entries'
texts apart.

**A carrier may be ordered with its air groups.** `add_cag` can be ticked for a type with a
`carrier_size` of 1 or more (`likely`); with it ticked, each line ordered is followed by one
more order for each air group the carrier holds - `carrier_size` of them - of the first unit
type with `is_cag`, the same number one after another. Each air group is its own line of
the queue. `confirmed`

**A wing is built with the equipment its country has.** There is nothing to choose.

**A land brigade may be ordered as reserves**: `as_reserves`, as the designer has.

What each orders, `parallel` times:

| window | the order |
| --- | --- |
| designer | the design, `serial` of it, reserves or not |
| ship | the ship at the levels chosen, `serial` of it |
| air | the wing, `serial` of it |
| brigade | the brigade, `serial` of it, reserves or not |

`confirmed`

## 8. What a new unit is called

A country's file lists names by unit type under `unit_names`. **The names under a brigade
type are names for divisions.**

- **A unit of more than one brigade is named**, from the list of its first brigade's type:
  once round the list from where the country last left off, taking the first name that
  nothing in the game is called, and remembering the place. When the list has none left,
  the text of `DIVISION_NAME` with a number counted up from 1 until the name is free.
- **Every brigade, ship and wing is named.** A ship or a wing takes a name of its own
  type's list the same way, and then `SUBUNIT_NAME` with a number. **A land brigade never
  looks at a list** and goes straight to `REGIMENT_NAME` with a number.
- **A unit of one** - any ship, any wing, a brigade ordered alone - has no name of its own
  and takes its brigade's when it is finished (`buildqueue.md`, section 6).

The patterns hold `$NUM$`, the number; `$ORDER$`, the text of `ST`, `ND` or `RD` for a
number ending in 1, 2 or 3 and of `TH` for any other; and `$TYPE$`, the text of the type's
key and `_short`. In English `DIVISION_NAME` is `$NUM$$ORDER$ $TYPE$ Division`: `1st Inf
Division`. **Only 11, 12 and 13 are spared**: the original's 111th is `111st`.

"In use" is **one set a country** (`CCountry.UnitNamesInUse`): `RegisterUnitName` adds to the
country's set, read on the listing on 2026-10-17 (`FINDINGS-theatres.md`, section K), where this
said one set for the whole game until then. The start save bore it out: with one set for the game,
Chile's theatre headquarters came out `2nd Theatre` where the save has `Santiago HQ`. The three routines are
otherwise `likely`, read in decompiled C.

## 9. Ours

- **The order is refused where the windows would not have let it be given.** The original's
  commands ask nothing - a window disables its button, and that is all the refusing there
  is. Here `COrderUnitCommand` refuses a type that is locked, not the country's to use or
  past its ration, more land brigades than a division may have, and equipment better than
  the country's. A lone brigade is not refused for being alone: the brigade builder orders
  exactly that.
- **The ship, air and brigade builders ask the ration too.** In the original only the
  brigade builder and the designer do; the others would let an order through that goes
  past it. No type of the base game that is a ship or a wing has a ration.
- **A name is chosen as the order is carried out**, by the simulation. The original's ship
  builder chooses the ship's name on the machine of the player who clicked and sends it with
  the order; the result is the same for one player.
- **Every name the orders of battle give is in use from the start.** That the original
  registers them was not read.
- **A pattern with no number in it is given as it is**, where the original's routine for a
  division would count for ever. It can only happen with no text for `DIVISION_NAME`.
- **The label is the type's name**, and the picture is whatever the interface file gives.
  The original shows the name and the picture of a historical model.
- **The description of a wing or a brigade is left empty.**
- **A list does not cut its lines off at the side.** The files give lists 533 wide lines
  whose columns run past 850, so the original cannot either; it was not read.
- **A ticked box is the second frame of its sprite.** Not read.
- **The windows keep their counts and what is picked while the game runs**, as the
  original's do; a type the country may no longer build is dropped when the window opens.
- **A country can be given every unit to try.** `CUnlockEveryUnitCommand` lifts the two
  locks of section 2 for one country - the technology a type waits for and `usable_by` -
  and nothing else: a type that is not buildable stays out, a ration holds, and the
  country pays. Nothing in a game posts it; `--all-units` on the command line does, for
  the country played, and *Play OpenHOI3 (all units)* in VS Code starts that way. It is
  what a carrier and its air groups were first ordered with, on 2026-10-09: the carrier
  and, after it, two lines of one air group each.

## 10. What the maintainer found, 2026-10-08

Tried by hand, the first time anyone had: the windows had been checked by a developer
switch that opens one and puts something in it, which presses nothing.

| what he saw | what it was |
| --- | --- |
| the designer's picker greyed, and not to be clicked | the picker was drawn under the designer, whose art tints the whole window and takes the clicks (section 4) |
| the brigade builder's lines' ground off centre - "a little too low and left", which he said of the designer's and then "same issue" of these | `brigade_entry_3` gives its button two positions and the first was used; the second stands (`interface.md`, section 2). Four down and three left |
| the ship builder's lines' ground running out past the frame on the right | `select_ship_button` is in the install as a `.tga` and as a `.dds`, two different pictures, and the game draws the `.dds` (`interface.md`, section 5) |
| `cancel`, `accept`, the four serial and parallel buttons and `as_reserves` dead in the ship, air and brigade builders | `stats_pos`, a list box 533 by 620 holding one line, lies over all of them, and a list box took every click in its rectangle (`interface.md`, section 2) |

Each is mended and was pressed with the mouse's own events afterwards - a line picked, the
counts changed, the box ticked, the order given and the window closed, in all four.

He looked again on 2026-10-09 and found all of it right, with one thing more: **a click on
a line's counter picks the line in the original**, and here the counter kept the click to
itself. An icon now hands its click to the button it lies on (`interface.md`, section 5).

**One was never accounted for.** It was the designer's lines he first called too low and
left, and on the second look he did not. Their button has one position, `{ 3 1 }`, and at it a
line is 572 wide under a header 572 wide and starts on the pixel the header's frame does.
Nothing was changed there but the order the two windows are drawn in, so it was most
likely the tint lying over them that made them look so.

**Found on the way:** the brigade builder's lines are coloured by group (section 3), which
the first reading had passed over.

**The ship list with every type in it, 2026-10-09.** With all thirteen ship types to
build the list ran over the gap under its frame, and the maintainer said the game's
never does. That had the list box read (`interface.md`, section 2), and what was wrong
was general: lines were spaced by the box's `spacing` and cut off at its edge, where the
original packs them and draws whole lines only. Mended, every list here changed - the
build queue shows seven lines where it showed six and part of a seventh, the brigade
builder twelve and no half line.

**The ship list itself is not settled.** Its file gives it a height of 620 - twenty-five
lines - in a frame of about 300, so by the routine it never scrolls: twelve ship types
end inside the frame and the thirteenth is drawn across its lower edge. That is how it
is drawn here, and it is not what he remembers. Either the unmodded game does it and is
seldom seen with thirteen types to build - BlackICE's own file gives the list 345, which
is fourteen lines and then a scrollbar - or something else holds it in that was not
found. The air builder's list and the brigade picker's are 620 high as well.

**The picker's scrollbar lies on its lines** when it has one. Its box is 533 wide and its
lines 572, and a list's scrollbar stands at the box's right edge here, so with more land
brigades than the 26 rows the box has - which `--all-units` gives - the bar is drawn over
the columns. The maintainer, 2026-10-09: expected, and the original avoids it by never
having that many brigades to list. Where the original stands a list's scrollbar was not
read (`interface.md`, the last section, item 11), so the bar's place is ours either way.

## 11. Tooltips

Source: `reversing/findings/FINDINGS-buildertooltips.md`, read 2026-10-11. Tried with `--click`;
the maintainer has not looked at them. `confirmed` unless said.

**While a builder window is up it answers for every element** the production screen is asked
about, the screen's frame among them, which it leaves silent (`tooltips.md`, section 5). By the
element's name:

- **a column's header** - `sort_` and the column, and in the designer its totals, `total_` and
  the column - says the column's name, `BUILD_` and the column and `_IRO`, and after a moment
  what it means, the same with `_DRO`: "Strength", then "How many men each brigade is made up
  of." Each window has its own list and its own few spellings: speed is `BUILD_SPEED_DD` in the
  designer and the brigade builder and `BUILD_SSPEED` in the other two, the designer's fuel total
  is `total_oc` as its file names it, and the air builder's surface defence takes the plain
  `SURFACE_DEFENCE` for its name;
- **the four step buttons** say `BUILD_ADD_REM_BUNDLE` with `$NUM$` 10: "Ctrl + LMB to add or
  remove 10";
- in the designer, **`terrainbonus`** says the terrain summary (below), **`combined_arms_bonus_value`**
  `BUILD_CA_BONUS_VALUE`, and **`combined_arms_indicator_` and a number** the group of that number,
  counted from one in `common/combined_arms.txt`, by its name, and after a moment its key and
  `_desc`;
- **anything else says nothing**: a list's lines, the panel's figures, the costs, the light,
  `accept`, the templates.

**The practical** - `on_complete_icon` and `on_complete_impact`, wherever they are - is answered by
the game screen before any window: the practical's name, `TC_BUILD_COST_AND_TIME`, and how much
cheaper and quicker the country builds what it covers (`upkeep.md`, section 6), in yellow, a
percentage to one place, cut: "Infantry Practical reduces building cost and time by §Y12.3%§W".
The windows tell it which practical by keeping the key on the icon and the figure as they fill
them; one with none - the ship builder with nothing picked - says nothing.

**The terrain summary** is worked out over the design's brigades, each as the country would build
it today. For each terrain of `map/terrain.txt`, in its order, and then a river, the night, forts
and a landing:

    for each brigade:   add its own adjuster for it
                        a terrain: add the terrain's own attack and defence
                        the other four: add RIVER_CROSSING_PENALTY, BASE_NIGHT_PENALTY,
                                        BASE_FORT_PENALTY or AMPHIBIOUS_LANDING_PENALTY to the attack
    then each figure over the number of brigades, cut toward nothing

so the terrain's figures and the define come in once, as an average does. Each with anything to
say is a line: the terrain's name, or `RIVER_ADJUSTER`, `NIGHT_ADJUSTER`, `FORT_ADJUSTER`,
`AMPH_ADJUSTER`, then `: ` and its figures separated by `, ` - `UA_ATTACK`, `UA_DEFENCE`,
`UA_MOVEMENT` and `UA_ATTRITION`, each left out at nothing, written as a space, `§R` below nothing
or `§G` otherwise, `+` above nothing, the figure to one place, cut, and `%§W`. **Attack, defence
and movement are times a hundred and attrition is not, and attrition's colour goes the other way.**
Three infantry brigades and an armoured one in the mountains: "Mountain: Attack §R-47.5%§W,
Defence §R-17.5%§W, Movement §R-10.0%§W", by the base game's files and no technology.

**Kept as read**, though they look odd: attrition written without its hundred, so `0.5` reads
`+0.5%`; attrition above nothing red and with a `+`; a terrain's own attrition left out of its
line where its attack and defence are in; and nothing but the headers, the steps and the
designer's few saying anything.

*Ours:* the summary is worked out when it is asked for, where the original keeps it from the last
change to the design; it differs only if the country's technology moves while the window is up. A
group icon numbered past the groups says nothing, where the original reads past its list.

## What is not built

- **Custom game mode**, in which the four price by points.

## What is not established, in one place

1. **Anything a closer look at the running game would show**: where each figure sits
   against its icon, the frames of the checkbox and of the combined arms strip, whether
   the spare template buttons really stay, what the panel shows with nothing picked.
2. **Which key makes a step ten**, in the code: the buttons' tooltip says Ctrl, and Ctrl is what
   is built.
3. ~~**The label and the picture**: how a historical model is chosen for them.~~ Read 2026-11-08 (section 7).
4. **The name of a level of a technology**, and the text of its effects.
5. ~~**The description** the air and brigade builders show.~~ Read 2026-11-08 (sections 7 and 12).
6. **What saving a template does**, beyond the command it posts, and how a country with no
   `default_templates` comes by any.
7. ~~**What "of the type's branch" counts** for a ration.~~ Read 2026-10-16: the type's group (section 2).
8. **Whether a series is named unit by unit.** A line carries one name for its unit and one
   for each brigade; what its second unit is called was not read.

## 12. The air builder's description

*From `reversing/findings/FINDINGS-smallbatch.md`, section 4.* `selected_plane_desc_list` has a row for each technology of the type, `air_desc_entry`: its name, ` : §Y`, the name of the country's level of it. The original asks for the level's name with no country, so a country's own name for a level does not come in. Neither the ship builder nor any other has such a list.
