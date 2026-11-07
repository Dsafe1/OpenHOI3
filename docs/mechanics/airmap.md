# The air map mode

What the map shows when the air mode is on: the player's fighter missions as colours, rings of
range round the player's air bases, the air bases and anti-aircraft guns as large pictures, and
the air battles and bombings of the past week as icons. Written as rules this project can
implement. No addresses and no offsets. Each rule says how sure it is - `confirmed`, `likely`,
`inferred` in the fact base's vocabulary. Where this project chooses its own behaviour the
paragraph is marked **Ours**.

Sources: `reversing/findings/FINDINGS-airmap.md`, read for this, whole;
`reversing/findings/FINDINGS-combatview.md`, section 14 (the battle icons, read first and
corrected by the other); `reversing/findings/FINDINGS-combat.md` (the list of finished battles).

Code: `CCombatHistory` and `CFinishedCombat` in `lib/OpenHOI3.Sim/Combat` keep the list;
`CAirMapColours`, `CAirMapIcons` and `CAirBaseCircles` in `lib/OpenHOI3.Interface/Map` work out
what is drawn, and `CAirMapText` in `lib/OpenHOI3.Interface/Screens` what is said of a province;
`src/Map/CMapView` paints the colours and `src/Map/CAirMapLayer` draws the rest.

---

## 1. In short

- **Grey everywhere, green where the player's intercepts reach, red where its air superiority
  does, striped where both do** - the sea too. Bombing missions colour nothing. `confirmed`
- **A ring round each of the player's air bases for each range of aircraft based there**, as
  wide as they reach, green when their wings are full and red when they are spent. `confirmed`
- **The air bases and anti-aircraft guns as large pictures**, bigger with each level built.
  `confirmed`
- **Three icons to a province** for the past week's air battles, bombings of the player's side
  and bombings by it, out of **a list of finished battles that keeps a week**. `confirmed`
- What it says of a province: how many bombings and how many air battles it saw in the past
  week. `confirmed`

## 2. The list of finished battles

Every battle that ends - on land, at sea, in the air, a bombing of any kind, and a convoy raid's -
leaves an entry: **the hour it ended, its kind, its province, and one country for each side**.
`confirmed`

**A side's country is the first of the countries still in the fight on it**, or none where there
are none. A side keeps that list as it goes: a unit's owner is put on it as the unit joins, where
it is not there already; and **whenever a unit is taken off the side, the list is made again from
the units left on it**, in their order. A battle's end lets its units go without touching it.
`confirmed`; the order the list is made again in is `inferred`. So:

- **a beaten side has no country** - its units were taken off it one by one as they fell back;
- a side standing at the end keeps its countries;
- a ground bombing's defender is the province's holder, put on its list with no unit - until a
  unit is taken off that side, which makes the list again from the units and drops the holder.

The list a side keeps of every country it ever fought as is another and stays whole. *Ours:*
here that one is `CCombatSide.Countries`, which the battle windows read, and the first is
`CCombatSide.Standing`.

**It keeps a week.** At the start of each hour's battles, before any is fought, entries are
taken off the front of the list while the first ended more than **168 hours** before the hour;
and before that, any that ended after the hour. `confirmed`

## 3. The colours

Every province, land and sea, has its two colours - its ground and its stripes (`map.md`,
section 10) - and here both start **grey** (0.5, 0.5, 0.5). Then, for each of the player's units
under an **air intercept** or an **air superiority** order, each province of its mission that
the unit still reaches (`fleetsandair.md`, section 3, the range alone) is coloured: `confirmed`

| order | ground | stripes |
| --- | --- | --- |
| air intercept | **green** (0.35, 1, 0.35) | green too, where they are still grey |
| air superiority | red too, where it is still grey | **red** (1, 0.35, 0.35) |

So a province under both is striped green and red whichever came first, and a province under a
bombing mission, or under a mission of anybody else's, is grey. Each colour is the original's
packed byte over 255: 0x80, 0x58 and 0xFF. They carry an alpha of a half; what the original's
shaders make of that was not followed through. The ground under them is the original's
terrain map mode turned grey - the textured one, which is not built here; the maintainer,
2026-10-11, to leave that for later. *Ours:* on land a colour is laid at its alpha over the
flat colour of the province's terrain turned grey, and on the sea at 0.8 over the sea's colour -
the share the original's water shader gives a province colour
(`reversing/findings/FINDINGS-mapmode.md`, *The sea*). Painted
flat, the grey made the map one colour with no coast; this keeps the ground showing through,
as the original's does.

**When.** `confirmed`: when the mode is chosen; and, while it is on, when an intercept or an air
superiority order of the player's starts, and when one is gone. **Not each day**, and not when a
unit moves base and its provinces fall out of reach. *Ours:* each hour the player's orders of the
two kinds - which unit, which order, which provinces - are compared with the last hour's, and the
map coloured again where they differ: the same moments, seen from outside. The original colours
the map as the dying order is taken apart, and whether its provinces are still on the map then
was not read; here they are not.

## 4. The battle icons

Three counts for every province, out of the list's entries of **an air battle or a bombing of
any kind** at that province. With P the player, `confirmed`:

    the entry is "ours" where
        P holds the province (controls it) or owns it,
        or the defender is P,
        or the defender is a country other than REB that is on P's side
            (the same country, its overlord or puppet, or a puppet of the same overlord),
        or the defender fights a war beside P and P is not at war with it
    ours:       a bombing counts as "bombed us",   an air battle as an air battle
    otherwise, where the attacker is P, or P may operate in the attacker's ground
               (on its side, or fighting beside it and not at war with it),
               or P is at war with the defender:
                a bombing counts as "bombed them", an air battle as an air battle
    otherwise:  the entry is left out

"At war" counts REB as always at war, as the original's test does. Province 0 has none.

**A count is one byte, read back signed.** 128 entries of one kind in a province within the week
read as less than one and take its icon away. `confirmed` - kept as read, though nothing here is
likely to reach it.

| count | picture of `interface/mapitems.gfx` | where, from the province's point |
| --- | --- | --- |
| bombed us | `air_bombed_us_icon` | 3 north |
| bombed them | `air_bombed_them_icon` | 3 south |
| air battles | `air_battle_icon` | 3 east |

**Each shows where its count is above 0.** The original raises all three by 3 off the ground and
draws them at the type's `scale`, 3.5. *Ours:* the point is the province's own point
(`mapunits.md`, section 1), where the original's is its label's, which is not read; the height
is left out; and a picture is a square of twice its scale a side, as the buildings' are
(`mapunits.md`, section 9).

## 5. The large air base and anti-aircraft pictures

For every province with an air base, `large_air_base` at the air base's place; for every one with
anti-aircraft guns, `large_anti_air` at theirs. `confirmed`:

    n = the level built - what was built, not what works of it after bombing -
        where the player knows the province better than 1; otherwise 1 where there is one
    the picture's size = (n - 1) x 0.2 + 1 times its scale

So a level 10 air base is 2.8 times as large as a level 1, and one the player knows little of is
drawn at the size of a level 1. The air base's has the two frames of the small one, the second
with an air unit at home (`mapunits.md`, section 9).

**A click on the large air base is asked before any counter** and picks the next of the player's
air units based there, as a click on the small picture does in the other modes; the small
pictures cannot be clicked in this mode. `confirmed`. That is how an air unit at its base is
picked in the air mode, whatever stands over it. *Ours:* the small pictures are still drawn
under the large ones - whether the original hides them was not read; and the large pictures,
frames and all, are worked out with the icons once a day and when the mode is chosen, where
the original changes an air base's frame as a unit comes and goes.

## 6. The rings of range

For each **air base of the player's** - a province it holds with an air base, `inferred` from
the original's list of a country's air bases - with **units based there** (of any country):
`confirmed`

    the units, by range, shortest first - one of equal range after those already taken
    groups: going down them, a unit whose range is within 0.1 of the group before's joins it,
        and the group's range becomes that unit's; otherwise it starts a group
    for each group, a ring at the air base's place:
        radius   the range in kilometres / 4, in map pixels - which is exactly how far the
                 range reaches, a distance between provinces being four times their pixels
        share    the group's strength / the strength its wings have at their fullest
        dim      0.3 where the player has an air unit selected and it is not of the group;
                 else 1

**How a ring is drawn** (`gfx/FX/circle.fx`): the original's picture `map/Sector.dds`, a quarter
of a disc that is brightest at its rim and fades inward, drawn four times about the base to make
the whole disc, the radius at the picture's edge (its rim lies at about 0.9 of the radius). Its
colour goes from **red** (1, 0, 0) at no strength through **yellow** at half to **green** at full,
and it is seen through: its alpha is the picture's x 0.3 x dim. `confirmed`. **Since 2026-11-07 the rings are drawn
through `circle.fx` itself**, translated, on the map's surface - nine vertices, the picture mirrored into each quarter,
the ring 1.0 above the air base, no depth test (`mapdraw.md`, section 4; `reversing/findings/FINDINGS-airmap.md`,
section 8) - the 2D drawing kept for a map drawn flat.

The rings are made again each day, and when what is selected changes. `confirmed`. *Ours:* "the
range" is the shortest of a unit's wings', which is the range its orders use; the original takes
the unit's own type's, and which of its wings' that is was not read. Units are taken in the order
of their numbers where the original takes them in the order they came to the base.

## 7. What it says of a province

In the original this is the tooltip of the province under the mouse. `confirmed`:

    counted, of the list's entries at this province, where P holds or owns the province, or
    the entry concerns P:
        bombings       entries of a bombing of any kind
        air battles    entries of an air battle
    bombings above 0:     AIR_TOOLTIP_BOMBING, with NUM the bombings
    air battles above 0:  AIR_TOOLTIP_DOGFIGHT, with NUM the air battles

Bombings first. **An entry concerns P** where the attacker is P; or the attacker is a country
other than REB on P's side, or fighting beside P and not at war with it; or the defender is P; or
P is at war with the defender; or P may operate in the defender's ground. It is written
otherwise than the icons' tests and comes to the same: a battle the tooltip counts is one the
icons count, as ours or as theirs. `confirmed`, by setting the two side by side. *Ours:* the
original's own test is what is built, all the same.

Each is a line of the tooltip of the province under the mouse, after its name (`tooltips.md`,
section 7), built since 2026-10-11.

## 8. Turning it on

The button `mapmode_15` by the minimap turns it on (`minimap.md`, section 4), and `--mode=air` starts in it. The rings and icons are
drawn at every height of the camera, where the buildings are not drawn from far off
(`mapunits.md`, section 7); what the original's far view does with them was not read.

## Kept as read, though they look odd

1. **The colours are not made again each day**, nor when an order's provinces fall out of reach
   as its unit moves base - only when an order of the two kinds starts or goes.
2. **A count of battles is a signed byte**: 128 of a kind in a week hides the icon.
3. **A beaten side is named by nobody**, so a battle lost by the player's enemies over a
   stranger's ground counts by who attacked alone.
4. **A province the player knows little of shows its air base at the size of a level 1.**

## Not built

- **Intelligence** past what `CIntelligence` has (`aircombat.md`, section 6), and so what the
  player knows of anybody else's ground beside his units.
- ~~**The map mode buttons.**~~ Built 2026-11-07 (`minimap.md`).
- **The naval mode**, whose icons are of its own and were not read.

## Not established

- What the original's shaders make of a province colour's alpha in this mode.
- Whether the original hides the small building pictures in this mode.
- Which wing's range the original takes for a ring.
