# The production screen: what goes in each element

What the original's production screen writes into its elements, written as rules this
project can implement. No addresses and no offsets. Each rule says how sure it is -
`confirmed`, `likely`, `inferred` in the fact base's vocabulary. Where this project
chooses its own behaviour the paragraph is marked **Ours**.

Source: `reversing/findings/FINDINGS-countryviews.md`, section 4, read for this. **Nothing
in it was checked against a running game**; the formats are from the original's own
format strings and the tests hold them.

What the screen shares with the others the top bar opens is `countryviews.md`; its six
sliders are `sliders.md`; what the shares do is `production.md`. Colours are written `§R`,
`§G`, `§Y` and `§W`: the escapes for red, green, yellow and white.

## 1. The window

`confirmed` The screen is the window `country_production` of
`interface/country_production.gui`. The code finds these by name:

| kind | names |
| --- | --- |
| text boxes | `energy_value`, `metal_value`, `rm_value`, `oil_value`, `fuel_value`, `supplies_value`, `money_value`, `nukes_value`, `ic_value`, `transports_value`, `escorts_value`, `automation_setting` |
| icon | `bg_countryskin` |
| buttons | `close_button`, `mode_arrow_left`, `mode_arrow_right`, `build_division_button`, `build_air_button`, `build_ship_button`, `brig_attach`, `convoy`, `escort`, `underground`, `convoy_button`, `theatres_calc_button`, `theatres_assign_button`, `auto_deployment_checkbox`, and **one named after each building** (section 5) |
| list boxes | `build_queue`, `ai_need_queue`, `trade_list`, `convoy_queue` |
| box of overlapping elements | `strat_resources` |

and six windows of the type `domesticpanel_slider`, put at the `positionType`
`domestic_eco_slider_start` and each next one further by `domestic_eco_slider_offset`.

The headline, the labels beside the figures and the pictures are found by nothing: a text
box shows the text of the key its file writes in it.

## 2. The seven resources

`confirmed` throughout. Each is one line: **what the country holds and, in brackets, what
the day did to it**, both in whole units, cut off and not rounded. The original's own
format is `%i (%s%i§W)`.

    change = going out - coming in

over the same ledgers the top bar reads (`topbar.md`, section 2), with what is traded for
counted as coming in.

| the day | the bracket | example |
| --- | --- | --- |
| brought more than it took | `§G+` and the gain | `26820 (§G+459§W)` |
| took more than it brought | `§R` and the loss, **with no minus sign** | `7169 (§R9§W)` |
| neither | `§Y0` | `0 (§Y0§W)` |

So a falling stock is a red number and a rising one a green number with a plus; the sign
is carried by the colour. **That was confirmed of the running game on
2026-10-08.** A change of less than one unit a day is written as a nought in
the colour of the way it goes.

The stock is the capital's pool, or the country's own for a government in exile, as on
the top bar. There is no `K` here: the figure is written in full.

*Ours:* since 2026-11-15 the bracket is the top bar's own sums - the day's expense less its income with what was
traded for, every ledger the top bar reads (`CTopBarText.DayOut` and `DayIn`) - where until then it summed five
ledgers out and four in and left trade, debt, tribute and what exiles hand over out of it. The supplies and fuel taken
out of the capital are in the usage (`supply.md`, section 11).

## 3. IC, bombs and convoys

`confirmed` **`ic_value`** is the country's IC, a plain whole number: the middle figure of
the top bar's three.

`confirmed` **`nukes_value`** is the bombs the country holds, to two decimals.

`confirmed` **`transports_value`** and **`escorts_value`** are how many the country has,
followed by ` (+N)` when `N` are being built and by nothing when none are.

*Ours:* nothing makes a bomb yet, so the first reads `0.00`. The other two are the
transports and escorts the country has on no convoy (`convoys.md`, section 3), and `N` the
ships the queue's convoy lines still have to give, ten an item (`convoylines.md`, section 4).
That "how many the country has" means the free ones and not those on convoys as well is
`inferred` from the two counts the original keeps.

## 4. The strategic resources

`confirmed` The box `strat_resources` holds one small window, of the type
`production_strat_resource`, for each strategic resource the country has the use of any of
(`modifiers.md`, section 6). The window's icon `strat_icon_in_prod` is given the sprite
named `GFX_` + the resource's key + `_small` - **if there is a sprite of that name**; with
none it keeps the one its file gives it.

## 5. The buttons

`confirmed` **A building's button is found by the building's own key.** For every building
whose file says `capital = yes`, the window is asked for a button of that name -
`industry`, `anti_air`, `radar_station`, `nuclear_reactor`, `rocket_test`, `underground`,
`air_base`, `naval_base`: the eight the base game marks so, and the eight its file has
buttons for - and if there is one it is enabled when the country's technology allows the
building and disabled when it does not. A mod's building gets a button by being given one
of its name.

`confirmed` `theatres_calc_button` is hidden whenever the screen is drawn. `escort` is
disabled until a technology allows escorts - one whose file says `allow_escorts = yes`.

`confirmed` **Four buttons order units, each by bringing up a window of its own**:
`build_division_button` the division designer, `build_ship_button` the ship builder,
`build_air_button` the air builder, and `brig_attach` the brigade builder. Each puts away
everything in the screen's own window but its frame - the country's picture, the shadow,
the top piece, the close button, the headline and the automation line with its arrows -
and the window comes up over that. `builders.md` has the four. Built 2026-10-08.

The building buttons open the confirm-build dialog (`buildinglines.md`, section 2), and so
do `convoy` and `escort`, for transports and escorts (`convoylines.md`, section 1). *Not
read:* what the theatre buttons and the checkbox do. *Ours:* `theatres_calc_button` is hidden
and `escort` disabled as above; the rest are drawn as their files have them and do nothing.

## 6. The four lists

`confirmed` `build_queue`, `ai_need_queue`, `trade_list` and `convoy_queue` are each made
again when their length is not the model's, and otherwise have each line filled.

### A line of the build queue

`confirmed` Each line of `build_queue` is a window of the type `queue_entry`, for one line
of the country's queue (`buildqueue.md`, section 1). Source:
`reversing/findings/FINDINGS-buildqueue.md`, section 8.

| element | what goes in it |
| --- | --- |
| `production_queue_green`, `production_queue_orange`, `production_queue_red` | one of the three: green for a line paid more than 0.990 of a day the last time the queue was worked through, orange for one paid anything, red for one paid nothing |
| `production_speed` | what it was last paid, as a whole percentage: `100%` |
| `ic_cost` | what a full day of it costs, to two decimals, cut, with the text of the key `IC` straight after: `7.02IC` |
| `eta_date` | the day it will be done at the pace it was last paid, or `---` for a line paid nothing |
| `series_info` | `+` and how many more of the unit follow, for a series; nothing for a line of one |
| `prodqueue_progress` | how far the one being built has got, as a whole percentage |
| `production_queue_naval`, `production_queue_division`, `production_queue_air`, `production_queue_buildings` | one of the four, by what the line builds |
| `counter_type` | the sprite `GFX_counter_` and the first brigade's type key |
| `unit_name` | the line's name, or its first brigade's where it has none |
| `unit_target` | `=> ` and the name of the unit the line is built for |
| `original_country` | a shield, for a line built for another country |

and five buttons: `cancel`, `max_prio`, `prio`, `non_prio`, `max_non_prio`.

**The date:** a line no day has yet worked on is taken to be paid in full.

    left = (duration - progress) / what it was last paid          a part of a day counts as a whole one
    the date that many days on

**The text of `IC` is put straight after the number, with nothing between.** The German
table gives the key a space of its own, ` IK`; the English has none. `confirmed`

*Not read:* what the five buttons post, and how the date is written. *Ours:*

- **The buttons** cancel the line and move it to the front, one place towards it, one
  place back and to the back (`buildqueue.md`, section 7), which is what their sprites are
  called: top, up, down, bottom.
- **The date** is written as the research queue's is, year first
  (`technologyscreen.md`, section 6).
- `unit_target` is empty and `original_country` hidden: nothing is built for a unit in
  the field or for another country.

### The other three

*Not read:* the lines. *Ours:* all three are empty: no AI asks for anything, and there is
no trade and no convoy.

## 7. Ours

- **The screen is opened by the top bar's Production button**, or with
  `--screen=production` after `--` on the command line.
- **What each element says is worked out in `OpenHOI3.Interface`** - `CProductionScreenText`,
  `CBuildQueueText` and `CSliderText` - and tested. `src/Screens/CProductionView.cs` finds
  the elements and sets them, and `CBuildQueueEntry.cs` those of a queue's line.
- **`--order=type,type`** after `--` on the command line orders one unit of those brigades,
  ships or wings for the country played, each called by its type's own name. And
  **`--builder=division:type,type`** - or `ship:`, `air:`, `brigade:` and one type - opens
  the window that orders one with that in it; an exclamation mark at the end gives the
  order as well.
- **`slider_automation_setting`** and the two arrows beside it are drawn as their file has
  them; what the original writes there was not read.

## 8. Tooltips

Source: `reversing/findings/FINDINGS-productiontooltip.md`, read 2026-10-11. Tried with `--click`;
reviewed in play on 2026-10-11: "Looks good". How a tooltip is shown is `tooltips.md`; the sliders' own
are `sliders.md`, section 6. `confirmed` unless said. By the element's name, all in the first half:

| element | what it says |
| --- | --- |
| `energy_icon`, `_label`, `_value`, and the same for `metal`, `rm`, `oil`, `supplies`, `money`, `fuel` | that good's day, as the top bar's number of it says (`topbar.md`, section 14) |
| `strat_icon_in_prod` | the resource, below |
| `nuke_icon`, `nukes_label`, `nukes_value` | `NO_NUKE_PRODUCTION` where the country's technology makes no bombs, else `NUKE_PRODUCTION` with what it makes, two places |
| `theatres_calc_button`, `theatres_assign_button` | `THEATRE_RECALC_DESC`, `THEATRE_ASSIGN_TROOPS_DESC` |
| `escort` | `LEDGER_ESCORTS`; while the button is disabled, a line more: `NEED`, ` §Y` and the name of the first technology that allows escorts |
| `convoy` | `CONVOYS` |
| `underground` | only while the button is disabled: why it may not be used |
| a building's button | the building's name and a line break; while the button is disabled, `NEED`, ` §Y` and the technology that unlocks it |
| a line of the build queue | below |

Nothing else on the screen says anything - not the IC figure, the counts of transports and
escorts, nor a slider's title, value or need.

**A strategic resource** is `STRAT_DET_RES_TT` - in English its name, a line, what the country has
of the resource's modifier a line a value, a line, and **one line for each province whose resource
reaches the country**:

    "@" + its controller's tag + "  " + its name + " - " + colour + share * 100 to two places + "%§W"

    standing = infrastructure * (1 + local + global infrastructure), held between 0.01 and 1
    share    = standing / what it was built to, or -1 where it was built to nothing
    colour   = §R under 0.330, §Y under 0.990, §G from there

The provinces are those of the resource whose controller gives the country its resources
(`modifiers.md`, section 6), for a country that holds land. **Kept as read**: a province's line
leaves out the flat term its yield has, and one with nothing built reads `-0.10%` in red.
`inferred`: the provinces come in the order the map numbers them.

**A line of the build queue** answers by which of its elements is under the mouse:

| element | first half | second half |
| --- | --- | --- |
| `cancel` | `PEN_CANCEL_PRODU` | `PEN_CANCEL_PRODU_DRO`, a blank line, `CANCEL_MULTI_CONSTRUCTIONS_DESC` |
| `max_prio`, `prio`, `non_prio`, `max_non_prio` | `PEN_MAX_PRIO`, `PEN_PRIO`, `PEN_NON_PRIO`, `PEN_MAX_NON_PRIO` | |
| `counter_type`, `production_queue_division`, `production_queue_air`, `production_queue_naval`, `series_info`, `unit_target` | one line per brigade, `£` + its type's key + ` ` + its name, and `BRIGADE_MIL_CON_CUMULATED_EXP` with the experience gathered, two places | |
| anything else | `PEN_FULL_SPEED` for a line paid a full day, `PEN_PARTIAL_PROGRESS` with the part as a whole percentage, cut, or `PEN_NO_PROGRESS` | |

`£` and a name draw a unit's counter (`tooltips.md`, section 7). A line that upgrades a brigade
says `BRIGADE_UPGRADE_MIL_CON_INFO` instead; nothing upgrades through the queue here.

*Ours:*

- **A building's button opens the confirm-build dialog** since 2026-10-15, enabled where a technology makes the
  building available, the underground's where the country may train one (`buildinglines.md`, section 2). The escort
  button is disabled until a technology allows escorts, as
  section 5 says it is - `allow_escorts = yes`, which the technology files are now read for.
- **A text box lets the mouse through** to what is under it, as before: over `series_info` it is the
  line's background that answers, which is the speed and not the brigades.
- ~~**The trade list's and the convoy list's lines, and the AI's needs**, are not read and are empty~~ - built since:
  `trade.md`, `convoywindows.md`, `theatresscreen.md`.

## What is not established, in one place

1. **Little was seen running.** One thing was confirmed on 2026-10-08: a
   falling resource is a red number with no minus sign. The rest is from the bytes.
2. **What the buttons do**: the theatre buttons and the checkbox - and the five on a line
   of the build queue. The building, convoy and escort buttons are read (`buildinglines.md`,
   `convoylines.md`). The four that order units are
   read (`builders.md`).
3. **The lines of three of the four lists.** The build queue's are read (section 6), all
   but how its date is written; **none of it has been looked at beside the running game**.
4. ~~**`slider_automation_setting`** and its arrows.~~ Read and built (`frontend.md`, section 4.4).
5. ~~**The trade list and the convoy list**, whose builders were found and not read.~~ Read and built (`trade.md`,
   `convoywindows.md`).
6. **The tooltips of the trade and convoy lists' lines**, and of the builder windows, which the
   screen asks first (section 8).
