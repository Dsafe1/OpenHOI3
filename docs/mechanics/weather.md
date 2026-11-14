# Weather: fronts, a province's hour, the seasons, and the weather map

Every province has a weather, made again every hour: humidity, pressure, wind, temperature, precipitation, cloud, and
on land mud and frost. **Fronts** born at the map's low pressure zones carry humidity, low pressure, wind and warmth
across the provinces they step into; **high pressure** spreads out of its own zones; each province then makes its cloud
and rain out of its humidity, its frost and mud out of rain and cold, and pulls its temperature toward a target set by
`climate.bmp`, its terrain, the **season** and the night. What weather does to a march, a battle, supply and the air was
built before it (section 5); this is the weather itself. Read out of the original - `reversing/findings/FINDINGS-weathersim.md`,
which consolidates and corrects `FINDINGS-weather.md`, `FINDINGS-weatherfront.md` and `FINDINGS-temperature.md` - and
checked against two of the maintainer's saves an hour apart (section 7). `confirmed` unless marked.

Every figure is in thousandths and every division C's, cut toward nought; a figure marked *fixed* is compiled into the
original, and a define is Their Finest Hour's value in `common/defines.lua`'s `weather` block.

## 1. Where a province's temperature starts, and its season

**The base.** `map/climate.bmp`'s palette **index** - not its colour - under the province's unit point (`positions.txt`'s
`unit`, a sea zone's `text_position`, else its bounding box's centre), and the temperature of its terrain in
`terrain.txt`:

    base = (index - 100) x 0.150 + terrain.temperature                *fixed*: 100 and 0.150

The terrain is the one the map gives the province.

**Latitude** is how far north of the map's middle the province's bounding-box centre is, in thousandths of the way to
the top: `max(y - height/2, 0) x 1000 / (height/2)`. **Everything south of the equator is nought** - the original's own
clamp - and so has no season.

**The season**, 1 to 5, made again at the start of every province's day (so the midnight hour still has the day before's):

- latitude 250 or less: **3**, all year;
- otherwise winter's width `L = (latitude - 250) x 1000 / 750 x 183 / 1000` days, summer's `X = 183 - L`, and with
  `d` = the day of the year (1 January is 0) less `L / 2`:

| d | band | north | south |
| --- | --- | --- | --- |
| 0 or less | F | 5, winter | 3 |
| 1 to 45 | A | 1, late winter | 4 |
| 46 to 91 | B | 2, spring | 4 |
| 92 to 91 + X | C | 3, summer | 5 |
| 92 + X to 136 + X | D | 4, autumn | 1 |
| 137 + X to 182 + X | E | 4, late autumn | 2 |
| over 182 + X | F | 5 | 3 |

"North" is a unit point in the map's northern half. At the top of the map winter is half the year and there is no
summer; just above 250, the reverse.

**Night** is the province's own hour, by how far east it is, outside six to eighteen widened or narrowed by the month and
its latitude (`landcombat.md`, section 4, as built before): made again for each province as its weather is.

## 2. A province's hour

In this order; nothing is held to its range until the end, so each step reads the one before unclamped:

1. **Wind**, with no front over the province: `wind x 750 / 1000` (*fixed*).
2. **Humidity**: `t = max(temperature, 0)`; on land `cap = terrain.humidity x 5`, at sea `cap = 5` and `t += 10`; where
   humidity is 1 or more, `t = t x 1000 / humidity`; then
   `humidity += (1000 - p) x t / 1000 x cap / 1000`, where `p` is where pressure stands between `PRESSUREMIN` 870 and
   `PRESSUREMAX` 1090, 0 to 1000. Deserts' `humidity = -50` dries them out.
3. **Cloud**: the air's wetness for its warmth, `humidity x 1000 / temperature x 10` where it is above 0 and humidity
   otherwise, against `50 - terrain.precipitation`, 30 less on land: at or over it and with any humidity, cloud gains
   **0.100 under a front and 0.200 without**; otherwise it loses 0.050 (*fixed*).
4. **Rain**: `2 x (cloud - bar)` where cloud is over the bar - **0.250 on land under a front, 0 on land without, 0.500 at
   sea** - else nought. **Without a front** it burns off `0.2 x rain` of cloud and `10 x rain` of humidity; under one,
   nothing, which is why a front means days of rain and clear air a shower.
5. **On land, mud and frost**, from last hour's temperature and frost and this hour's rain: infrastructure (the
   province's, as supply has it, 0.010 to 1) - mud gains `(1 - infrastructure) x 0.05` where rain is over 0.500 or the
   ground is frozen at -5 or warmer, else loses `max(infrastructure x 0.005, 0.002)`; above 15 degrees it loses 0.005
   more, and below -5 it is nought. Frost: **below 0 with rain over 0.250, it gains `HOURLYFROZENINCREASE` (0.01) where
   it is below -5, and nothing at all between -5 and 0**; otherwise below -10 and under 0.500 frozen it gains a fifth of
   that, below -2 and under 0.250 a tenth; otherwise above 0 it thaws by `HOURLYTHAW` (0.01).
6. **Temperature** steps toward its target (below): 2 on land and 1 at sea, x10 where the target is over 10 degrees away
   either way, **x5 only where cooling** by over 5, x10 again in season 3 while `TEMPERATURECHANGESPEED` is positive, all
   times that define (0.1): a step of 0.2, 1.0, 2.0 or 20.0 degrees on land an hour. It steps down where it is above the
   target and up otherwise - so it overshoots, and moves up when equal.
7. **Pressure** below `PRESSUREDEFAULT` (1013) rises by `PRESSURESTEP` (5), on land always and at sea only with no front
   over it.
8. **The ranges**: wind 0 to 200, temperature -50 to 50, humidity `MINHUMIDITY` to `MAXHUMIDITY` (0 to 100), mud, frost,
   rain and cloud 0 to 1, pressure 870 to 1090.

**The target**: the base, then by season - **3** +5; **2 and 4** +1; **1** -1, -1 more over latitude 950 and -1 more
over 650; **5** -4, -2 more over 950, -4 more over 650, and where that is under -3 and the ground wholly frozen a wind
chill of `0.125 x wind + 5` - and night's -2 (**-5 in winter**); then where it is over 10 or at sea, less
`cloud x CLOUDCOVERAGETEMPERATUREDROP` (8); then where it is under 3 and anything falls, 4 less. The latitudes and the
0.125 are *fixed*.

**The picture** the weather shows - snow, or a blizzard in a wind over `GFX_SNOW_STORM_LIMIT`, where rain is over
`GFX_SNOW_LIMIT` and it is under -5; else rain, or a storm over `GFX_STORM_LIMIT`, where rain is over `GFX_RAIN_LIMIT`;
else overcast over `GFX_CLOUD_LIMIT`, partly cloudy over `GFX_PARTIAL_CLOUD_LIMIT`, or clear - is chosen after the hour.

## 3. Fronts and high pressure

**The hour's own weather**, before anything else in it, in this order:

1. every low pressure zone's cooldown counts down an hour, with no floor;
2. **each of `default.map`'s `high_pressure_zones`**, in its order, sets its province to `PRESSUREMAX` and walks out in
   the order provinces are reached, each step costing the next province's size (1 to 4, as supply counts it): a province
   whose pressure is under `PRESSUREMAX - cost x PRESSUREREDUCTION` (2) is raised by `PRESSUREPROPAGATION` (10) - one step,
   which may pass that - and the walk goes on through it while the figure is above nought; one at or over it stops the
   walk there;
3. **fronts are born**, as many as the cap allows - `low_pressure_zones` x `MAXFROMEACHPRESSURE` (8) - one at each zone
   that may bear one now, in the file's order (below);
4. every province is cleared of its front;
5. **each front ticks**, the oldest first; one that dies frees its zone's cooldown and the provinces it held.

**A zone may bear a front** where its province's season is in its mask (`low_pressure_zones`' number: a bit for each
season by its number, so `31` misses season 5, winter), no front is over it as the hour before left the markers, its
cooldown has run out, and fewer than 8 of its fronts live. Its **heading** is drawn (0 to 999) the first time and then
turned on by `1000000 / MAXFROMEACHPRESSURE` - an eighth of the compass - at each front; its cooldown is drawn,
**24 to 71 hours**; and the front is born at the zone with its home's pressure dropped by `LOWPRESSUREBASE` and a drawn
`0 to LOWPRESSUREOFFSET - 1`, 100 to 199 hPa, curving at `-10` in the north and `+10` in the south (*fixed*). The draws, on
the game's generator, are heading, cooldown, depth, a front at a time - the first of the hour.

**A front's hour.** Between moves - every `WEATHERMOVEMENTDELAY` (2) hours - it only marks its provinces as its own. On a
move its arc's half-width is 200 thousandths of a turn, 350 while it covers under 3 provinces, halved over 30 and again
over 100 (*fixed*); its direction turns by its curve and the curve keeps four fifths of itself. It dies with no
province, or under 4 provinces more than 1000 pixels from home. Otherwise it leaves everything it stood on and, from
each province it stood on still under `PRESSUREDEFAULT`, takes the neighbours - in the edges' order - with no front,
not over `PRESSURETHRESHOLD` (300) more pressure, and **whose bearing's arc holds its direction** (over 100 provinces,
one a province), handing each province's weather to them:

    rain = humidity x 0.98;  the province keeps humidity - rain / 5, once
    each taken:  humidity + rain;  wind + 0.97 of the difference, or where its pressure was higher, the more of that and
                 0.25 of the pressure's difference; pressure pulled 0.98 of the way down; temperature + 0.95 of the
                 difference, at most 10 either way

The arc test is the original's: the window is `bearing +/- arc`, each end taken modulo 1000 with C's sign, wrapping only
where the low end comes out over the high - so a bearing of 1000 wraps and one of 0 does not; the direction is never
brought back into 0 to 1000.

**An edge's bearing**, worked out with its distance (`movement.md`, section 2) from the same two points - between land
and sea the land end's naval base: thousandths of a turn from due west, north 250, east 500, south 750, due west
**1000**, in single precision: `floor((atan2(y1 - y2, x2 - x1) / 2pi + 0.5 + 0.0005) x 1000)`, never wrapped round the
map. It is every bearing in the base game's own `map/cache/adjacencies.bin`, 83,596 of 83,596.

## 4. When

**In the hour**: the fronts and high pressure first (section 3), before the units move; every province's weather after
the countries' hour and before the battles - so a march and a country's hour see this hour's fronts and last hour's
rain, mud and frost, and a battle the hour whole. **In the day**: each province's season, first in the province's day.

**A new game**, before its first day: every province's weather cleared, its pressure `PRESSUREDEFAULT`; its season and
night for the hour the game starts; its temperature set to its target; then **`INITIAL_SIMULATION_HOURS_AHEAD` (96)
hours of fronts and every province's weather with the clock, the night and the season standing still** and no picture
chosen. With the first days' hour that is 97 hours before the first played.

**A save** keeps each province's eight figures - a figure left out where nought, pressure where `PRESSUREDEFAULT` - and the
fronts in their order, and nothing of the zones' cooldowns and headings. **A load** [exe] keeps the save's weather and
fronts, and entering the loaded game makes each province's season and night again for the saved hour - no temperature
seed, no ninety-six hours, no first days. No picture is chosen and no front marked on a province until the first hour,
and the zones' cooldowns and headings start at nought - in the original only in a fresh process, a process that has run
a game keeping that game's - so a front over its home holds no zone back in that hour and any zone may spawn one (`reversing/findings/FINDINGS-weathersim.md`, A.6; `savegame.md`, section 20; `docs/enhanced.md`).

## 5. What weather does

Built before the weather itself, each in its own spec: a land unit's speed (`movement.md`, section 5) and the supply
lost in mud (`supply.md`); the weather term of a land battle, of a sea battle and its breaking off, of the battle in the air
and of bombing (`landcombat.md`, `navalcombat.md`, `aircombat.md`, `bombing.md`); a ship's firing range and the sighting
at sea; and the cloud an air unit may not fly into (`fleetsandair.md`). **Attrition is not built.**

## 6. On screen

**The weather map mode**, the button `mapmode_3` (`minimap.md`, section 4; "Weather Mapmode"): land is its terrain's
colour, **greyed and lightened by how frozen it is** and **at half brightness where the player's intelligence of it is
under 2**; land nobody owns is greyed and dimmed as six tenths of each; the sea is the terrain mode's. Its colours are
made when the mode is chosen and again when a province changes hands - **not each hour**, as the original's.

**Its pictures**, only in that mode: over each province whose weather shows anything, a plain icon - the
`weatherBillboardType` of `interface/mapitems.gfx` named for its picture with `_simple` after it:
`weather_partialcloud_simple`, `weather_cloud_simple`, `weather_rain_simple`, `weather_thunderstorm_simple`,
`weather_snow_simple`, `weather_snow_storm_simple`, one `textureFile` each. A picture is twice `scale` (2.5) high and as
wide as its texture's shape, its middle 4 map units over the province's point - 4.3 over a sea zone's, whose anchor
stands 0.3 off the water - up off the map drawn as north. **Since 2026-11-07 they are drawn through the install's own
`weather_billboard.fx`**, translated for the canvas, by the technique the type's `effect` names, fed as the original
feeds a picture's cloud: `vTime`, `vXOffset` nought, `vFlip` one - so a plain icon is a still sample of its texture.
The file is the one the first type naming that `effect` gives, as the original keeps the effect under that name
(`reversing/findings/FINDINGS-weathersim.md`, C.3.1). *Ours:* the pictures of one kind are drawn together, where the
original draws them in the order its scene reaches them; and they are drawn over the counters, which the reading leaves
open. They
follow the hour, and draw at every zoom - dots from far off. **Measured** on the maintainer's screenshots of 2026-10-15:
at 33 screen pixels to a map pixel, the square cloud over the Breton Coast is 162 pixels wide - 4.9 map pixels against
the 5 a map unit to a pixel gives - and its middle 133 pixels, 4.0 map pixels, north of where the sea zone's name is
written.

**Only the plain icons**: the original draws them where its `alwaysCounters` setting is on, and with it off draws the
same names without `_simple` - a cloud with rain or snow falling under it, and lightning in a storm. **`alwaysCounters`
off is not supported** (`docs/compatibility.md`, entry 12; `settings.md`), by the maintainer's word on 2026-10-15, so
those are never drawn. They were built that day and taken out the same day; the original's `weather_billboard.fx` moves
the rain at uv + (0.000025t, -0.00005t) for its colour alone and the snow at uv + (sin(-0.00001t x 3 x 3.14) / 100,
-0.00001t), and the lower picture hangs `2 x scale - 0.3` under the cloud (`reversing/findings/FINDINGS-weathersim.md`,
section C.3). **t is not milliseconds**, as this said until 2026-11-07: each frame adds the seconds since the game was
launched, modulo 1000, so the rain and the lightning run with the frame rate and speed up for 16.7 minutes before
falling back to nothing (C.3.1). **Should the full art ever come back, that clock is built as read** - the
maintainer's word, 2026-11-07: "Keep it as read for now."

**Its tooltip** takes the place of the province's name and owner:

    Temperature: -1.1C            TEMPERATURE, 1 place, "C"
    Pressure: 1013.0mb            PRESSURE, 1 place, "mb"
    Wind speed: 12.3KPH           WINDSPEED, 1 place, KPH
    Humidity: 55.55%              HUMIDITY, 2 places
    Overcast: 56.70%              OVERCAST, cloud x 100
    Precipitation: 23.40%         PRECIPITATION, rain x 100
    Mud: 10.00%                   MUDDY
    Frozen: 0.00%                 FROZEN

then, under a front, `WEATHER_FRONT` "This low pressure zone is moving §Y$DIR$§W." with its heading as a compass point
(`DIR_WEST` to 63, `DIR_NORTHWEST` to 188, ... `DIR_SOUTHWEST` to 938, west again over it), and **straight after it with
no break** the picture's word (`WEATHER_RAIN` "Rain", `WEATHER_CLEAR_SKY` "Clear Sky", ...); then the tooltip's usual
tail.

**The province view's weather strip** (`provinceview.md`, section 3) now shows the province's own weather.

## 7. Against the saves

`OpenHOI3.SaveCheck weather <install> <earlier save> <later save>` gives every province the earlier save's weather and
the game its fronts, runs an hour of weather and sets it beside the later save, leaving out a front born that hour (its
draws and the zones' cooldowns are no save's); a land province that disagrees is tried with every terrain in turn.
*Measured* on 2026-10-15:

| saves | fronts' provinces, in order | every province's eight figures |
| --- | --- | --- |
| 8 January 02:00 - 03:00 | 29 of 29 | all but 48 land provinces |
| 8 January 06:00 - 07:00 | 27 of 27 | all but 49 land provinces |

**Each of those land provinces agrees in everything given another terrain** - Phongsali as jungle and not mountain, Al
Hufrah as desert and not hills: the map's choice of a province's terrain, which `map.md` lists as not established, and
which this check now measures. **Settled 2026-11-13**: none of them has a `text_position`, and the original counts a
province's terrain round its default text point, two pixels south of the centre (`map.md`, section 9); with it both
pairs of saves agree in every province's eight figures (`reversing/findings/FINDINGS-liveread.md`, section 74). `OpenHOI3.SaveCheck weather <install> <start-date save>` is a look: a new game's weather
spread over the provinces beside the start save's - 20 fronts to its 21, and every figure's spread alike.

## What looks wrong and is built so

1. Frozen ground between -5 and 0 under rain neither freezes nor thaws.
2. The temperature overshoots its target, moves up when equal to it, and only cooling gets the x5.
3. Season 5's x2 is dead while `TEMPERATURECHANGESPEED` is positive.
4. The south has no season: latitude nought everywhere south of the equator.
5. A province reads its season from the day before in the midnight hour.
6. `31` in `low_pressure_zones` misses winter; `default.map`'s own comment on the bits is a season off.
7. The cooldowns count down without a floor, and a dying front frees the cooldown of its zone's place in the whole list
   where a new front's slot counts only the zones its climate lets through.
8. Markers are cleared before any front ticks, so an earlier front can take a later front's provinces.
9. A bearing of 0 and one of 1000 behave differently, and an edge across the map's seam points the long way round.
10. The weather map's colours lag the weather; its pictures do not.
11. The tooltip drops the province's name, and the word follows the front's line with no break.

## Ours

- **The weather defines are read as they are needed.** The original copies them once, at the first weather hour a
  process runs, so a mod's `PRESSUREDEFAULT` meets the built-in 1013 in the first game of a process.
- **A new game starts every zone's cooldown and heading at nought.** The original's carry over from a game played
  before in the same process.
- **Only the `_simple` pictures** - a decision, not a guess: `docs/compatibility.md`, entry 12.
- **Up off the map is drawn as north** (`mapunits.md`); measured against the original for the simple cloud (section 6).
- **The tooltip's labels are plain**; the original writes them in the settings' text colours, not read.
- **The terrain mode is not dimmed** where the player knows little, though the original's is, as the weather mode is.
- The province of id 0 has no weather.

## Not built

- **Attrition**, which reads the temperature and the wind.
- ~~**Loading a save**, and with it what a load does to the weather.~~ Built (section 4).

## Not established

- **A province's terrain**, for about one land province in two hundred: the weather check names the terrain the
  original holds for each (section 7).
- ~~Whether the pictures draw at every zoom, and how big they are; the tooltip without the province's name.~~ Settled by
  the maintainer's screenshots of 2026-10-15 (section 6): every zoom, the size read, and no name.
- **Half brightness where the player knows little**: read and not seen - the maintainer's game ran with `debug fow`,
  which gives every province 9.
- **The texture's green** the original's map gives a province, nought here.
- **The seed of the generator during the 96 hours**: a new game's weather cannot be the original's roll for roll.
