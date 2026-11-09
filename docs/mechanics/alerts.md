# The alerts

Read from `reversing/findings/FINDINGS-alerts.md`. Figures marked [exe] are hard-coded in the original; [defines] are keys of `common/defines.lua`; [gui] are read from `interface/alerts.gui`.

## 1. The pass

Every **three seconds of real time** [exe], paused or not and whatever screen is up, the alerts are tested for the played country, first on the first frame. An alert newly true gets an icon at the row's end; one no longer true loses its icon and the row closes up, the others keeping their order. An alert that comes back goes on the end. There are no alerts in a custom game, and a scenario holds off all but 11, 14 and 15 [exe]: there are neither here.

The first icon stands at `alerticon_startposition` and each after it `alerticon_offset` on [gui]. An `alerticon_window`'s `alerticon` shows the frame of the alert's number and one [exe]. `alerticon_banner` is off the screen in the file and nothing touches it.

The tests run in this order [exe], which is the order alerts that come up in one pass join the row: 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 14, 12.

## 2. The sixteen

| | key | up when | the list in its delayed half | a click |
| --- | --- | --- | --- | --- |
| 0 | `alert_nap_expiry` | a pact has passed `NAP_UNBREAKABLE_MONTHS` less `NAP_EXPIRY_ALERT_MONTHS_AHEAD` [defines] since it began, and the second is above nought | section 3 | nothing |
| 1 | `alert_deployable_units` | something waits to be placed | the first 21 [exe]: `£`, the first brigade's type and the name; a building's name; `...` for more | the deploy menu, closing a country screen; the first selected unit stays selected |
| 2 | `alert_troops_in_foreign_territory` | a land or air unit stands where the controller is not the player, nor an enemy, nor allied, nor giving access, nor in its real faction, nor its puppet or master | the provinces, as found | nothing |
| 3 | `alert_revoltrisk_in_provinces` | never: nothing shows it [exe] | | |
| 4 | `alert_can_invite_to_faction` | the player leads a real faction, and another country that owns land, is in no real faction, is within `FACTION_JOIN_DIST`, is not at war with the faction and has effective neutrality at or below `INVITE_FACTION_NEUTRALITY` [defines] | `Name (@TAG)`, `, ` and ` AND ` | diplomacy |
| 5 | `alert_resource_panic` | a good is falling with fewer than 30.001 days of it and more than -1.001 [exe]: stockpile over the day's fall, in thousandths and cut | `RES_OUT_IN_X_DAYS`, its days that figure's whole part and one | nothing |
| 6 | `alert_possible_law` | a law group holds, earlier in the file than the current law, one whose `allow` holds: the nearest such, a line a group | `POSSIBLE_LAW` with the name, the change's cost to one place, and the group | politics |
| 7 | `alert_free_tech_slots` | research's share of leadership, in thousandths, exceeds a point for each line of the queue | none | technology |
| 8 | `alert_wasted_ic` | a whole IC is spare in all (section 5) | `POSSIBLE_WASTED` for each share with any | production |
| 9 | `alert_tech_far_ahead` | something in the queue is due over a year ahead | `TECH_AHEAD`, the name and its folder in brackets, by the years ahead | technology |
| 10 | `alert_lack_of_troops` | the top of a command the AI has, with objectives and brigades, has less power than the enemy's times 2 attacking, 3 blitzing or 1 [exe] | the first 20 [exe], `£`, type, name, `=> ` and the objectives; `& ...` | the next such unit, selected and centred; the selection is kept |
| 11 | `alert_bad_supply` | a unit with brigades got under all of the supplies it asked for | the first 20: `£`, type, name and province; `& ...` | the next such unit |
| 12 | `alert_national_decisions` | a diplomatic decision may be taken | the titles, in the file's order | diplomacy |
| 13 | `alert_mobilized_in_peace` | mobilised at peace | none | nothing |
| 14 | `alert_victory_conditions` | `endgame.md`, section 6 | | the goals window, opened and never closed |
| 15 | `alert_allied_objectives` | a friend has asked something of the player | section 4 | nothing |

Automation holds some off [exe]: supplies and money in alert 5, and alert 8, while production is handed over or the sliders are automated; alert 6 while politics is; 7 and 9 while the technology flag is.

## 3. The pact

For each pact listed, under `alert_nap_expiry_delayed`, ` - ` and: before it may be broken at all, `_list_A` with that date; in one of its three windows of `NAP_FORCE_BALANCE_RULE_MONTHS` that is still to end and whose limit the balance (`agreements.md`, section 6) is below, `_list_B` with the window's `NAP_BREAK_FORCE_BALANCE_` as `LIMIT_PERC` and the balance as `CURR_PERC`, and, but in the last, `_list_E` with the window's end and the next one's limit; past them, `_list_C`. Then always `_list_D`, with the date it expires. A date is the year, the month's name and the day. The other party is its flag and name.

## 4. Allied objectives

`AO_ALERT`, and a line for each province asked about: each country asking in yellow, `, ` and ` AND `, then `OL_AO_THEY_REQUEST`, `OL_AO_DEFEND` where the province's controller is in the player's faction and otherwise `OL_AO_ATTACK`, and the province in yellow.

## 5. Spare IC

What a share was given, in thousandths, less what it needs, never below nought [exe]:

- consumer goods: only with no dissent, a twentieth over their need, the need being rounded to a thousandth;
- production: what its queue used;
- reinforcement and upgrades: a day's cost of their work;
- supplies and lend-lease: never.

Alert 8 wants the five added and cut to whole IC to be above nought, so a country with a little spare in several shares is told of them all.

## Clicking

Every click closes the orders window and any other side menu, and lets go of the selection but for 10 and 11. A country screen answering to an alert opens as its button does, so a second click closes it. The six that have nothing to do (0, 2, 3, 5, 13, 15) still close and let go [exe].

## Ours

- **No sound**: there is no audio in this engine yet, so an icon coming up plays no `message`.
- A building waiting is listed after the units; the original's one list has them as they came.

## What looks wrong and is kept

`enhanced.md` lists these.

1. Alert 3 is complete in the original, list and all, and nothing shows it. It is not built.
2. A law "more efficient" is only one earlier in the file whose `allow` holds; nothing asks whether it is better.
3. A pact is listed three months before it may be broken at all, as "may be broken on".
4. The wasted IC figure on the top bar is another sum, of what shares were given over what they spent.
