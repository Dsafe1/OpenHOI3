# The map: provinces, the raster, adjacency, and the start state

What the original computes when it builds its map, written as rules this project can
implement. No addresses and no offsets: those describe one build of an executable this is
not a reimplementation of. Each rule says where it comes from and how sure it is, in the
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
| `positions` | `positions.txt` | hand-placed points per province: unit, text, city |
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

1. the default is the **centre of the bounding box**;
2. `positions.txt`'s `unit = { x y }` overwrites it, truncated to whole pixels, on any
   province;
3. `positions.txt`'s `text_position = { x y }` overwrites it, truncated, on a **sea zone
   only**;
4. **the last one in file order wins.**

`confirmed`, exhaustively: applying those four in file order reproduces every province of a
running game. Four provinces in five take the default.

*Ours:* the file is read since 2026-10-09, when units came to be drawn at this point
(`mapunits.md`). It is six megabytes of which `unit` and `text_position` are a small part,
so it has a reader of its own that keeps nothing else; the rest of it - where a building,
a city and a province's name are drawn - is not read.

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
carries a distance and a bearing. The bearing's formula is known for same-kind pairs and is
in the findings; the distance is **not established** and is not geometric - every value is
a multiple of 50 and the ratio to pixel distance is not constant. Nothing in stage 1 needs
either.

**Not established:** whether a raster edge exists across the x seam, between the first and
last column. *Ours:* it does, because the map is a cylinder everywhere else.

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

**This section's rule is the maintainer's, from experience of modding the original, given on
2026-10-07. It has not been read out of the executable**, and it is recorded as that rather
than as `confirmed`.

- `terrain.txt` declares the **categories** - ocean, mountain, forest, woods, marsh, plains,
  urban, hills, jungle, desert, arctic - each with its modifiers, an `is_water` flag and a
  display colour. (measured)
- After the categories it lists one entry per shade of the terrain bitmap: a `type` naming a
  category and a `color` holding a **palette index**. `terrain.bmp` is 8 bits per pixel, the
  same size and the same way up as the province bitmap, and each pixel is one of those
  indices. (measured)
- **A province's terrain is the category that covers the most of its pixels.** (maintainer)
- **A `terrain = <category>` line in the province's history file overrides that.**
  (maintainer)

What the install shows when the rule is applied, which is corroboration and not proof:
every one of the 3547 sea provinces comes out as ocean and no land province does; every
pixel's index has a terrain; Berlin, Paris and Moscow are urban. And the override does real
work - 201 land provinces end up different from what the bitmap alone gives them. The
clearest case is arctic: the bitmap makes 5 provinces arctic and the history files make it
142, so the bitmap barely paints arctic and the files are how the north gets it. (measured)

**Not established**, and decided here until it is:

- *whether the count is per category or per palette index.* Several indices share a
  category. *Ours:* per category.
- *what breaks a tie.* *Ours:* the category declared first in `terrain.txt`.
- *what `priority` on an index entry means.* Nothing here reads it.

## What is not established, in one place

- for **terrain**: whether the majority is counted per category or per palette index, and
  what breaks a tie. The rule itself is the maintainer's and has not been read out of the
  executable;
- the **distance** on an edge;
- the **bearing** between a land province and a sea province;
- whether a raster edge crosses the **x seam**;
- the order and merging of **two history files for one province**.
