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
is carried by the colour. **The maintainer confirmed that of the running game on
2026-10-08.** A change of less than one unit a day is written as a nought in
the colour of the way it goes.

The stock is the capital's pool, or the country's own for a government in exile, as on
the top bar. There is no `K` here: the figure is written in full.

*Ours:* as on the top bar, four of the thirteen ledgers move anything yet.

## 3. IC, bombs and convoys

`confirmed` **`ic_value`** is the country's IC, a plain whole number: the middle figure of
the top bar's three.

`confirmed` **`nukes_value`** is the bombs the country holds, to two decimals.

`confirmed` **`transports_value`** and **`escorts_value`** are how many the country has,
followed by ` (+N)` when `N` are being built and by nothing when none are.

*Ours:* nothing makes a bomb, a convoy or an escort yet, so the three read `0.00`, `0`
and `0`.

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
disabled until a technology allows escorts.

*Not read:* what any of the buttons does. *Ours:* `theatres_calc_button` is hidden; the
rest are drawn as their files have them and do nothing - there are no units to build and
no buildings to place.

## 6. The four lists

`confirmed` `build_queue`, `ai_need_queue`, `trade_list` and `convoy_queue` are each made
again when their length is not the model's, and otherwise have each line filled.

*Not read:* the lines. *Ours:* all four are empty: nothing is built, no AI asks for
anything, and there is no trade and no convoy.

## 7. Ours

- **The screen is opened by the top bar's Production button**, or with
  `--screen=production` after `--` on the command line.
- **What each element says is worked out in `OpenHOI3.Interface`** - `CProductionScreenText`
  and `CSliderText` - and tested. `src/Screens/CProductionView.cs` finds the elements and
  sets them.
- **`slider_automation_setting`** and the two arrows beside it are drawn as their file has
  them; what the original writes there was not read.

## What is not established, in one place

1. **Little was seen running.** The maintainer confirmed one thing on 2026-10-08: a
   falling resource is a red number with no minus sign. The rest is from the bytes.
2. **What the buttons do**: the three unit builders, the building buttons, the convoy and
   theatre buttons, the checkbox.
3. **The lines of the four lists.**
4. **`slider_automation_setting`** and its arrows.
5. **The trade list and the convoy list**, whose builders were found and not read.
6. **Tooltips.** The screen's builder was found long ago
   (`reversing/findings/FINDINGS-uinumbers.md`) and none is shown here.
