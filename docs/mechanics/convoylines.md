# Convoy lines: transports and escorts ordered, priced and delivered

The build queue's third kind of line, after a unit's (`buildqueue.md`) and a building's (`buildinglines.md`): ten
transports or ten escorts at a time, ordered from the production screen, paid for by the production share like any
line, and added to the country's free transports or escorts when each is done - the pool its convoys are manned from
(`convoys.md`). Read out of the original on 2026-10-15 (`reversing/findings/FINDINGS-convoylines.md`, sections A and
B). `confirmed` unless marked.

## 1. Ordering

**The production screen** has two buttons, `convoy` and `escort` (`country_production.gui`). Each opens the
**confirm-build dialog** (`confirm_build`, `buildinglines.md` section 2) for transports or for escorts; neither orders
anything itself.

- **`convoy` may always be pressed.** Nothing enables or disables it.
- **`escort` is disabled until a technology has given `allow_escorts`** - in Their Finest Hour only `sea_lane_defence`,
  "Sea Lane Defence".

The dialog, for transports and for escorts:

| element | transports | escorts |
| --- | --- | --- |
| `photo` | `picture_convoy` | `picture_escort` |
| `label` | `CONVOYS` "Convoys" | `ESCORTS` "Escorts" |

- `serial_value` and `paralell_value`, their steppers and their tooltip are the building's: 1 to 99, 10 with Ctrl;
- `ic_cost_value` **parallel x the cost a day** (section 2), to two places;
- `build_time_value`: **serial x the time, in thousandths of a day, over 1000, truncated** - "N Days" to 365, "N/30
  Months" above (`DAYS`, `MONTHS`); with the defines as they ship that is serial x 100 days for transports and serial x
  240 for escorts;
- the green, orange or red mark as for a building: the production slider's IC less every queued line's cost a day is
  the spare, green where it covers parallel x the cost, orange where it is above nought, red otherwise;
- **`on_complete_icon` and `on_complete_impact` are left as the file has them** - the dialog sets them only for a
  building (section *Not established*);
- **`accept`**, never disabled, orders **`parallel` lines of `serial` items each**; `cancel` orders nothing.

**The order** - the original's `CConstructConvoyCommand`: the country, transports or escorts, and how many items:

- **always valid**: nothing refuses it - not escorts without `allow_escorts`, not an amount of nought, not a country
  with no ports;
- a **new line at the end of the queue**, of the size asked, with no cap, priced (section 2); a convoy line is never
  joined to another;
- nothing is paid and nobody is told.

**The AI's production minister** posts the same order from the install's Lua, `CConstructConvoyCommand(tag, escort,
amount)` - not built here (*Not built*).

## 2. The price

    cost = COST_DEFINE x (1000 + the technology's cost field) / 1000         thousandths, truncated, at least 0.010
    time = TIME_DEFINE x (1000 + the technology's time field) / 1000         thousandths, truncated, at least 0.010
    duration = time cut to whole days

| | define (`economy`) | Their Finest Hour | technology effect |
| --- | --- | --- | --- |
| transports' cost a day | `CONVOY_BUILD_COST` | 2 | `convoy_build_cost` |
| transports' days | `CONVOY_BUILD_TIME` | 100 | `convoy_build_time` |
| escorts' cost a day | `ESCORT_BUILD_COST` | 4 | `escort_build_cost` |
| escorts' days | `ESCORT_BUILD_TIME` | 240 | `escort_build_time` |

The four technology effects are what the country's technologies give it, summed; **no technology in either install
gives any of them**, so in Their Finest Hour an item of transports costs 2.000 a day for 100 days and one of escorts
4.000 for 240. **A line is priced once, when it is ordered, and never again**: the day a line of the queue is finished,
a change of law and the start of a game price every other line again and pass a convoy line over (`buildqueue.md`,
section 2). The line's size plays no part: a line of five costs one item's day, each item taking the whole time.

## 3. The day and what a finished item gives

**As any line** (`buildqueue.md`, section 3): its need is its cost, paid in the queue's order from the share; a full
day is a day of progress. It gathers no experience and teaches no practical.

**An item is finished** where progress reaches the duration:

    the country's free escorts (escorts) or transports (otherwise)  += CONVOY_CONSTRUCTION_SIZE / 1000, truncated  -- 10
    progress -= min(duration, progress)          -- what was paid past the duration goes to the next item
    size -= 1
    the line is done where size is 0 or less

- **One item is `CONVOY_CONSTRUCTION_SIZE` ships**, 10 as the define ships; a line of five transports gives ten every
  100 days for 500 days.
- **Nothing else**: no practical, no message, no "does the country still own land" test - a unit's and a building's
  line both have that test and a convoy line does not - and no price again for the next item.
- A line finished (not an item of a series) sets off the day's pricing of every other line, as any line's does.

The new ships lie in the free pool (`CCountry.Transports`, `Escorts`), which is what the day's convoys are manned from
(`convoys.md`).

## 4. The production screen

**The counts.** `transports_value` and `escorts_value` are the free transports and escorts, each followed by
`" (+N)"` where N, the ships still to come, is not nought:

    N = for each convoy line of the kind: size x (CONVOY_CONSTRUCTION_SIZE / 1000, truncated)

**The line in the queue** (`queue_entry`):

- `counter_type`: the sprite `icon_build_convoy`, or `icon_build_escort` for escorts;
- `unit_name`: `CC_CONVOY` "Convoy" or `CC_ESCORT` "Escort", **with no count**;
- `unit_target` empty, `original_country` hidden;
- the background, speed, cost, date, progress and `series_info` ("+4" on a line of five) as any line's;
- **the category picture is not chosen** for a convoy or a building line; the four stand as the file has them, the
  buildings' last and on top (*inferred*).

**Tooltips:**

| element | tooltip |
| --- | --- |
| `convoy` | `CONVOYS` "Convoys" |
| `escort` | `LEDGER_ESCORTS` "Escorts"; where disabled, a line `NEED` "Need" + " §Y" + the first technology that gives `allow_escorts` |
| a convoy line's `counter_type`, `series_info`, `unit_target`, the division, air and naval pictures | an empty tooltip |
| a convoy line's other elements but its buttons | the line's speed sentence, as any line's |

**While the confirm-build dialog is open, nothing else on the production screen has a tooltip**: the screen hands every
tooltip to the dialog, which answers only for its four steppers. This holds for a building's dialog as much as a
convoy's.

## 5. History and the save

A history's `convoy_construction` block makes a line: `size`, `cost`, `duration`, `progress`, and `status = yes` for
escorts; its `country` is overwritten by the country the file is read for. A new game keeps the file's cost and duration
and sets its progress to nought. Their Finest Hour's history has none; BlackICE's has - *Not built*.

## 6. Against the saves

`OpenHOI3.SaveCheck research <install> <earlier save> <later save>` prices every convoy line of the earlier save as an
order would and sets it beside the save; then it works each line paid in full in both saves for the days between,
paid in full every day, and sets what is left of it - or that nothing is - beside the later save. A line keeps its
number only within one session of the game, so two saves with a load between them are priced and not worked forward.
*Measured* on 2026-10-15, a reference game:

| saves | priced | worked forward |
| --- | --- | --- |
| 4 January 12:00 - 8 January 07:00 | 76 of 76 | 39 of 42 |
| 8 January - 1 February, 1 February - 1 March, 1 March - 1 April | 77, 75 and 75 of each | a load between: not worked |
| 1 April - 20 April 03:00 | 107 of 107 | 69 of 70 |

Every line the AI had ordered is at 2.000 and 100 days or 4.000 and 240. The four worked forward that differ are each
a part of a day short - Britain's two by 0.292 and a whole day, Nicaragua's 0.129, Panama's 0.056 - which is a day in
between that the pool did not pay in full, and that the saves do not show. Among those that agree, Japan's five lines
of two at 90.000 on 1 April are lines of one at 9.000 on 20 April: an item finished on the tenth day, and the nine days
after it went to the next.

## What looks wrong and is built so

1. The `convoy` button can always be pressed; nothing but the `escort` button stops a player ordering escorts without
   `allow_escorts`, and the order itself never refuses.
2. A convoy line is priced once: a technology, a law or a minister never reaches a line already queued.
3. A finished item gives no practical and raises no message.
4. A finished item does not ask whether the country still owns land.
5. A line's name has no count; the count is in `series_info`.
6. While the confirm-build dialog is open, nothing behind it says anything.

## Ours

- **A line's number is taken from the count units and brigades take**, one a line, where the original reserves two
  more for each item that nothing uses.
- **Ordering runs nothing else.** The original's order also rebuilds the country's neighbour lists and runs what follows
  a diplomatic change - timing, not the line.

## Not built

- **The AI**, whose production minister orders transports and escorts in the install's Lua, `ConstructConvoys`.
- **BlackICE's history lines of convoys** (`history/units`, `convoy_construction`), which the order of battle's reader
  reports and passes over, as it does BlackICE's building lines.

## Not established

- **Whether `on_complete_icon`, `on_complete_frame` and an empty `on_complete_impact` are drawn** in the transports and
  escorts dialogs: nothing sets them. A screenshot of the dialog after pressing `convoy` would settle it.
- **Which category picture a convoy line shows**: the buildings', last in the file, is inferred.
- **That a transport line loaded from a save shows a share of nought until its next day** - the original reads the
  second `status = no` as a number over the share. No save is loaded here, so it does not arise.
