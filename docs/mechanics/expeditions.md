# Expeditionary forces

A unit lent to an ally, and taken back. No addresses. Source: `reversing/findings/FINDINGS-expedition.md`, static, and `reversing/findings/FINDINGS-surrender.md`, sections A and C.2, for an annexation and an exile; the window and a declined offer were tried with `--click`, the hand-over and the return by tests, and an exile's sending home with `OpenHOI3.SaveCheck declare ... occupy lend=TAG` (section 5). The maintainer has not looked at it. Hardcoded but the windows and words, and `EXPEDITION_INFLUENCE_COST`, `EXPEDITION_RETURN_TIME` and `EXPEDITION_RECLAIM_TIME` in `defines.lua` [mod].

## 1. The window

`ExpeditionDialog`, opened by any of the three rows. One kind of unit at a time - Army, the default, Air and Navy - and one unit ticked across all three. Sending lists the player's units that are nobody's loan; a Retrieve lists the recipient's units the player lent, a Return the player's units the recipient lent, none with a recall under way. Land is a tree, a unit with a commander appearing only under it, children shown where it is opened, a row indented 10 a level; air and fleets are flat. A theatre is listed and its box greyed. Ticking takes every other tick off and ticks what shows under it; nothing else is tested, not a battle or the sea. Accept is greyed until one is ticked. No element has a tooltip.

The description says who it goes to or when it comes back, and names the unit, how many units its tree holds - itself among them - and where it stands. **Kept as read**: a Retrieve's date reads the reclaim define and the delay is the return define's; clearing a fleet's box leaves Accept up, and what it posts names no unit and does nothing.

**Ours**: which rows are open is the window's, where the original shares it with the outliner.

## 2. The rows

Where allied or in one real faction, above Share Technology: **Expeditionary Force**, and **Return** where the player holds a unit of theirs and **Retrieve** where they hold one of the player's, neither with a recall under way. Only sending sends a diplomat and is greyed by one; it is greyed too for want of the influence.

## 3. The flow

The influence is paid when offered and never refunded. The AI answers by `DiploScore_SendExpeditionaryForce` in the install's Lua. Accepted, the unit leaves its commander and it and all under it go to the host, each remembering who lent it first; the lender is told `OUREXPACC` and everyone else `EXPOTHER`. Declined, the lender alone is told `OUREXPDEC`.

A withdrawal sets only the date, the return define's days from now; nobody is told. Each day a host sends home **the first** unit past its date in its list of units, so several come one a day. It goes alone: **what is under it is detached and stays with the host, still on loan** - each now a unit of its own at the top, with the lender's flag, its date the game's end, to be given back or recalled by a row of its own.

**Going home**, in order (`CUnit::ReturnFromExpedition`, read whole): the unit leaves its commander; it leaves its lender's list and has no lender; the host's AI lets it go; it is the lender's; what was under it is detached where it stands; and both sides are told. `EXPRETURNEDWE` to the host and `EXPRETURNED` to the lender, each with the player's standard values, ACTOR the host, RECIPIENT the lender, MESSENGER the foreign office and NAME the unit's, about the province the unit stands in, the host first and the lender second, and **carrying the unit**: its Goto selects it - the lender's by then - and centres on it, and the popup closes (`messages.md`, section 4.1; `reversing/findings/FINDINGS-expedition.md`, the addition of 2026-11-14). Checked on screen: a save's Phillippine Force sent home to the United States is selected at Manila by the popup's Go to. No peace, change of alliance or end of a war sends one home.

**The AI's own route** (`CSendExpeditionCommand`, `agents.md`, section 10.5) asks nobody and waits for nothing: the unit and **everything under it go to the other country together, the tree kept**. Each that came from that country first is its own again, nobody's loan; any other is lent, remembering who lent it first - its owner where it came from nobody - with no recall standing. Nobody is told. The AI's agents detach what is under a unit before they give one back and lend only a unit with nothing under it, so here, where a landless country's sending abroad is not built (section 6), a tree is not carried yet.

**Ours**: a unit home, by either route, has its date cleared, where the original leaves its old one, which nothing reads without a lender.

## 4. What a lent unit is

The host's to order, but its leader is greyed: `compatibility.md`, entry 23. It shows the lender's flag on its counter and in the panel's `exp_force`, whose tooltip says who lent it. It may not be merged, reorganised, split, disbanded or attached to, or have a brigade's type changed, and `split_troops` and removing all leaders pass it over. Saved as `expeditionary_owner` and `expeditionary_date`, the end date while no recall stands. **An order of battle's `expeditionary_owner` line makes its unit lent too** - the original's unit loader takes the line from a history file as from a save - so Their Finest Hour's 1941 bookmarks start with the Romanian, Hungarian, Slovak, Finnish, Canadian, South African and other divisions their files lend Germany, Britain and Finland, on their lenders' lists, as the start saves have them; read since 2026-11-15 (`units.md`, section 3), with no date, so the end date's.

**Its figures are its lender's**: a brigade's organisation and the regain of it, what the unit uses and who pays for it, its combat width, the combat modifiers and what it fires with, its attack delay, the point it shatters and the officers it loses and gets back. The lender goes on officering and reinforcing it - its brigades take the lender's officer ratio - and the host does neither; and **the host never counts it among what its officers must cover; the lender does where the two share a faction**, and nobody where they do not (`organisation.md`, section 1; `reversing/findings/FINDINGS-liveread.md`, sections 79 and 107). Its brigades' stored figures are left as they were when lent.

**A unit an order of battle lends is made with its lender's brigades**: as a new game takes each brigade's levels from its historical model and fills a doctrine the model leaves at nothing from its country (`units.md`), a lent unit's brigades take the lender's - the save's Corp 5 Armata, lent Germany by Romania at 1941.12.11, has Romania's `mobile_warfare` of nought where Germany's is five, and with it a headquarters too slow for the offence a leader's doctrine trait is worth, so Germany's choosing gives it Straube and not Kleffel (`leaders.md`, section 5). *Measured*: every lent brigade's levels the save's, 52 of 52 at 1941.6.22 and 173 of 173 at 1941.12.11, where 44 and 135 were (2026-11-15; `CUnits.SetAtStart`).

A lent unit taken out of the game leaves its lender's list of units lent out, as the original's frees it (`inferred`: the original's removal unlinks a unit from everything holding it, and a freed unit left on the list would be reinforced).

The passive upgrading by the sliders goes on as the original's, from the lender's. The upgrade command on the unit panel, a brigade changed to another type, is refused for a lent unit.

## 5. An annexation, and a government in exile

**An annexation** (`war.md`, section 13): every unit lent to the country, of every level in the order of its list, goes home as in section 3, both sides told, and is shattered at its lender's; then every unit it lent, in the order of its list of them, comes back to it, to be destroyed with the rest.

**A government going into exile** (`war.md`, section 14): of its units on ground a country not its friend holds, its own are taken out of the game and **those lent to it are set aside**; when the walk is over each goes home as in section 3, its two sides told about the province it stood in, and is **shattered** at its lender's. A lent unit at sea, or on a friend's ground, stays lent to the exile. A lent corps goes home without what is under it, and each of those, set aside on its own account, follows it one by one.

So every unit returned this way is told about - `EXPRETURNEDWE` to the host's player, `EXPRETURNED` to the lender's - and stands at its lender's capital, as a shattered unit does (`landcombat.md`, section 6), with the gap that section names: the original picks the capital or a depot where the unit stands.

Tried with `OpenHOI3.SaveCheck declare <install> GER FRA occupy lend=ENG` on 2026-11-14: Britain lends France two divisions, one in Porsangerhalvoya, Norway's, and one in London. With France in exile in London, the first is Britain's at London, off Britain's list of units lent out, and both sides were told; the second stays France's on loan. Before this, the first was taken out of the game and left on Britain's list. `GER AUS occupy lend=HUN`, an annexation, sends both of Hungary's home, as it did.

**Kept as read**: a unit at sea is never asked about; a corps' divisions come back each as a unit of its own, not under it.

## 6. Not built

The AI lending or giving back by an offer, and a landless country's units sent abroad by the AI's own route (`agents.md`, section 13). A player asked to accept one, which only an AI's offer could raise.

**Ours**: the original's AI is told of a unit going home, every agent that holds it letting it go; the agents here keep their lists until they next sort their subordinates (`agents.md`, section 5.1), and only the unit's own agent is told.
