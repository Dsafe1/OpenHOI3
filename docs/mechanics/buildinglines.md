# Building lines: a building ordered, priced, finished and placed

The build queue's second kind of line: a building, ordered from the production screen or a province, paid for by the
production share like any line, and finished as one more level of the building in its province - or, where it was
ordered with no province, as a building waiting in the deploy menu to be put on the map. Read out of the original on
2026-10-15 (`reversing/findings/FINDINGS-buildingqueue.md`, sections A and B). `confirmed` unless marked.

`buildqueue.md` is the queue and a unit's line; this is what differs for a building. The queue's third kind, the
convoy, is `convoylines.md`.

## 1. The buildings

`common/buildings.txt`, a block a building, in the file's order. What a line asks of a building:

| key | what it is to a line |
| --- | --- |
| `cost` | IC a day, before the discount |
| `time` | days, before the discount |
| `on_completion`, `completion_size` | the technology category whose discount prices it, and the practical a finished one gives |
| `max_level` | the most of it a province may have, built and queued together |
| `capital` | it has a production-screen button, and a line of it with no province is placed later |
| `port` | it needs a province with a port |
| `prerequisites`, `not_if_x_exists` | buildings the province must have, and buildings it must not; neither install uses them |
| `repair` | `no`: the day's repair does not fill it |

**The buttons** of the production screen are the buildings with `capital = yes`, by their keys: in both installs
`air_base`, `naval_base`, `industry`, `anti_air`, `radar_station`, `nuclear_reactor`, `rocket_test` and `underground`.
Forts and infrastructure are built from the province view (`provinceview.md`, section 4).

## 2. Ordering

**The production screen.** A building's button - enabled where the country may build it: its technology has made it
available, and for the underground, `CanTrainUnderground` - opens the **confirm-build dialog**, `confirm_build`
(`interface/confirmbuild.gui`). **It asks for no province.**

- `label` the building's name; `photo` the sprite `picture_` + its key;
- **`serial_value` and `paralell_value`**, both 1 to start: `serial_plus`, `serial_minus`, `parallell_plus` and
  `parallel_minus` step them by 1, **by 10 with Ctrl** (`BUILD_ADD_REM_BUNDLE` "Ctrl + LMB to add or remove $NUM$", NUM
  10); a minus is disabled at 1 and a plus at 99;
- `ic_cost_value` **parallel x the cost a day** (section 3), to two places; `build_time_value` **serial x the days**,
  "N Days" to 365 and "N/30 Months" above (`DAYS`, `MONTHS`);
- one of `selected_brigade_green`, `_orange`, `_red`: the IC the production slider gives less every queued line's cost
  a day is the spare; green where it covers parallel x cost, orange where it is above nought, red otherwise;
- `on_complete_icon` `icon_` + the category's key, `on_complete_impact` `+` and `completion_size` to one place;
- **`accept`**, never disabled, orders `parallel` lines of `serial` buildings each, **with no province**; `cancel`
  orders nothing.

**A province** (the province view, `provinceview.md` section 4; and the AI's production minister, which always names one): one level
a click, for each selected province the player holds where the building may go (section 6).

**The order** - the original's `CConstructBuildingCommand`: the country, the building, the province or none, and how many:

- **valid** where the building is real, and with a province, where the building may go there (section 6); **with none,
  always** - a production-screen order is never checked;
- **with a province**, it joins the **last** line of that building in that province, its size raised to at most what
  `max_level` leaves - **not priced again**;
- otherwise a **new line at the end of the queue**, of the size asked, **with no cap**, priced;
- nothing is paid and nobody is told.

## 3. The price

    d    = the country's discount in the building's on_completion category
    cost = cost x (1000 - d) / 1000                        a day, thousandths, truncated
    days = time x (1000 - d) / 1000                        whole days, truncated

**`d` is the same discount a unit's brigades take in their category** (`production.md`): the category's practical
against an expected 5.0, plus the country's `industrial_efficiency` and `ic_efficiency`, at most 990. **Nothing else**:
no levels term, no by-type term, no build-speed modifier, no floor but the 990, no reserves, and **the line's size plays
no part** - several of one building on one line cost one building's day each, built one after another.

Their Finest Hour's categories: `construction_practical` for the bases, the forts, industry and infrastructure;
`artillery_practical` anti-air; `electornicegineering_practical` radar; `militia_theory` the underground; `nuclear_bomb`
the reactor; `rocket_practical` the rocket test site.

A line is priced when it is made, **again every day a line of the queue is finished**, as a game is entered and when a
law changes (`buildqueue.md`, section 2).

## 4. The day

**As a unit's line** (`buildqueue.md`, section 3): its need is its cost; it is paid in the queue's order, from the share
and then from resources; what the pool does not cover is lost. **No gearing**: a building line has no run of one type.

**First, every day, a building line is asked whether it may still be built**, and where not it is taken out of the
queue, with nothing said and nothing given back:

- a line with a province, of anything but the underground, **whose province the country no longer holds**;
- an underground with no province, **the first day the country may not train one**;
- an underground with a province where the province **has one already, or may no longer take one**
  (`undergrounds.md`, section 2).

## 5. Finished

When a line's progress reaches its days, **one building of it is finished**:

1. Nothing for a country that owns nothing, but the rebels.
2. **The practical**: the building's `completion_size` into its `on_completion` category, in full, **not scaled by
   anything** - before anything else, so a line that builds nothing pays it out.
3. **With no province** - a `capital = yes` building - **it waits in the deploy menu** (section 6), and nobody is told.
4. **With a province**, **one more level of it built** - at most `max_level` - **and none of it working**: the day's
   repair fills it, 0.1 a day times (100 + roads) / 100, about **ten days on full roads**, ninety with none, and the
   province's modifier takes it in as it fills. The underground alone starts with its strength (`undergrounds.md`,
   section 2). **`BUILDING_DONE`** "$NAME$ has been built in $PROV$." to the player **only where he holds the province**.
5. Its progress less its days; one fewer to build; **priced again for the next where more are left**.

**Every line of the queue is priced again** - and the country's modifier made again first - on any day a line is
finished and taken out, a unit's or a building's.

**A port or an air base a line builds is not among its area's harbours or air bases** until the area is made again; here
the areas are made whole whenever they are asked for (`supply.md`), so it is at once.

## 6. Placing

**Where a building may go** - the original's `IsBuildingAllowed`, for the country:

- the building is available to it;
- **it holds the province** - not asked for the underground;
- the province's built levels and every queued line of the building there come to **below `max_level`**;
- a `port` building needs a port;
- none of `not_if_x_exists` is there and all of `prerequisites` are.

**A building waiting to be placed may go** where that holds, and the province has a port where the building needs one,
and:

- **the underground**: none there, and the underground's own rules (`undergrounds.md`, section 2);
- anything else: **the country holds the province**; **a naval base only where there is none**, **an air base only where
  there is less than one level**.

**The deploy menu** lists it after the units, in the order it was finished: its name, no counter, no tooltip. Chosen,
**the map shows where it may go** - grey (0.5, 0.5, 0.5) everywhere, (0.5, 0.9, 0.5) where it may, and for the
underground the green by its initial strength, (0.5, 0.6 + 0.3 x initial / 0.4, 0.5) - painted once as it is chosen. **A
left click on a province where it may go places it**; a click elsewhere does nothing. Placed: one more level built and
none working, as a finished line; a naval base making a port at once among its area's harbours; and `BUILDING_DONE` to
the province's holder.

## 7. The line on the production screen

- `counter_type` the sprite `icon_build_` + the building's key - none for the underground, which keeps the file's;
- `unit_name` the building's name, and " (" + the province's name + ")" where it has one;
- `unit_target` not written, `original_country` hidden;
- the rest as a unit's line: `production_queue_buildings`, `series_info` "+size-1", the cost, the date it is due, the
  progress;
- **its tooltip** is empty over the counter, the series and the target; elsewhere the status sentence, as a unit's;
- the five buttons as a unit's.

## 8. Against the saves

A save keeps each building line as `building_construction` in its country's block - its cost, its days, its progress,
its province, and the building numbered **from one** in the file's order. `OpenHOI3.SaveCheck research` prices each line
of the earlier save under that save's practicals, laws, ministers and technology and sets it beside what the save keeps.
On 2026-10-15: **21 of 21** for the saves of 4 and 7 January 1936, **22 of 22** for 8 January, **23 of 23** for 1
February, cost and days. The autosave of 1 April gives **26 and 27 of 28**: France's naval base and Italy's air base
keep the prices the January saves agree with, because neither country has finished a line since and its practical has
moved. That is the original pricing again only on a day a line finishes, which the check cannot see.

## What looks wrong and is built so

- A production-screen order has no province and is never checked; only the button's being disabled stands in the way.
- A new line's size is never capped; a province order is capped only when it joins a line.
- A province order joins the last matching line, which is not priced again.
- A line's price ignores its size; its days are truncated.
- `accept` is never disabled, and its colour weighs parallel and not serial.
- A finished building does not work on the day it is finished.
- A building's practical is never scaled by anything.
- A deployed naval base or air base can only be a new one.
- A line in a province its country no longer holds goes silently; so does an unplaced underground the day its country
  may not train one.
- `BUILDING_DONE` goes to the province's holder, not the builder, and placing from the menu tells the placer.
- The placing map is painted once a line is chosen.

## Ours

- **A building is available to a country where a technology it has at level 1 or more names it in
  `activate_building`**, as a unit is; the original keeps a list of available buildings that such a technology fills,
  and the reading took its filling for that (*inferred*).
- **The deploy menu lists the buildings after the units**, each kind in the order it was finished; the original keeps
  one list of both, in the order of finishing.
- **The unit being placed still tints the map our way**, green over the mode's colours; the original paints the same
  grey and green for a unit as for a building (`mapunits.md`, section 6) - left so until the maintainer has looked.
- **The confirm-build dialog's buttons do not repeat while held**; the original repeats after 0.3 s and then every 0.1 s
  (*inferred*). The share the colour weighs is the production share times the country's IC; the original's "factor" in
  it was not read.
- **A line's number is taken from the count units and brigades take**, and a building waiting is numbered from its
  line's, as the original numbers it.
- **A port a line or a placing makes is among its area's harbours when the supply network is next made whole** - on a
  change of hands anywhere - where the original adds a placed naval base at once and a line's at the next touch of the
  area.

## Not built

- **The AI**, whose production minister orders every building with a province. The province view's building buttons,
  the player's way to forts and infrastructure, are built (`provinceview.md`, section 4).
- **BlackICE's history lines of buildings** (`history/units`, `building_construction`), which the order of battle's
  reader reports and passes over; Their Finest Hour has none.
- ~~Convoys, the queue's third kind of line.~~ Built 2026-10-15: `convoylines.md`.

## Not established

- That a new level comes in at nought working level and how long it takes to fill, against a running game: no save
  catches one part built.
- The province view's building button's enabling.
- The confirm-build dialog's small things: the Ctrl key, the steppers' repeat, how it is taken down.
