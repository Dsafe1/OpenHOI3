# The map: provinces, the raster, adjacency, and the start state

What the original computes when it builds its map, written as rules this project can
implement. No addresses and no offsets: those describe one build of the original's
executable and mean nothing to an engine of our own. Each rule says where it comes from and how sure it is, in the
fact base's own vocabulary - `confirmed`, `likely`, `inferred` - or **measured**, meaning
counted here against the files of a Their Finest Hour install on 2026-10-07.

Sources: `reversing/findings/FINDINGS-mapbuild.md`, `reversing/findings/FINDINGS-history.md`,
`reversing/findings/FINDINGS-camera.md`.

Where this project deliberately does something else, the paragraph is marked **Ours**.

## 1. The files

`map/default.map` names the rest. Paths in it are relative to `map/`.

| key in `default.map` | file | what it holds |
| --- | --- | --- |
| `definitions` | `definition.csv` | one row per province: id, colour, a name nobody reads |
| `provinces` | `provinces.bmp` | the raster: every pixel is one province's colour |
| `adjacencies` | `adjacencies.csv` | connections the raster cannot show: straits, canals, river crossings |
| `positions` | `positions.txt` | hand-placed points per province: unit, text, buildings, city |
| `terrain`, `terrain_definition` | `terrain.bmp`, `terrain.txt` | terrain per pixel, and what each terrain does |
| `rivers`, `climate` | `rivers.bmp`, `climate.bmp` | rivers, and the climate index per pixel |
| `continent`, `region`, `coastlines`, `tree_definition` | text files | groupings and decoration |

`default.map` itself also carries `max_provinces`, `sea_starts`, the pressure zones for the
weather, and four numbers for the 3D border and tree geometry.

All of these layer like any other file: the expansion's copy replaces the base game's.
(measured: `tfh/map` holds its own complete set.)

## 2. What a province is

- **`definition.csv` is semicolon-separated: `id;red;green;blue;name;x`.** The first row is
  a header. (measured)
- **Ids run from 1 with no gaps**, 1 to 14168 in Their Finest Hour, and `max_provinces` is
  one more than the highest id - 14169 - because **id 0 exists and means "no province"**.
  (measured for the files; `confirmed` for id 0 being a real entry that the original gives
  the same per-province storage as any other.)
- **A row with a colour and no id is not a province.** There are 59 at the end of the file.
  (measured) *Ours:* they are skipped without a warning, since the shipped file has them.
- **Colours are unique**: no two rows share one. (measured)
- **The name column is not the name.** The displayed name is the localisation string
  `PROV<id>`. (measured: `PROV1861` is `Berlin` in `province_names.csv`.)
- **A province is sea if its id is in `sea_starts`, and land otherwise.** 3547 ids, all
  between 10500 and 14168. (measured)

## 3. The raster

`provinces.bmp` is an uncompressed 24-bit bitmap, 5616 x 2160, bytes in blue-green-red
order, with the header's height positive.

- **y counts north.** A positive height means the file stores its bottom row first, and the
  original indexes rows from the *end* of the pixel array - so the first row in the file is
  the **northernmost**, and y = 0 is the far south. Opened in an image viewer the picture is
  upside down relative to geography. `confirmed`, two ways: every province's bounding box,
  and the latitudes of six cities. This is the sentence that saves an hour.

      row in file  = (height - 1) - y
      pixel offset = header + (row * width + x) * 3

- **The original turns it into a raster of 16-bit province ids**, one per pixel, and that -
  not the bitmap - is what answers "which province is at this point". `confirmed`
- **A colour in no `definition.csv` row maps to id 0.** `confirmed`: the original's raster
  returns 0 for exactly the pixels a direct reading of the bitmap cannot match. Their Finest
  Hour's bitmap has 16 such colours over 14188 pixels, and 14166 of those pixels are pure
  white. (measured) **The white pixels are the lakes** - `inferred`, from where they fall
  when the map is drawn: Vänern and Vättern, the Finnish lake district, the Great Lakes'
  smaller neighbours. So "no province" is mostly inland water, and it is drawn as water.
  *Ours:* unmatched pixels are reported once, as a count, rather than per pixel.
- **Every province has at least one pixel.** (measured)
- **x wraps.** The map is a cylinder: the original wraps differences in x by the map's width
  when it measures between provinces. `likely` - seen in the bearing formula, not read as an
  instruction.

## 4. Where a province "is"

Each province has a pixel bounding box and one position, and the position is what the
original uses whenever it needs a single point - sampling the climate, deciding day or
night, measuring a bearing.

1. the default is the **centre of the bounding box**: `min + (max - min + 1) / 2` each
   way, so it is rounded **up** where the box is an even number of pixels across;
2. `positions.txt`'s `unit = { x y }` overwrites it, truncated to whole pixels, on any
   province;
3. `positions.txt`'s `text_position = { x y }` overwrites it, truncated, on a **sea zone
   only**;
4. **the last one in file order wins.**

`confirmed`, exhaustively: applying those four in file order reproduces every province of a
running game. Four provinces in five take the default.

**The rounding was wrong here until 2026-10-09**, when the distances between provinces came
to be worked out from this point: with `(min + max) / 2` a third of them agreed with the
original's own cache and the rest were a pixel out, and with the rounding above every one
agrees (`movement.md`, section 2). (measured)

A province has **two more points**, each also starting at the centre:

- **where its name is written**: `text_position`, truncated, on any province - land too; **where the file gives none,
  two map pixels south of the centre**, which the original sets before it reads the file. A land province's terrain is
  taken around it (section 9) - with the centre itself, 92 of the 94 provinces whose terrain was not the running game's
  had no `text_position` (`reversing/findings/FINDINGS-liveread.md`, section 74);
- **where its port is drawn**: `naval_base = { x y }` inside `building_position`. It is the
  land end of the distance between a land province and the sea beside it - and **the pixel
  it falls on decides whether the province has a port at all**, and onto which sea
  (`fleetsandair.md`, section 2).

And **a place for each of its buildings**: every other entry of `building_position`, kept
as the file writes it, which is where that building's picture stands on the map
(`mapunits.md`, section 9).

And **its city**: `city = { x y }`, kept as the file writes it, not cut; **where the file gives none, the centre of the
bounding box as it stands, with none of the name's 2.0** [exe] (`reversing/findings/FINDINGS-citypoint.md`, section 1).
**The one thing in a game that reads it is the capital marker**, which stands there on the province that is its owner's
acting capital (`mapunits.md`, section 9) - no distance, no camera and no tooltip asks it; the original's hidden map
editor nudges it (`FINDINGS-citypoint.md`, section 4). The files hardly use it: Their Finest Hour gives one province a `city`,
Lisbon, (2541.856, 1475.007) against a centre of (2541, 1478); the base game's own file one, London; BlackICE none.
(measured)

*Ours:* the file is read since 2026-10-09, when units came to be drawn at the first of these
(`mapunits.md`). It is six megabytes of which these keys are a small part, so it has a
reader of its own that keeps nothing else; the name's turn and size are read since 2026-11-07 (`mapdraw.md`, section 4),
and the city since 2026-11-14. The original's default
for a port the file does not place was not read; the base game places every one that
matters.

**The default can land outside the province.** A concave shape has a bounding-box centre
that belongs to a neighbour; 57 provinces in the build that was measured sample a
neighbour's pixel or an unmatched one. The original warns only when the position was given
explicitly, so nothing reports these. `confirmed`

## 5. Adjacency

Two provinces are adjacent when:

- **they share an edge in the raster** - two pixels of different provinces side by side or
  one above the other. Diagonals do not count. `confirmed` by count: walking the bitmap for
  4-adjacent pairs accounts for all the original's edges but the explicit ones.
- **or `adjacencies.csv` says so.** `From;To;Type;Through;Comment`, where `Type` is `sea`
  (a strait, crossing the sea province named in `Through`), `canal` or `river`. 170 rows in
  Their Finest Hour: 147 `sea`, 8 `canal`, 14 `river`, and a terminating row of `-1`s.
  (measured)

An edge is directed and each side holds its own, so a pair appears twice. An edge also
carries a kind, a distance and a bearing. The kind, the distance and the order a province
holds its edges in are in `movement.md`, section 2, which is what uses them; the bearing,
which only a weather front reads, is `weather.md`'s section 3 - every one of the base game's cache's.

**A raster edge does exist across the x seam**, between the first and last column: the
original looks east and west of every pixel with x wrapped. `confirmed`, and with the
order: built that way, every province has the neighbours the original's cache gives it.

## 6. The cache, and the bug not to copy

The original derives several files from the map - the id raster, the adjacency graph, the
climate index per province, naval distances - and keeps them in a cache folder under the
user's documents, one per mod.

- **One stamp governs every cache**: the 32-bit sum of a Fletcher-style checksum over seven
  files - `default.map`, `positions.txt`, `terrain.txt`, `provinces.bmp`, `terrain.bmp`,
  `definition.csv`, `adjacencies.csv`. If the stamp on disk differs, everything is rebuilt.
  `confirmed`, and the sum reproduces exactly for two different sets of files.
- **`climate.bmp` and `rivers.bmp` are not in the seven**, although the climate cache is
  derived from `climate.bmp`. Editing it alone changes nothing, silently, for as long as the
  cache survives. `confirmed` for the list; `inferred` that it is a bug.

*Ours:* a cache is stamped from **every file it is derived from, and only those**, so an
edit to any source rebuilds exactly what depends on it. And nothing is cached until it is
measured to be worth it.

**The raster and the adjacency graph are not cached, because they do not need to be.**
Measured on 2026-10-07 against Their Finest Hour: reading `provinces.bmp`, turning its
12 million pixels into ids, measuring every bounding box and finding every adjacency takes
about **120 ms**. The whole start - localisation, countries, map and ten thousand province
history files - is under a second and a half cold and about 650 ms warm, and most of that
is opening the history files. The original's expensive step is the path and naval-distance
calculation, which is where a cache will first earn its place here too.

**Nor are the distances**, which are what the original's `adjacencies.bin` exists to keep.
Since 2026-10-09 every edge is measured on every start - a walk of some tens of pixels for
each of 83,596 edges, reading the terrain and the river bitmaps - and the whole start is
still about 1.3 seconds. (measured)

## 7. History, and the one start

Province, country and leader history files share one grammar: plain `key = value` lines
that describe the state before any dated entry, and blocks of the form
`1938.9.1 = { ... }` holding changes that take effect on a date.

- **The original keeps every dated entry for the whole session and replays them.** Moving
  from one date to a later one applies each entry dated after the old date and **on or
  before the new one**; moving backwards undoes them in reverse. It is a reversible log,
  and it runs at startup and whenever the lobby's start date changes - never during play.
  `confirmed`
- **So the state at a start date is: the undated lines, then every dated block on or before
  that date, in date order.** The comparison includes the date itself.
- **The start date is `start_date` in `common/defines.lua`**, `1936.1.1`. (measured) It is
  also the first of the eight bookmarks, which this project does not read.

*Ours:* there is one start, so the history is **folded once** into a start state and the
log is not kept. Nothing here needs to undo an entry. The entries dated after the start are
not discarded as data - the original fires them as time passes, and stage 2 will need to
know how - but that is the clock's business and not the map's.

Province history lives in `history/provinces/<folder>/<id> - <name>.txt`; **the id is the
number the file name starts with** and the folder and the rest of the name are for people.

| key | meaning | where |
| --- | --- | --- |
| `owner`, `controller` | a country tag | undated and dated |
| `add_core`, `remove_core` | a country tag | undated and dated; `remove_core` dated only |
| `infra`, `industry`, `manpower`, `leadership`, `energy`, `metal`, `rare_materials`, `crude_oil` | numbers | undated |
| `naval_base`, `air_base`, `anti_air`, `land_fort`, `coastal_fort`, `radar_station` | building levels | undated and dated |
| `points` | victory points | undated |
| `terrain`, `strategic_resource` | names | undated |

(measured, over the 4480 province files of the expansion layer.) Sea provinces have no
history file - one of 3547 does. Every land province has one.

**Layering is by relative path.** The expansion replaces 4429 of the base game's province
files at the same folder and name. (measured)

**Not established - how the original handles two files for one province.** It happens: 36
ids in the expansion layer and 43 in the base have two files in one layer, sometimes in
different folders (`asia/7108 - Bojnurd.txt` and `soviet/7108 - NizhAydere.txt`), and one
id has a differently named file in each layer. Whether the original reads both, and in what
order, has not been read. *Ours, until it has:* files are read in order of relative path,
compared without regard to case, and a later file's lines are applied after an earlier
one's - so a scalar like `owner` takes the later value and a repeated `add_core` adds
nothing new. The provinces this happens to are reported, as one line with their count: 52
across the two layers once the expansion's files have replaced the base game's.

## 8. The camera

For reference when the map is given its controls; none of it is a rule the simulation
depends on.

- The original scrolls on the arrow keys, on a middle-button drag - which goes through the
  map projection rather than the screen - and when the mouse is within **six pixels** of a
  screen edge. The result is clamped to the camera's limits. `confirmed`
- There is one scroll speed for all three; the original has no setting for the edge alone.
- The zoom arithmetic has not been read.

## 9. Terrain

**The rule was read out of the executable on 2026-10-09**
(`reversing/findings/FINDINGS-movement.md`, section 8). Until then this section held a
rule from experience of modding the original - *the category that covers the
most of a province's pixels* - marked as not read. The original's is close to that and
narrower: it counts **only a small square around where the province's name is written**.

- `terrain.txt` declares the **categories** - ocean, mountain, forest, woods, marsh, plains,
  urban, hills, jungle, desert, arctic - each with its modifiers, an `is_water` flag and a
  display colour. (measured)
- After the categories it lists one entry per shade of the terrain bitmap: a `type` naming a
  category and a `color` holding a **palette index**. `terrain.bmp` is 8 bits per pixel, the
  same size and the same way up as the province bitmap, and each pixel is one of those
  indices. (measured)
- **A province's terrain, from the map** - `confirmed` as read, and **checked against the running game on 2026-11-13**:
  every one of its 14,168 provinces' terrain, read off the game's province templates, is this rule's, the text position
  being two pixels south of the centre where `positions.txt` gives none (section 4;
  `reversing/findings/FINDINGS-liveread.md`, section 74):

      it starts as the category painted at the province's unit point (section 4);
      on land, the 20 x 20 pixels from ten west and ten south of its text position are
          gone through, and of those that are the province's own the category painted
          most often replaces it;
      of two counted equally often, the one terrain.txt declares first;
      with no pixel counted it stays what the unit point gave;
      land that comes out as a water category takes the first category declared that
          is not water.

  The count is per category, not per palette index. A sea province's terrain is whatever is
  painted at its unit point.
- **Province 1737 is in the executable by number**: its terrain is what is painted at its
  text position, with nothing counted. Kept, so that the base game comes out the same.
- **A `terrain = <category>` line in the province's history file overrides that.**
  (from modding experience)
- A point off the east or the north edge of the bitmap reads as palette index 0. A palette
  index no entry of `terrain.txt` covers would crash the original at a unit point and is
  skipped in the count; *ours:* such a pixel paints nothing, a province with nothing else has
  no terrain, and the number of such pixels is reported.

What the change of rule does to the base game, before any history file is applied: **384 of
its 10,621 land provinces come out differently** - most of them between neighbours such as
mountain and hills (76), plains and forest (72), plains and hills (57) - and two sea
provinces are a land category, being painted so at their point. (measured, 2026-10-09)

**Not established:** what `priority` on an index entry means. Nothing here reads it.

Each category's `movement_cost` is read too. It has nothing to do with the province's own
terrain: it is what a pixel of that terrain adds to a distance (`movement.md`, section 2).

## 10. Whose colour a province is

On the political map a province is **the colour of whoever holds it, and striped with its
owner's where that is another country.** Asked on 2026-10-09 how the
original shows occupied ground: "It uses striping", with a screenshot.

- **The stripes are the original's own picture**, `map/terrain/stripes.dds`: 128 pixels
  square, two diagonal bands to a tile, the bands a little over a quarter of it. (measured)
- **How it is laid on the map** is in the original's terrain shader, which is a text file of
  the install (`gfx/FX/terrain_2_0.fx`): a province has two colours, the picture's alpha says
  how much of the second a point takes, and the picture repeats **every 8 map pixels** both
  ways, measured from the map's south-west corner. (measured) So the stripes are fixed to the
  ground and grow with the zoom.
- **Which colour is the stripes'** was not read. Settled on 2026-10-09: "The
  owner is the thin stripe, the controller the wide one." It was built the other way round
  first, from a guess at the same screenshot that took its heavy border for the edge of
  what a country owns.
- **A border between countries is drawn where the holder changes**, and stripes make none.
  In the screenshot the heavy border and the war's dashed line run along the side of the
  striped province that faces the country of the narrow bands, and a thin line along the
  side facing the country of the wide ones; with that word on which is which, that puts the
  heavy line at the edge of what is held. `inferred` here, and read since: the border meshes' country byte is made from
  the controllers (`mapdraw.md`; `CBorderCorners`).

Since 2026-11-07, as read (`reversing/findings/FINDINGS-mapmodescountries.md`, section 1): both colours are at half
brightness where the player knows the province below 2, land nobody owns is grey 0.6, and **the lobby's map, where a
country is chosen, is the other way round** - the owner's colour the ground and the controller's the stripes, undimmed -
as the original's front end draws it. The other map modes are `mapmodes.md`.

*Ours:* the original lays its colours over the grey of the terrain's own picture and
lightens them; here a province is a flat colour. And with the picture missing nothing is
striped.

## What is not established, in one place

- for **terrain**: what `priority` on an index entry means, and where the original puts a port that `positions.txt`
  does not place. ~~Which terrain about one land province in two hundred takes~~: **settled 2026-11-13** - none of the
  48 had a `text_position`, and the original counts round its default text point, two pixels south of the centre, where
  ours counted round the centre; the name is still drawn about the middle of the box, as screenshots of the original
  have it. With that every province's terrain is the running game's, all 14,168 read off it, and `weather` agrees in
  every province (`reversing/findings/FINDINGS-liveread.md`, section 74);
- ~~the **bearing** between a land province and a sea province~~: the naval base is the land end, its default the
  bounding box's centre (`weather.md`, section 3);
- the order and merging of **two history files for one province**.

Settled on 2026-10-09 and no longer here: the terrain rule (for all but the provinces above), the distance on an edge, and
whether a raster edge crosses the x seam.
