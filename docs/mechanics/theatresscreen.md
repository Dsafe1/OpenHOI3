# The theatres screen

*Spec for `country_theatres`, behind the top bar's Theatres button: `reversing/findings/FINDINGS-topscreens.md`, sections A and D. Read out of the original, tried with `--click`, and the maintainer has not looked at it. The file's elements are `tfh/interface/country_theatres.gui`. Figures are exe-hardcoded unless a file is named.*

`CTheatreScreenText`, `CTheatreBuildSteps` (`Interface/Screens`), `CPlanWants`, `CPlanHoldScore` (`Sim/Agents`), `CTheatreView`, `CTheatreEntry`, `CAreaEntry` (`src/Screens`).

## 1. How it opens

The sixth country screen. The top bar's button opens and shuts it, shutting any other; `close_button` and Escape shut it. It does not pause the game, has no automation arrows and no tooltips, and clears nothing on the map.

## 2. The two lists

`my_theatres`: the player's own theatres, in their order; `allied_theatres`: each ally's in turn. A theatre whose headquarters is gone has no row. A row's name and shield are the headquarters', and its mark is the plan's power beside the enemy's: above 0.75 the first frame, above 0.5 the second, otherwise the third. All of a row is fixed when it is made. Only `background_button` answers, and chooses the theatre.

A selected theatre headquarters on the map is the theatre the screen opens on; otherwise the first row to be made, where nothing has been chosen. The choice survives shutting the screen. Where the chosen theatre has gone the first of the player's is shown, without being chosen.

## 3. The selected theatre

Filled **only when the choice changes or the screen opens**: it is a snapshot, and building from it does not refresh it.

- `title`, `country_icon`: the headquarters.
- `land_stance`, `air_stance`, `naval_stance`: `STANCE_*`.
- `forces`: every brigade under the theatre by type, in the database's order, `<n> £<type>` joined by commas.
- `immediate_needs`: section 6, or `NO_NEEDS_FOR_BUILDS`. `build` is enabled when there are needs and the headquarters is the player's.
- `objective`: up to three objectives by name, with an ampersand before the last; of four or more, `OBJ_AND_MORE` for the one scoring most - victory points × 20000, its `ic`, 1000 for a base of each kind, 10000 for its holder's capital; `NO_OBJECTIVES` where the plan is off or has none.
- `current_operation`: `THEATRE_INVASION` for each of the owner's invasions with a division of this theatre, run together; `NO_INVASION_PLAN` with none.
- `goto_button`: the player's own headquarters is selected and shown; an ally's does nothing; either way the screen shuts.

`odds_with_reinforcements`, every `*_label`, `theatres_front_bg` and `needs_label` are the file's and nothing fills them.

## 4. A front's row

A row for each of the theatre's fronts with two provinces or more whose enemy is no friend of the player's.

- The province that names it: of its provinces, each `4 × (1 − |i − n/2| / (n/2))` plus its infrastructure out of 1000, the highest above nought and the first of equals. With none above nought the row stays blank and answers nothing.
- `theatres_continent`: the first sprite that exists of `GFX_theatres_<province>`, `GFX_theatres_<region>` for each of its regions, `GFX_theatres_<enemy tag>` where the province is on the enemy's capital's continent, `GFX_theatres_continent_<continent>`.
- `title`: `FRONT_TITLE`.
- `odds_description` and `current_odds`: `ODDS_DEFEND_*` for stances up to Defensive and `ODDS_ATTACK_*` above, with the plan's ratio. **One figure for every row of a theatre**: the front does not enter it.
- `stance`: starts from the plan's stance and steps round five. **It changes only its own words** and the plan, the needs and the odds stay as they are - the original's, apparently unfinished.
- `goto_button`: centres the map on the naming province and shuts the screen; selects nothing.
- `forces_needs` and `build`: section 6, for the front's own provinces.

## 5. Build

A press runs steps until one answers no, each posting unit orders aimed at the theatre's headquarters, spending the country's manpower as it stands at the press. Sum what is needed into air, land and ships.

A candidate has a need above nought, may be built, and its limit leaves one. Its score is `need × 5000 + (draw mod (need × 10 + 1)) × 1000`, the highest strictly, so the first of equals.

1. **Air above land / 4**: the best air type, or ships if none. Lines of the need's integer square root, `need / root` of them; each cut to what the men left will raise, stopping at 2, then charged; below nought the step answers no.
2. **Else land above 2**: one division. First the types of priority over 4, then those of 4 or less, a brigade at a time taking each off the needs - up to one short of the most a division may have, then the most. **The needs stay down even if the division then cannot be ordered.** If it can: one order, charged, and the step answers yes. If not, step 3.
3. **Else land above nought**: the best land type as in step 1, or ships if none.
4. **Otherwise ships**: as step 1. No ships: the step answers no.

*Ours:* the original draws on a generator on the stack that is never seeded, so a pick is arbitrary; the screen has a generator of its own. A step's men are `BuildCostManpower` at the type's base figure, where the original's also applies the country's technology: only the size of a line can differ, the charge being the command's.

A queue line remembers the headquarters (`target`, saved as the original's).

## 6. What a plan is short of

Nothing here changes the game. The original refills an inactive plan once a day, unsaved; **we work it out as the screen is filled.** An active plan at the top of the chain - an AI's - answers from its own `min` and powers, and a row then shows the same as the theatre.

**Short of**, per type: `difference` is wanted less what the unit and all under it hold, signed. Each difference adds to its group's total. Then for each type with a positive difference, in database order, the lesser of that and what its group is still short of is taken: that is the need, and the group's total comes down by it. A surplus of one type so cancels a shortage of another in its group.

A group is a type's representative: a carrier, a capital ship the battleship, a transport or submarine itself, another ship the destroyer; a land type with width and not the headquarters brigade the armour brigade under softness 0.5, a paratrooper, mountain or marine type itself, otherwise the infantry brigade; anything else itself.

The queue's lines aimed at the headquarters are then taken off: each brigade of a line, its size.

### 6.1 Wanted

Roles fall back along chains, the first the country has unlocked and may use: armour to `light_armor`, `mechanized`, `motorized`, infantry; fast to `motorized`, `cavalry`, infantry; mountain and garrison to infantry.

**Held** counts brigades by role; in a front's case a land unit only where it stands on or is bound for the front. **Enemy** is the unfriendly land beside the provinces, each province once: its brigades by land, armour, fast and air, its mountains. The plan's own area is empty for a played country, so a theatre's enemy is nothing.

Scale `s` in thousandths: for a front **200**, always - the original's look-up at war asks of the country's own tag and finds nothing. For a theatre 1000 at war; at peace 200, replaced by the top threat's weight × 5 where above nought and raised to the top antagonism's × 5; held to 100..1000.

Stance factor `k`: Defensive 1.5, Attacking 2, Blitzing 2.5, otherwise 1. `B = enemy × k`, `A = B × 1.5k`, each truncated, and likewise for armour and fast.

Size `S`: for a front, provinces × `s` × 9 / 1000; for a theatre the level's base × `s` / 1000 - 3 a division, 9 a corps, 27 an army, 81 an army group, a theatre the sum of what stands directly under it, at least 9. At peace `S` is held to IC in thousandths × 10 for a faction leader, 4 for a member, 1 otherwise, × 4 more unless the ruling group is democracy; this binds only with almost no IC. At least 3.

`H = max(S, A)`, `L = max(S/2, B)`. Armour `max(H/5, Aa)` and `max(L/5, Ba)`, the fifths only where an armour type resolved to something other than infantry (*inferred*). Fast `max(H/4, Af)` and `max(L/4, Bf)`. Infantry the rest.

**Wanted is net of what is held**: each role adds `max(0, min(low, high) − held)`. Infantry takes `high − held` steps, each choosing a mountain type while its target counts down to what is held, then a garrison likewise, then infantry, adding only while `low` counts down above what is held. The mountain target is 3 × the greater of mountains held and faced; the garrison target is the count of held provinces of the front or theatre scoring 25 or more.

**Three things look wrong and are the original's**: a shortage is taken off twice, once here by role and once in the difference, so a front almost always says it needs nothing; the tests are `>=`, so one mountain step and one garrison step are taken with no call for them; and the scale of a front is fixed.

Aircraft: 9 (an army 3, a corps nought) × `s` / 1000 and half of it, at a theatre or army group at least 3 and 1, an army 1; each at least the enemy's air. Tactical bombers take two thirds, interceptors a third.

### 6.2 Powers

An active plan's own. For an inactive one `our` is the sum over its brigades of value × strength share × organisation share, and `their` the enemy's raw value; with no enemy that gives the third frame and the `BAD` sentence, which is how a played country's theatre always reads.

### 6.3 A province's garrison score

`CPlanHoldScore`, truncated; nought under infrastructure 0.2. A province with no victory points and no depot of its own beside a war candidate is nought. Otherwise the threat of unfriendly neighbours of 100 or more, the sum over the count, a twentieth, plus the count (France and Belgium towards Belgium and Luxembourg count 4000 while Germany holds land); three times the fort level beside a near neighbour; coastal fort `5 × (level + 1)`; air and naval bases, Germany's and Italy's on a curve, doubled in a small area away from the capital's; victory points owned at the greater of their share and `2 × points + 10`, held at the second; 100 for the acting capital and for a depot of its own, 60 for a strategic resource; its IC by its revolt risk; 30 and 2 a ship at a naval base; a quarter of an objective's priority; and for an AI's unit the province's `defend_prov` entry.

## 7. What is not settled

- Whether a played country's inactive plan can carry an area of operations; none does here.
- Whether a threat list can hold the country with the null id, which would change the fixed scale of a front.
- What a line's target does on delivery: nothing is known of it here but what the screen reads.

## 8. The production screen's list

*From `reversing/findings/FINDINGS-smallbatch.md`, section 3.* `ai_need_queue` is the same needs, a row for each of the played country's theatres whose headquarters stands, for any country. `CAiNeedEntry`. A row has the headquarters' name, the counts with **the trailing comma the theatres screen cuts**, or `NO_NEEDS_FOR_BUILDS` and `build` greyed, and `build` orders as the screen's Build does, with no test of whose it is. The line's tooltip names each type, a comma a new line. *Ours:* the row is worked out again as the hour turns or the queue's length changes and not each frame, and is not hidden while a builder window is up, the windows standing over it.
