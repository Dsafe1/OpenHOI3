# The land orders window, and the orders it gives

The window a land unit's right click brings up with **Ctrl** held: the original's `land_unit_order` in
`interface/orders.gui`. It is the only way a player gives a land unit a **strategic redeployment** or a **support
attack**, and it gives a plain move with a date to start at or to arrive by. Read out of the original whole in one wave
(`reversing/findings/FINDINGS-landorders.md`, section A the window and B the orders) and set against two
screenshots of it, one on a province of the player's own and one on an enemy's, every figure on which the reading accounts for.

What is hardcoded and what a mod may change: the window's layout is `orders.gui`'s and the date widget's
`datewidget.gui`, its words are localisation (`MOVE_ORDER`, `SUPPORT_ATTACK`, `ORDER_STRATEGIC_REDEPLOYMENT`,
`ORDER_SYNC_ARRIVAL`, `ORDER_DAY_TIME`, `ORDER_DAY_TIME_DESC`, `ORDER_DAY`, `ORDER_NIGHT`), and the attack delay and a
redeployment's speed are defines (`UNIT_ATTACK_DELAY`, `UNIT_ATTACK_DELAY_MODIFY`, `STRAT_REDEP_*`); everything else -
which rows, their tests, the tick, what the orders do each hour - is the executable's.

## 1. Opening it

- **A right click on a province with Ctrl held, with a land unit selected**, brings it up for the province - for the
  first selected unit, as the naval and air windows do (`selection.md`, section 6). Without Ctrl the click is the plain
  move of `movement.md`.
- **Nothing is kept from one window to the next**, unlike the air window's settings.
- While it is up a right click on the map does nothing, as with the naval window.

## 2. What it shows

- **`title`**: the province's name.
- **`orders_list`**: always three rows of `order_entry`, in this order: **Move Order, Support Attack, Strategic
  Redeployment** - each `name` the order's key. **None is ever left out.**
- **A row is greyed where the order may not be given (section 6) to any one of the selected land units** - one refusal
  greys it, and its box shows the sheet's fourth frame, a grey filled box, ticked or not (`interface.md`, section 7). The rows are made as the window opens and **greyed once, then**: nothing makes them again while it is up.
- **The row ticked first** is the first that may be given of **support attack, then move, then strategic
  redeployment**; with none, nothing is ticked and `accept` cannot be pressed. Ticking a row does nothing else.
- **A date widget** (`datewidget.gui`'s `datewidget`) at the position `land_mission_timewidget_start` of `orders.gui`,
  starting at **the hour now**, with its eight arrows: a year of 8,760 hours, a month, a day of 24 hours, an hour, each
  held between the defines' start and end dates - the air window's (`fleetsandair.md`, section 4). **It may be stepped
  into the past.**
- **`sync_checkbox` and `sync_title`** ("Synchronise Arrival") are **shown only while Move Order is ticked**, which is
  why neither screenshot has them. The box starts clear. While it is ticked, each frame **raises** the date widget to
  the estimated arrival of section 4 where that is later, and never lowers it.
- **The daytime row**, each frame at **the date widget's hour**: `daytime_title` "Locally in $PROV$:", `daytime_icon`
  (`GFX_prov_daynight`) its second frame, the moon, at night and its first by day, and `daytime_icondesc` "Night" or
  "Day" - night as the air window works it out for a province and an hour (`fleetsandair.md`, section 4).
- **`accept`** can be pressed while a row is ticked; **`cancel`** shuts the window.

There is **no stance and no priority** in it.

## 3. Accept

For **every selected unit that is a land unit and is not falling back** - the window's unit first - the ticked order is
given with **the defensive stance**, and two dates:

- without the synchronised arrival: **not to start before the date widget's hour**;
- with Move Order ticked and the box ticked: **not to arrive before the estimated arrival** (section 4), worked out
  afresh, and nothing on the start.

Then the window shuts.

## 4. The estimated arrival

For each selected unit, the latest of them:

    speed   = the unit's top speed (its slowest brigade's maximum_speed)
              x (1000 + its adjuster for the province's terrain) / 1000
              x (1000 + what the province's weather does to movement) / 1000
              x LAND_SPEED_MODIFIER / 1000
    hours   = ceil( the length of its way there / speed )
    arrival = now + hours + its attack delay

**The province's own terrain and weather**, where the march uses the ground of each step and the weather where the unit
stands, and no `local_unit_speed`.

## 5. The two dates

A land unit holds the two hours an order was given with (`movement.md`):

- **not before**: before that hour the unit does not move at all - it takes no step and adds nothing to its progress;
- **not arriving before**: a unit that has covered the distance to the next province waits there with full progress
  until that hour.

**A plain move, its halt, and a unit's halt for any reason empty both**; the window's orders are the only thing that
sets them.

## 6. Which order may be given where

Each order's own test, for one unit and the province:

- **Move Order**: the unit is in no battle, **the province is next to the one it stands in**, and it may go there
  (`movement.md`). So the window's Move Order goes only to a neighbouring province, where a plain right click goes any
  distance.
- **Support Attack**: the province is held by a country **at war with the unit's** (the rebels counting so on either
  side), the unit is in no battle, and **the province is next to the one it stands in**. Nothing else - no battle has to
  be going on there, nobody has to stand there.
- **Strategic Redeployment**: the province is **not held by an enemy**, the way the unit already has does not cross a
  province an enemy holds, the unit is in no battle, and both its own province and the target are land a country owns.
  **No distance, no way and no kind of unit is asked** - a neighbour will do, and so will a province with no way to it,
  which the order then drops at once (section 8).

## 7. What a support attack does

As it is given, a unit on its way is **stopped where it stands**; nothing is routed. Then each hour, in this order:

1. before its not-before hour: it waits;
2. **in a battle**: the first such hour starts its attack delay, and it is marked as having joined; every such hour
   counts one hour of attacking. It goes on;
3. **out of a battle, having joined one**: the battle is over - the unit is halted and the order ends;
4. its attack delay running: it waits;
5. **a land battle in the province**: a unit with no way is given the plain way **one step in**, and the march's look
   ahead then has it **join** that battle - it never enters, its progress never growing under this order;
6. otherwise: a unit with a way has it taken away with the order; one without waits.

So **a support attack waits beside the province, as long as need be, until somebody else's land battle begins there**,
joins it without moving in, and ends the hour that battle does. Its way gone before it joins, the order is dropped.

## 8. What a strategic redeployment does

It is given the **very safe** way there at once (`movement.md`, section 3). Then each hour:

- in the province: done;
- otherwise **its attack delay is started again, every hour of the journey** - a draw of the game's generator each hour
  for each unit redeploying - so it always arrives with a fresh delay. **An enhanced game's fix
  `redeploy_delay_once`** (2026-11-27): nothing is started on the way and the delay is started once, in the hour
  the unit is found in the province - one draw, and the same whole delay on arrival. An order that ends short of
  its province, for an enemy or a battle ahead, then leaves no delay behind, where the original's leaves the last
  hour's;
- where the next province of its way is held by an enemy **or has a battle in it**: if that is the target, the order
  ends with the way taken away; otherwise the way is taken away and the very safe way found again, the order ending if
  there is none - **given as any way is, so a unit in a battle leaves it**, falling back where it was holding ground
  (`reversing/findings/FINDINGS-liveread.md`, section 87: Poland's 55th reserve division, attacked at 2447 on 6 September
  1939, goes round by 2508 and the battle is left with no defender);
- in a battle (still, after that): the order ends;
- with no way: the very safe way is found again, the order ending if there is none.

Its speed, its supplies and its organisation on the way are `movement.md`'s.

## 9. Tooltips

**Only the daytime row speaks**: `daytime_title`, `daytime_icon` and `daytime_icondesc` say `ORDER_DAY_TIME_DESC` -
"Estimated local day time in $PROV$ when the mission begins is: $TIME$" - with TIME Night or Day at the date widget's
hour. **The rows, the buttons, the box and the date widget say nothing**, unlike the naval and air windows' rows.

## 10. Kept as the original does them

Each looks wrong and is built so - do not tidy them:

- **The window's Move Order and Support Attack go only to a neighbouring province**, a plain move any distance.
- **A row's greying is fixed as the window opens.**
- **The date widget may be set in the past**, which holds nothing back.
- **A strategic redeployment starts its attack delay again every hour** on the way, drawing on the generator each time.
- **A strategic redeployment's test asks nothing about distance or a way**; one with no way there is dropped the hour
  it is given. Confirmed on 2026-11-04, on its row being open for a province it cannot reach: "The
  availability of the strategic redeploy to unreachable provinces is the same in the original."
- **A support attack counts its hours of attacking in its own hour as well as the battle's.**
- **A support attack's way into a province with no battle is taken away with the order**, so the AI's support attack on
  a province where nobody fights yet is dropped.
- **No row has a tooltip.**

## 11. Ours

- **Land a country owns** stands for the original's owner area of a province with provinces in it: the record has the
  area as the province's, and every land province with an owner has one.
- **The length of the way** in the estimate is the plain way's, summed edge by edge; the original's call to a path's
  length was read, not which path it is handed.
- **The top speed** in the estimate is the slowest brigade's, as the march's (`movement.md`, section 5.1); the original
  reads one figure off the unit's definition, not read further.
- **The march's look at the province ahead**, which starts or joins a battle, is held back before the start hour as
  well as the step: the original's not-before test stops the march, and where its battles start from was read apart.
- **An expeditionary unit's** redeployment would judge the next province against the country that lent it; nothing here
  is lent, so it is judged against the unit's own.
- **Which units the window asks** for its greying and gives the order to are the selected land units, the window's unit
  first, as the naval and air windows do (`selection.md`, section 6); the original asks every selected unit and gives the
  order to its land units.

## 12. Not established

- The date widget's text, which is *measured* and not read: the screenshots give "1936 Jan 1 0:00" and "1936 Jan 4
  11:00" - the year, the month cut to three letters, the day and the hour - and each character standing in a cell of
  10, `timefont`'s one advance (`interface.md`, section 6). The air window's widget is the same.
- What the original's dead test in the redeployment's rule (a region group of the unit's area) was meant to ask.
