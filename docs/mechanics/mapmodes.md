# The map modes

The ten map modes built on 2026-11-07, beside the six built before them - terrain and political (`map.md`, section 10),
weather (`weather.md`, section 6), revolt risk (`revolt.md`, section 7), supply (`supplymap.md`) and air (`airmap.md`).
Each is chosen by its button by the minimap (`minimap.md`, section 4) or with `--mode=` and the mode's name. Read out of
the original in `reversing/findings/FINDINGS-mapmodesfigures.md` (infrastructure, victory points, strength, resources),
`reversing/findings/FINDINGS-mapmodescountries.md` (political, diplomatic, intel, theatre) and
`reversing/findings/FINDINGS-mapmodesrest.md` (simplified terrain, region, naval); where a resource icon stands is
`reversing/findings/FINDINGS-resourceicons.md`. **All of it was read and tried with `--screenshot`, and it
has not been reviewed in play.**

A figure marked **[exe]** is fixed in the original's code; one marked **[mod]** comes from a file a mod may change.

Throughout, **b** is a province's brightness: 1 where the player knows it at 2 or more, a half below
(`intelligence.md`), as the original's political mode dims it. A colour "× b" is each part times b. Its colours are
`Interface/Map`, one class a mode, and its tooltip lines `Interface/Screens/CMapModeText`; `src/Map/CMapView` chooses
between them.

## 1. When a mode is coloured again

The original colours a mode again in three ways [exe]:

- **When it is chosen**, every mode.
- **On a change of hands or of the diplomatic statuses** its refresh flag follows - a war declared, an alliance, a
  non-aggression pact, military access, a faction joined or left: the political, intel, diplomatic, theatre,
  infrastructure, victory point and strength modes, and the revolt and supply modes built before.
- **Never otherwise** for the resources, region, simplified terrain and naval modes, but that the naval mode is
  coloured again when one of the player's patrols, raids or escorts starts or goes.

**Nothing colours a mode again when a unit moves** - so the strength mode shows where units stood when it was last
coloured, as the original's does.

*Ours:* the session keeps a figure of the wars, pacts, factions and military access (`CMapModeTriggers`) and of the
player's naval missions (`CNavalMapColours.Figure`) and colours again when one changes. The intel mode is coloured again
each day as well: the original's follows a pass over what the player's units see whose caller was not found.

## 2. Infrastructure

**Black** where the player knows nothing of the province; else a ramp by the level of its infrastructure modifier - the
modifier in tenths, rounded down, worked in single precision as the original works it - with no stripes and no dimming
[exe]:

| level | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| red | 0.1 | 0.6 | 0.8 | 1 | 1 | 0.65 | 0.1 | 0.2 | 0.25 | 0 |
| green | 0 | 0 | 0.1 | 0.3 | 1 | 0.75 | 0.4 | 0.5 | 0.73 | 1 |

Level 0 is black too. The modifier is the province's whole, `LOCAL_INFRASTRUCTURE` and `GLOBAL_INFRASTRUCTURE` folded
in, and a bombed province shows what is left.

**Tooltip**, where the player knows the province at 1 or more: a line holding only the percentage, to two places and
cut, with no label - `70.00%`.

## 3. Victory points

**Grey** 0.9 × b; where the province has victory points, one of three hues by its **controller** [exe]:

- **green** (0, k, 0) - the player's own, or a member of its faction;
- **red** (k, 0, 0) - at war with the player, or the rebels;
- **yellow** (k, k, 0) - anyone else;

with k = (points / 15, whole) × 0.3 + 0.7, at most 1 - so full from 15 points - and each × b.

**Tooltip**, always, at sea and with none: `Points: 5` (`PROV_POINTS` [mod]).

## 4. Strength

**Grey** 0.5 × b. Where the player knows the province at 2 or more and land units stand in it - anybody's - a hue by
its controller, k = min(T, 500) / 500 × 0.5 + 0.5 [exe]:

- **green** (0.3, k, 0.3) where the controller is a friend - itself, an overlord or puppet, the same faction, an
  alliance, at war beside it and no enemy;
- **red** (k, 0.3, 0.3) where it is an enemy;
- **blue** (0.3, 0.3, k) otherwise;

each × b. T adds up what each land unit weighs **by what the player knows of the province** [exe]:

| known | a unit weighs |
| --- | --- |
| 2 | 1 |
| 3 to 5 | strength × 0.01 × (its number mod 37 + 15) - a figure that looks like a guess and is fixed by the unit |
| 6 and 7 | strength × 0.01 × organisation |
| 8 and 9 | strength × 0.01 × organisation × (experience + 1) |

A unit is a division and every unit in its tree; fleets and air units are not counted. **Tooltip**: nothing of its own.

## 5. Resources

**The colours** [exe]: grey 0.3 × b, or **green** (0.3, v, 0.3) × b by the province's IC, v = min(IC / 10 × 0.5 + 0.5,
1), and v = 0.5 where the player knows nothing of it. Over that, **a strategic resource is striped**, undimmed - solid
where the province has no IC:

- **blue** (0.247, 0.424, 1) where the player is given it (`resources.md`);
- **red** (1, 0.153, 0.153) where the province's controller is an enemy;
- **yellow** (1, 0.847, 0) otherwise.

**The icons**, made **once, as a game is entered**, from what each province can yield then - so a resource won or lost
in play changes no icon until the next game - and drawn **only in this mode and only under a camera height of 500**
[exe]:

- `resource_oil`, `resource_metal`, `resource_energy`, `resource_raremat` of `interface/mapitems.gfx` [mod] for each of
  crude oil, metal, energy and rare materials the province can yield at all, **each on a building's point**: the
  building whose place in `common/buildings.txt` [mod], counting the original's own `nobuilding` first, is the good's
  place in the goods pool - the 3rd to the 6th building of the file. Their Finest Hour's puts them on `coastal_fort`,
  `land_fort`, `anti_air` and `radar_station`; BlackICE's on `coastal_fort`, `beach_defence`, `land_fort` and `fortress`.
  So a built fort's picture stands on the icon that shares its point. **Where positions.txt gives the province no point
  for the building, the icon stands on the province's label point** - observed in the original on 2026-11-07, correcting the
  fact base, whose reading had inferred the map's corner (`reversing/findings/FINDINGS-resourceicons.md`, section 4).
  BlackICE's file gives `beach_defence` no point at all, so its metal icons stand there.
- `STRATEGIC_` and the resource's key on the province's **label point**, the middle of its box.

Each is drawn at its type's scale, through `billboard.fx` as the air mode's pictures are.

*Ours:* the icons' heights are left out.

**Tooltip**, where the player knows the province at 1 or more: each of metal, energy, rare materials and crude oil -
**crude oil last** - that it can yield, `Metal: 4.0/4.0`, now and at most to one place, rounded (`METAL_IRO`,
`ENERGY_IRO`, `RAREMAT_IRO`, `CRUDE_OIL_IRO` [mod]); then at any intelligence its strategic resource's name in green.

## 6. Simplified terrain

Each province in its terrain's `color` from `map/terrain.txt` [mod] × b, each part held at 1. The water drawn as the
terrain mode draws it. **Tooltip**: the terrain mode's part.

**The sea, in an enhanced game** (the fix `simplified_terrain_sea`, 2026-11-21; `docs/enhanced.md`, *What is built*).
The original works out a colour for every sea province in this mode and then draws the water plain: the mode's map
style, 8, chooses the water that does not read the provinces' colours, which only the styles of the air and naval modes
do [exe] - a fault, kept in the faithful game. With the fix the mode sets the air mode's style, 18, and each sea
province is handed on in its terrain's colour × b as the land is, so the water is drawn from it by the sea's Color
techniques (`mapdraw.md`, section 5). Nothing else of the mode changes: its layers, its tooltip and the land are as
they were, and the infrastructure mode, plain too, stays plain (`CSimplifiedTerrainColours.ColoursSea`).

## 7. Region

Each province in the colour of **the first region that lists it** × b, grey 0.5 × b where none does; no stripes
[exe]. A region's colour is handed out from `interface/colors.txt` [mod], one `color` block after another in the map's
order of regions (`map/region.txt` [mod]); an empty region takes none, and **where the list runs out the region met
then keeps white and the list starts again** [exe]. With no colors.txt every region stays white.

**Tooltip**: each region that lists the province, in its list's order, `\n - ` and its name, with `*` after a region
used for peace (`peace = yes`); then the province's continent with its first letter upper-cased. A region's name is its
key's localisation, or where the key has none and the region has provinces, `REGION_NAME` with `NAME` its first
province's name - "$NAME$ Region" in Their Finest Hour [mod].

**Continent painting**, the original console's `cpaint` - an editor switch left in the release, which nothing in a game
sets: the region mode then colours each province by its continent × b, grey where it has none, and the tooltip leaves the
regions out. A continent's colour comes from `colors.txt` from its first colour again, in `map/continent.txt`'s order, with
no test for an empty one [exe] - Their Finest Hour's europe 4 144 178, asia 181 72 106, africa 63 125 214. Built since
2026-11-07; *ours*, it is a switch of the debug menu (Shift and 4), as the province readout is, there being no console. A
continent past the colour list's end keeps white, which the reading leaves open.

## 8. Diplomatic

**The ground** by how the player stands towards the province's **owner**, the first that holds - the controller is not
asked, and nothing dims it [exe]:

| the owner is | colour |
| --- | --- |
| the player | (0, 0.9, 0) |
| at war beside it | (0, 0.8, 0.9) |
| its puppet or its overlord | (0, 0.7, 0) |
| in its faction | (0, 0.9, 0.7) |
| its ally | (0, 0.7, 0.7) |
| at war with it | (0.7, 0, 0) |
| anyone else, or nobody | 0.5 grey |

**The stripes**: the player's own land that is no core of its dark grey (0.4); any core of the player's green (0, 0.9,
0), whoever owns it.

**Tooltip**, on land with an owner: `\n ` and the owner's name, a colon, and the first of [mod]: `ATWARWITHUS`,
`OURWARALLY`, `OURVASSAL` - **where the player is the owner's puppet**, whatever its name says, and no table defines the
key, so it shows as itself - `DIP_THEY_PUPPET` with the player's name, `ALLIEDWITHTHEM`, `WEGUARANTEED`,
`THEYGUARANTING`, `NEUTRAL`; the player's own province nothing after the colon. **Factions are not asked**, so a member
with no alliance reads as neutral while it is coloured as the faction's.

## 9. Intel

By what the player knows of the province, land and sea alike, no stripes [exe]: dark red (0.3, 0, 0) at nothing, olive
(0.2, 0.3, 0) at 1, and from 2 green (0.2, min(level / 9 + 0.2, 1), 0.2) - full at 8.

**Tooltip**, at sea too: the level's name - `INTEL_NONE`, `INTEL_STRUCTURES`, `INTEL_UNITS`, `INTEL_2` to `INTEL_7`
for 3 to 8, `MAX_INTEL_LEVEL` [mod].

## 10. Theatre

**Grey** 0.5 × b everywhere; then each of the player's theatres, the k-th of its list, in the colour **of the map's k-th
region** × b (section 7) - on every province it holds, **its fronts' among them** (the saved `provinces` list leaves
those out; the original paints from the whole one), and on each of its focus areas holding more than one province where the
area's controller is not the player [exe]. A later theatre colours over an earlier. With more theatres than regions the
rest stay grey.

**Tooltip**, where the province's controller is a friend of the player's: the headquarters of the province's theatre in
brackets, `(Heeresgruppe Nord)` - so an ally's province names the ally's.

## 11. Naval

**The colours**, land and sea, every one at half alpha [exe]: grey (0.5); **green** (0.25, 1, 0.25) where the player's
fleets patrol - the province and the open water beside it - and along each escort's beat, the convoy's path from its
beat's first province to its last; **red stripes** (1, 0.25, 0.25) where they raid convoys, the province and the open
water beside it. Closed water beside is left out. The water is drawn from these colours, as in the air mode.

**The four icons**: for each province, whether the past week's naval battles there hold one of four kinds - **whether,
never how many** [exe]. With P the player, a battle is **ours attacking** where P attacked, or a country on P's side or
fighting beside it and no enemy - the rebels never - or P is at war with the defender; **ours defending** where P
defended or may operate in the defender's ground. A convoy raid sets "convoys we sank" if ours attacking, else "our
convoys raided" if ours defending; any other naval battle "we attacked", else "we were attacked". A naval bombing is
not counted, and nor is who owns the province.

| icon | `interface/mapitems.gfx` [mod] | from the province's point |
| --- | --- | --- |
| our convoys raided | `naval_convoy_raided_icon` | 3 east |
| convoys we sank | `naval_convoy_sunk_icon` | 3 west |
| we were attacked | `naval_raider_defended_icon` | 3 north |
| we attacked | `naval_raider_attacked_icon` | 3 south |

**The "raider" icons are about every naval battle that is not a convoy raid**, whatever the fleets were doing.

**The large naval base**, `large_naval_base` at the naval base's point, sized as the air mode's large pictures are: the
level built where the player knows the province at 2 or more, else 1 where there is a naval base at all, drawn at
(level − 1) × 0.2 + 1 times its scale; its second frame where a fleet lies in the port. A bombed base keeps its size.

The icons and the pictures are made again **when the mode is chosen and each day**, so they follow the week's battles a
day at a time while the colours do not.

*Ours:* the point is the province's own, the original's its label's, and the heights are left out, as in the air mode
(`airmap.md`, section 4).

**Tooltip**, with the same list: each of four counts, where above nought, in this order, its line with `NUM` the count
[mod] - `NAVAL_TOOLTIP_RAIDS` and `NAVAL_TOOLTIP_CONVOYS_SUNK` of the convoy raids, `NAVAL_TOOLTIP_RAIDER_ATTACKED` and
`NAVAL_TOOLTIP_RAIDER_DEFENDED` of the rest. Here a battle is ours attacking where P attacked or may operate in the
attacker's ground, else ours defending where P defended or may operate in the defender's, else ours attacking where P
is at war with the defender - not quite the icons' test, which is the original's.

## Not established

- Which theatre a province two countries' theatres hold names in the tooltip: here the first found.
- Whether a dying naval order still colours its provinces: the original colours again before the order is taken apart.
- Which of the original's provinces it does not simulate, which get no resource icon: here every province does.
