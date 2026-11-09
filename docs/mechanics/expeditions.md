# Expeditionary forces

A unit lent to an ally, and taken back. No addresses. Source: `reversing/findings/FINDINGS-expedition.md`, static; the window and a declined offer were tried with `--click`, the hand-over and the return by tests. The maintainer has not looked at it. Hardcoded but the windows and words, and `EXPEDITION_INFLUENCE_COST`, `EXPEDITION_RETURN_TIME` and `EXPEDITION_RECLAIM_TIME` in `defines.lua` [mod].

## 1. The window

`ExpeditionDialog`, opened by any of the three rows. One kind of unit at a time - Army, the default, Air and Navy - and one unit ticked across all three. Sending lists the player's units that are nobody's loan; a Retrieve lists the recipient's units the player lent, a Return the player's units the recipient lent, none with a recall under way. Land is a tree, a unit with a commander appearing only under it, children shown where it is opened, a row indented 10 a level; air and fleets are flat. A theatre is listed and its box greyed. Ticking takes every other tick off and ticks what shows under it; nothing else is tested, not a battle or the sea. Accept is greyed until one is ticked. No element has a tooltip.

The description says who it goes to or when it comes back, and names the unit, how many units its tree holds - itself among them - and where it stands. **Kept as read**: a Retrieve's date reads the reclaim define and the delay is the return define's; clearing a fleet's box leaves Accept up, and what it posts names no unit and does nothing.

**Ours**: which rows are open is the window's, where the original shares it with the outliner.

## 2. The rows

Where allied or in one real faction, above Share Technology: **Expeditionary Force**, and **Return** where the player holds a unit of theirs and **Retrieve** where they hold one of the player's, neither with a recall under way. Only sending sends a diplomat and is greyed by one; it is greyed too for want of the influence.

## 3. The flow

The influence is paid when offered and never refunded. The AI answers by `DiploScore_SendExpeditionaryForce` in the install's Lua. Accepted, the unit leaves its commander and it and all under it go to the host, each remembering who lent it first; the lender is told `OUREXPACC` and everyone else `EXPOTHER`. Declined, the lender alone is told `OUREXPDEC`.

A withdrawal sets only the date, the return define's days from now; nobody is told. Each day a host sends home **the first** unit past its date, so several come one a day. It goes alone: **what is under it stays with the host as the host's own**. `EXPRETURNEDWE` to the host and `EXPRETURNED` to the lender. No peace, change of alliance or end of a war sends one home.

## 4. What a lent unit is

The host's to order, but its leader is greyed: `compatibility.md`, entry 23. It shows the lender's flag on its counter and in the panel's `exp_force`, whose tooltip says who lent it. It may not be merged, reorganised, split, disbanded or attached to, or have a brigade's type changed, and `split_troops` and removing all leaders pass it over. Saved as `expeditionary_owner` and `expeditionary_date`, the end date while no recall stands.

**Its figures are its lender's**: a brigade's organisation and the regain of it, what the unit uses and who pays for it, its combat width, the combat modifiers and what it fires with, its attack delay, the point it shatters and the officers it loses and gets back. The lender goes on officering and reinforcing it, and the host neither counts it nor reinforces it. Its brigades' stored figures are left as they were when lent.

The passive upgrading by the sliders goes on as the original's, from the lender's. The upgrade command on the unit panel, a brigade changed to another type, is refused for a lent unit.

## 5. An annexation

What was lent to the country goes home and is shattered; what it lent comes back, to be destroyed with the rest.

## 6. Not built

The AI lending or giving back. A player asked to accept one, which only an AI's offer could raise. The exile's sending home.
