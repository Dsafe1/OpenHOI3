# Revolt risk

How much a province is minded to rise: an accumulator the day moves while its holder is at war with its owner, held
down by the troops in and around it, and the figure everything reads - made of it, the holder's and the province's
modifiers and that suppression. Read out of the original on 2026-09-30 and 2026-10-14
(`reversing/findings/FINDINGS-occupation.md`, sections 1 and B; `reversing/findings/FINDINGS-revolt.md`, section 3).
`confirmed` unless marked.

This is the second of the four pieces occupation and revolt were cut into (`occupation.md`): what revolt risk *does* -
the rising itself, the rebels - is the third (`rebels.md`), and the underground the fourth.

## 1. In one line

    each day, at war:  risk += (risk > support ? -0.1 : +0.1) - min(suppression / 10, 0.2), held to [0, support]
    what is read:      risk (against an enemy) + global_revolt_risk + local_revolt_risk - suppression
    what is shown:     that, never below minimum_revolt_risk nor nought

All figures are **points of revolt risk**, in thousandths - `local_partisan_support = 1.5` is 1.5 points, the save's
`revolt_risk=1.500`.

## 2. Suppression

Worked out once a day for every province, in its own day (section 3), and kept: a province's **suppression**.

**One province's figure**, P: for each unit standing in it whose owner is a **friend** of the province's holder - the
holder itself, its overlord or puppet, its faction, an ally, a co-belligerent, one it has military access through
(the original's `IsFriend` with access) -

    ratio = its brigades' strength x 1000 / their most strength            truncated
    P    += ratio x the sum of its brigades' suppression / 1000             truncated, unit by unit
    P     = P / 10                                                          truncated

So a division's suppression is its brigades' together, scaled by how whole it is, and **a full brigade with
`suppression = 1.00` gives 0.1**.

**The province's suppression**, S: its own P, and the P of every **land province within two edges under the same
holder** - every province reached by one or two of the original's edges, sea provinces among the steps, each once.
**The province itself is among them**, so **its own units count twice**.

- S = 1.0 - ten full brigades of suppression 1, or one division of three garrison brigades in BlackICE - stops the
  climb; S = 2.0 turns it down a tenth a day.
- Which units are friends is asked of the holder, so an ally's garrison counts.

*Inferred*: a brigade's most strength is taken as everywhere else here (`units.md`) - the original's unit asks a slot
of its own that was not read; the save's 30 of a militia brigade agrees.

## 3. The day

**Each province's own day**, after every country's, one province after another and each done before the next - the
original's `RunDailyProvincePass`, as far as it is built:

1. the repair (`bombing.md`, section 11);
2. **its suppression** (section 2);
3. its modifier made again, where the repair carried a building across a whole level (`modifiers.md`, section 3);
4. **the revolt risk's step**;
5. the return of land held at peace (`war.md`, section 11).

A province handed back early in the pass is no longer its holder's when a later one's suppression is counted.

**The step.** Where the province's holder is not its owner and **is at war with it - or either is the rebels**:

    support = the province's local_partisan_support
    step    = risk > support ? -0.1 : +0.1
    cut     = min(S x 0.1, 0.2)                    S x 100 / 1000, truncated
    risk   += step - cut
    if risk > support: risk = support

and then, in every province whatever its holder, **never below nought**.

- `local_partisan_support` is the **ceiling**: Their Finest Hour's policies give 1.5, 7, 11 and 19
  (`occupation_policies.txt`). With no policy - a province its owner holds - it is nought.
- **The step of 0.1 and the cut's 0.2 are the executable's own**; no define touches them.
- **Revolt risk freezes once the war stops.** It is moved only at war; land handed back at peace keeps what it had,
  and is only floored at nought. It never decays.

**A new game**, as it is entered and before its first day: every province with no revolt risk is given its
`local_partisan_support`, and every province's modifier is made again. Nothing has a support there in Their Finest
Hour, so it does nothing.

## 4. The figure read

What the conditions, the rebuild, the map mode and the province view read, in thousandths of a point - and it can be
below nought:

    effective = 0
    if the holder is an enemy of the owner (at war, or either the rebels):  effective = risk
    if the province has a holder:  effective += global_revolt_risk of the holder - of the owner where the rebels hold it
    effective += the province's local_revolt_risk
    effective -= its suppression

**What is shown** is `max(effective, max(minimum_revolt_risk, 0))`, the province's `minimum_revolt_risk`.

**The province's modifier**, as its last step (`modifiers.md`, section 3, step 14): where what is shown - asked of the
province's figures as they stand at that point of the rebuild - is above nought, `static_modifiers.txt`'s
**`revolt_risk` block times it**, each value times the points over 1000. Their Finest Hour's is `local_ic`,
`local_manpower_modifier` and `local_resources` at -0.02 each: **2% off each per point**, so a province at its
policy's ceiling of 19 loses 38% more of each.

## 5. Conditions, the effect and the history

| key | asks | true where | on a country |
| --- | --- | --- | --- |
| `revolt_risk = N` | the province's figure shown | **strictly above** N's whole part x 1000 | false |
| `pure_revolt_risk = N` | the effective figure, which may be below nought | strictly above N's whole part x 1000 | false |
| `num_of_revolts = N` | how many provinces the country owns that **the rebels hold** | at least N | |
| `revolt_percentage = N` | that count x 1000 over the provinces it owns | at least N, in thousandths: 0.3 is 30% | |

`revolt_percentage` is false for a country that owns nothing.

**`revolt_risk = N`, the effect**, **adds** N to the province's revolt risk, never below nought, and tests no
province: in a country's scope it changes province 0. **A province history's `revolt_risk = N` sets it.** Neither is
in Their Finest Hour or BlackICE.

## 6. Against the saves

`OpenHOI3.SaveCheck revolt <install> <earlier save> <later save>` sets the world as the earlier save has it - owners,
holders, revolt risk, the policies and every division of the start's orders of battle where it stands with its
brigades' strength - and steps every province once.

- **`Germany1936_01_07_22.hoi3` to `_01_08_02`**: Italy took 9987 at 19:00 on 7 January. Its suppression comes to
  **0.692** - its own militia division counted twice (98 + 98), 9995 one edge out (331), 10003 (98) and a corps
  headquarters two edges out at 10022 (67) - and the day's step to **0.031**, the save's figure: **1 of 1**.
- The six Ethiopian provinces of 20 April (0.081, 1.375, 0.974, 0.147, 1.500, 1.500) were taken between 7 January and
  12 February and went back to Ethiopia on 5 March: they are frozen since, and the two at 1.5 sit at
  `collaboration_government`'s ceiling. The other four cannot be followed without saves between.

## 7. What the player is shown

**The province view** (`provinceview.md`, section 2):

- `text_partisans`, beside `partisans_icon` (`PV_RR` "Revolt Risk"): what is shown, to one place, cut, and a percent
  sign - `§Y1.5%`, yellow only above nought - **only where the player knows the province at 9**; below, "?".
- **Its tooltip**: where the fog of war is on and the player knows the province below 2, `CANT_SEE_UNDER_FOW`
  "This is not possible to determine without further access to the area."; otherwise, a line each, trimmed:
  1. `LIKELY_REBELS` "Likely Rebels" and the `<type>_title` of the rebel type the province would choose, where it
     would choose one (`rebels.md`, section 4);
  2. `PARTISAN_SUPPORT` ": §R+" and **the accumulator** to two places, where it is above nought and the holder is at
     war with the owner - or either is the rebels;
  3. the holder's sources of `global_revolt_risk` - the owner's where the rebels hold it (`modifiers.md`, section 8);
  4. the province's sources of `local_revolt_risk`;
  5. `SUPPRESSION` ": §G-" and the suppression to two places, where there is any;
  6. always, `MINIMUM_REVOLT_RISK` ": §Y", the province's minimum to two places, "%§W".

**The revolt map mode** (the original's mode 12, `mapmode_5`, `MAPMODE_5` "Revoltrisk Mapmode"). Land is coloured by
what is shown, at a brightness b of 1 where the player knows the province at 2 or more and 0.5 below:

| | colour |
| --- | --- |
| the player does not hold it and knows it below 3 | grey (0.9b, 0.9b, 0.9b) |
| above 4 points | red (f x b, 0.3b, 0.3b), f = min(1, 0.7 + 0.02 x points) |
| from 2 points | orange (0.9b, 0.6b, 0.3b) |
| above nought | yellow (0.9b, 0.9b, 0.3b) |
| nought | green (0.3, 0.9, 0.3), **not dimmed** |

**An underground anywhere in the province paints it red** (b, 0.3b, 0.3b) (`undergrounds.md`, section 8).
The map's tooltip adds nothing in this mode. The button `mapmode_5` turns it on (`minimap.md`, section 4), and `--mode=revolt`
starts in it.

**The alert** `alert_revoltrisk_in_provinces` ("Possible Partisans") is made and sorted every pass in the original and
**never shown**: none of its calls passes it. It is not built.

## What looks wrong and is built so

- Revolt risk freezes once the war ends, and never decays.
- Suppression counts a province's own units twice and reaches two edges, across the sea too.
- The effect adds where the history sets.
- Every province of a new game at nought is first given its partisan support.
- The tooltip's "Partisan Support" is the accumulator, not the support; the figure has one place and the tooltip two;
  the figure needs intelligence 9 and the tooltip 2.
- Nought is bright green on the map whatever is known.
- `revolt_risk` and `pure_revolt_risk` are strictly greater.

## Ours

- **The fog of war is always on**, its switch not being built (`remove_fow`).
- **The province's day keeps each province's own suppression for the pass** and works it out again for one whose
  holder changes in it; nothing moves a unit during the pass, so it is the original's figure.
- **The map mode is coloured again once a day**, as the supply mode is; when the original colours it again was not read.

## Not established

- The original's slot for a unit's most strength (section 2).
- Where the four lower Ethiopian figures of 20 April came from.
- The AI's two readers of revolt risk (`FINDINGS-revolt.md`).
- Which tooltip slot the conditions' words sit in.
