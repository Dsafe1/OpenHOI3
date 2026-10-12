# The alignment triangle

Where each country stands between fascism, democracy and communism, and how it drifts. Brought into
diplomacy's first piece on the maintainer's word on 2026-10-12, because threat is weighed on it
(`diplomacy.md`, section 5); joining, leaving and being invited to a faction stay with factions.

Sources: `reversing/findings/FINDINGS-alignment.md` (the start, the schedule, the terms' gaps, the
clamp, read 2026-10-12; the start save reproduced for 93 countries of 101 with its inputs given) and
`reversing/findings/FINDINGS-politics.md`, sections 1 to 3 (the classes, the distances, the seven
terms). Thousandths throughout; every division cuts toward nought.

## 1. The corners

`common/ideologies.txt`'s groups, in the file's order - Their Finest Hour's fascism at (200, 200) with
the faction `axis`, democracy at (0, -146) with `allies`, communism at (-200, 200) with `comintern` -
each with its ideologies, its position and its faction's `tag`, `neutrality` and `influence`.

**The triangle** is the box of every group's position, grown from a box of nothing at (0, 0): its
apex the middle of the box's bottom, its two upper corners the box's top. With the middle `mid = minX +
width - width x 1000 / 2000`, the left edge's slope is `-height x 1000 / (mid - minX)` and the right's
`-height x 1000 / (mid - maxX)` (-1 where a divisor is nought) - **both measured from x 0**, as the
original's are, wherever the apex is.

## 2. Where a country starts

At its history's `alignment = { x y }`, **as written - nothing clamps it** - or at (0, 0) without one.
Its ruling ideology is its history's `ideology`.

## 3. When it drifts

At the head of a day, once a country's next step is due: **if the hour is past the one set, it steps,
and the next is set the interval's days later** - `ALIGNMENT_INTERVAL` x 24 hours. The test is strict,
so **a step comes every interval and one days**: 25 for Their Finest Hour's 24. A new game's first step
is due at once, so **every country takes one step on the first of the two days a new game runs**
(`tick.md`, section 6), and the second day, the clock not having moved, takes none. The original does
not save when the next is due, so a loaded game would step everyone at once; no game is saved here.

## 4. A step

- **A country in a faction**: its leader - the first of its members - stays where it is; every other
  member is put where its leader stands.
- **A country in none**: its drift (section 5), kept inside the triangle (section 6), is added.

## 5. The drift

For each group g, with M the members of g's faction:

    w = Relation + Ideology + Proximity
    if (dy^2/1000 + dx^2/1000) / 1000 < 40000:                 (dx, dy) = g - position: nearer than 200
        w -= Revanchism + Threat + Repulsion
    w += Influence

SUSC is the country's `suseptibility` modifier, S its `suseptibility_<g's faction>`, and **"base IC" the
IC of its provinces before any of its own bonuses**, `base_values`' 5 included (`ic.md`); a term marked
*susceptible* is multiplied by (1000 + SUSC) / 1000 and then by (1000 + S) / 1000.

| term | |
| --- | --- |
| Relation | the mean of the country's relation with each member, x `RELATION_WEIGHT`, susceptible; nought with no members |
| Ideology | where its ruling ideology is g's: `IDEOLOGY_WEIGHT` x (1000 + `drift_speed`) / 1000 |
| Proximity | the best over the members of `r x 1000 x t / 1000`, `t = max((5000000 - 1000 x d) x 1000 / 500000, 100)` - 10 less a five-hundredth of the distance between the two capitals, at least 0.1 - and `r` **the member's base IC divided by the country's, a whole number**; x `PROXIMITY_WEIGHT`, susceptible |
| Revanchism | the country's cores held by a member of g's faction, x (1000 - s) x 5 each, s the faction's share of the world's victory points in thousandths - nought from half; x `REVANCHISM_WEIGHT`, susceptible |
| Threat | the most threat it feels from a member, where any is above nought, x `THREAT_WEIGHT`, susceptible, x min(total IC x 1000 x 1000 / `LARGE_COUNTRY_IC`, 1000) / 1000 |
| Repulsion | `max(200000 - 1000 x r, 0) x REPULSION_WEIGHT / 1000`, r the whole distance by Newton's root; where its ruling ideology is of another group, x f / 1000 if f is over 1000, `f = (base IC x 1000 x REPULSION_IC_FACTOR / 1000) x 1000 / (w x 1000)`, w the faction leader's base IC and a quarter of the rest's, f 100000 where w is nought |
| Influence | where it has the static modifier g's faction's `influence` names: `b x (a x v / 1000) / 1000`, v that modifier's `align_towards`, `a = (100000 - max(leader's effective neutrality, 0)) x 1000 / 100000`, `b = max((100000 - its effective neutrality) x 1000 / 100000 + 250, 250)` |

**The vector**: for each group the country does not stand exactly on, the length of the line in single
precision - `floor(float(float(sqrtf(float(float(s) / 1000.0)) + 0.0005) x 1000.0))`, s the squared
distance - its inverse `1000000 / length`, the unit vector `dx x inverse / 1000`, `dy x inverse / 1000`,
and the drift adds `unit x w / 1000` on each axis. So the inverse is cut to a whole number, and a far
corner pulls less than its weight: about 5% short at 95 away, a quarter at 374, half beyond 500.

## 6. Keeping the move inside

Each test on the drift as asked: past the box's right, the move is to it; **below its left, the move is
`x - minX`, the sign reversed**; past its top or bottom, to it. Then, at the height that gives, the move
is put back inside the left or the right edge. The reversed arm is hidden in Their Finest Hour, anything
left of the box being left of the edge too. It is what puts a history point outside the triangle onto
its edge on the first day - the USA's and Canada's.

## 7. The group a country counts as

For threat (`diplomacy.md`, 5.1): its faction's group, or else **the group nearest where it stands**,
the first of equals. **The factor** between a feared country and a group is their squared distance
over `FACTION_THREAT_DIST` squared, x 1000, uncapped.

## Ours

- **Faction progress is not kept**, so its share of the victory points is nought and revanchism weighs
  every held core in full - which is what the original's first step has, and not what its later ones do.
- **No influence is built**, so no country has an `align_towards_*` modifier and the influence term is
  nought.
- **A capital is its acting capital**: the two differ in the original only for a government in exile.
- **The AI's opening laws and the cabinet's filling are not made** - the original's `Laws(country)` and
  its filling of empty posts as a game is entered (`lua.md`, section 5). They change many countries'
  drift speed and susceptibility before the first step, so **the first step agrees with the start save
  for 50 countries of 101**, the 11 of the factions among them, and for 67 with the save's laws given
  (`OpenHOI3.SaveCheck diplomacy`); the misses are of those inputs, not of the drift.

## Not established

- When the original's faction progress is first worked out, and by what.
- Which list a country's `align_towards_*` is looked for in.
- What the original's AI changes before the first step and what after (`FINDINGS-alignment.md`, 2.2).
