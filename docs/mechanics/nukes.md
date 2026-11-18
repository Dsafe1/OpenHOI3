# The nuclear bomb

Read from `reversing/findings/FINDINGS-nukes.md`. [exe] is hard-coded in the original; [file] is a mod's to change, in the file named; [defines] a key of `common/defines.lua`.

## 1. Where bombs come from

A country's stock is in thousandths: a thousand is one bomb. Each month, for every country, it gains what its technologies make, `nuclear_production` summed over them [exe]: no cap and no other condition. Their Finest Hour has one technology with the key, `da_bomb`, at 0.1 a level [file], so a level makes a bomb in ten months. **A nuclear reactor makes nothing**: it is only what `da_bomb`'s `allow` asks for [file]. The console's `nukes` adds to the stock and a history file's `nukes` sets it [exe]; no file of the install has one. The stock is saved as the country's `nukes`.

The production screen's `nukes_value` is the stock, its tooltip `NUKE_PRODUCTION` with the month's figure, or `NO_NUKE_PRODUCTION` where that is nought.

## 2. Who carries one

Any air unit with a wing whose strategic attack is above nought [exe], which the unit files decide [file]. No technology is asked beyond having a bomb.

## 3. The order

**The row** `NUKE_MISSION`, on the air orders window, after the rebase to a carrier and before the air supply [exe]. It exists only over land and only where the stock holds a bomb for every unit selected [exe]; it is greyed where the province is out of a unit's range or is not an enemy's. **It is ticked as soon as it is made, even greyed**, and the window's other choices of a row do not run, unless the rebase ticks over it first [exe]. Its tooltip is `NUKE_MISSION_DESC`; in the unit panel the order is `NUKE_MISSION_SHORT_DESC`, its `$TYPE$` as the other air orders', and `unitstatus_nuke` shows while it carries a bomb.

**Given**, it takes one bomb from the stock if there is one and sets off [exe]. With none it carries nothing and ends at its next hour, with nothing lost.

**Each hour** [exe], the order is over with no base, no bomb, or a target whose controller is no longer an enemy; it does nothing further in a battle, a bombing or under way; over the target it drops; elsewhere it flies on. **It asks no end date and no range after it is given**, so the window's two dates do nothing. Flying: home with no fuel; otherwise the way to the target, by day or night as the order says, as a strike flies.

**A bomb not dropped goes back to the stock** whatever ends the order: another order, the target changing hands, or the bomber being lost [exe].

## 4. The drop

In this order [exe]:

1. **The controller's national unity**, through the usual change, so scaled by its `national_unity_effect` and held to 1 to 100. The amount is `NUKE_UNITY_IMPACT` [defines] times a factor in thousandths: 1000; where the province is one of the controller's cores 1000 and the province's IC modifier; twice that at its acting capital.
2. **Every building** of every kind goes to nothing working, what is built left, so the day's repair brings it back. The province's modifier is not made again here.
3. **The province's goods and what it produces** are emptied. In a capital that is the country's stockpile.
4. **Every unit in the province**, of every country the bomber's own included, but the bomber and an air unit that is not at its own base. A land unit takes `(670 + roll / 3) / 1000` of its full strength, shared over its brigades, and ten million organisation shared evenly; each ship or wing takes that share of its own full strength, its own roll, and ten million organisation. The roll is 0 to 999 from the game's generator. It is settled as a battle's is, so **a brigade or a wing takes a hundredth of it, 0.67% to 1% of its strength, where a ship takes the whole**, and a brigade loses a share of 100 organisation by the number in its unit.
5. **Messages** to the players: `WE_NUKE` for the bomber's country, `NUKE_ON_US` for the controller, `NUKE_ON_OTHER` for anyone else. The first flag is the province's owner, the second the bomber for `NUKE_ON_US` and the controller otherwise.
6. **`on_nuke_dropped`**: its first event whose trigger holds, in a scope of the bomber's country, the province and FROM the controller, seed nought (`events.md`, section 8).

Nothing else: no manpower, leadership, revolt risk, dissent, relations or threat.

A bomb has been seen landing in the original, and on 2026-11-09 the scale of item 4 was confirmed from it: a brigade loses about a hundredth of its strength and most of its organisation, and ships are usually sunk.

## 5. A province event

`province_event` is the effect `country_event` is, fired in a scope with the province and no country, so the province's owner answers. FROM and the draws are the same. Which window it takes comes from the scope having a province, not from the event's kind. **A province event cannot fire by mean time** (*inferred* in the fact base with a positive control); only an effect or an on_action fires one.

## Ours

- The AI gives this order only where it knows strategic targets, the enemy's war industry handed to its agents (`moving.md`), which is not built, so **the AI makes bombs and does not drop them**.
- A fleet's transports taking damage lay none on what they carry, there being no side for it.

## What looks wrong and is kept

`enhanced.md` lists them.

1. Land units and air units at base lose under a hundredth of their strength; ships are sunk.
2. The bomb hits the bomber's own side and its friends in the province.
3. A greyed row can be the one ticked.
4. The window offers dates that do nothing.
5. The province's modifier stays as it was until something else makes it again, so its industry goes on counting.
6. The event's option gives the bomber's country national unity for the first and later bombs: `nuke_events.txt` [file].
