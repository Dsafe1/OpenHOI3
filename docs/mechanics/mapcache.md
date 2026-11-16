# Building the map cache

The whole of the original's `map/cache`, made from the map files: so that a map the original has not built a cache for - an
edited one, or a mod's once mods are layered - is drawn as the original draws it and not flat. Read whole in
`reversing/findings/FINDINGS-mapcache.md` (2026-11-08); `lib/OpenHOI3.Content/Map/Cache` builds it. **[exe]** a rule the executable
holds; **[mod: file]** what a file may change; *Ours* where this departs; *measured* where the rule is set by the files and not read.

## 1. What is made, and how it is checked

`quad_index.bin`, `all_quads.bin`, one `quad_textures/quad_texture_<tile>.dds` for each tile with land, `border_adj.bin`,
`border_bitmap.bin`, `map.bin`, `adjacencies.bin`, `navaldist.bin`, `climate.bin`, `provincecache.bin`, `quad_format.bin` and last `time`, in the original's own formats, so that the readers of `mapdraw.md` take them unchanged and the original can use the folder as its own (section 12).

**Every file is the original's byte for byte**, on two maps: Their Finest Hour's shipped cache, and the cache the original built for
BlackICE's map in the player's folder, its beach noise's count given (section 8) - and the original's own rebuild in the Documents folder, of 2026-11-08, whose count was 1,140. `OpenHOI3.SaveCheck mapcache` builds and compares, in
about 2.3 seconds a build. A trial of the client, the same view of Germany drawn from the original's cache and from one built here,
differs no more than two runs of the original's cache do - 80,892 and 77,752 pixels, 241 and 200 of them by more than 8, the water's
animation (2026-11-08).

## 2. Tiles

A tile is 50 pixels square [exe]; there are `width / 50 + 1` columns and `height / 50 + 1` rows, numbered from the south-west along each row.
A province with pixels, sea too, goes in the tile its bounding box's centre falls in - `x0 + (x1 - x0 + 1) / 2` and likewise for y - and each
tile lists its provinces by ascending id (*measured*, on 3,561 tiles). A tile has a record and a picture if and only if a province on its list
is land. Records are written in the order of the tile numbers.

## 3. What everything reads

y counts from the south: a bitmap's stored row r is `height - 1 - r` [exe]. A province is land if `default.map`'s `sea_starts` does not list
it [mod: default.map]; id 0 counts as land, and each rule that must leave it out says so. A province's pixels run from the south row by row, each
row from the west. A province's *first region* is the first in `region.txt`'s order that lists it, or none [mod: region.txt]. Regions are compared
by identity.

## 4. `map.bin`

Twelve bytes `MAP_CACHE`, padded with noughts; the numbers 2 and 3, which a reader compares whole; the count of river sources and each source's x
and y (`mapdraw.md`, section 4); then a raster of `(width + 1) x (height + 1)` bytes, each row from the west, row y at `y * (width + 1)`.

The raster starts at nought. Pixels x from 1 to width − 2 and y from 1 to height − 2, in rows from the south; c, l, r, d, u the province at the
pixel, west, east, south, north, and ld, rd, ul, ur the diagonals. Two passes [exe]:

1. Where d is not nought and c and d are alike in being sea or not: where c ≠ l and l = ld = d, the pixel's **whole byte** becomes 2 and the western
   pixel gains 0x20; then where c ≠ d and d = rd = r, the pixel's low four bits become 8 and the eastern pixel gains 0x10.
2. Where c is land and not nought and c = d: if the pixel's low bits are nought, c ≠ l, l = ul = u, l is land and not nought, c = r, and neither the
   northern nor the western pixel has a bit of 10: the low bits become 4 and the western pixel gains 0x20. Then, again if the low bits are nought, the
   same to the east: c ≠ r, r = ur = u, r land and not nought, c = l, neither the northern nor the eastern pixel a bit of 10: 1, and the eastern
   pixel gains 0x10.

The low bits say which corner of the pixel is cut (*inferred* from the rule): 2 south-west, 8 south-east, 4 north-west, 1 north-east. 0x20 and 0x10 mark
the pixel across the cut. The land mesh reads 0x20; nothing found reads 0x10.

## 5. `border_bitmap.bin`

Four bytes for each place of the same `(width + 1) x (height + 1)`, in the raster's order. By stages, each seeing what the last left, on two masks
of a byte a place that are never written: A, a province border's sides, and B, a region border's (`CBorderBitmapBuilder`; the tables are
`FINDINGS-mapcache.md`, D.3).

- **The columns**, for y from 1 to height − 1: byte 0 of columns 0 and 1 gain 1 and 2; column 0's masks gain the pair of sides, by whether the
  province to the south differs and is alike in being sea, and the last column's byte 0 gains 8 and 4 (0x18 and 0x14 across regions).
- **The sides** of each inside pixel: byte 1 is 0x40, byte 0 nought, and for the east (1), west (2), north (4) and south (8) a side is set where both
  provinces are not nought and differ, are both land or both sea, and that neighbour's raster byte has none of the bits 0xE, 0xB, 0xA, 0xF in turn. A
  side between land provinces of different first regions goes in B with 0x10; every other in A. Two sides of the shapes (1,4), (2,4), (8,1) and
  (8,2) with exactly one of their regions the pixel's become a corner code.
- **The cuts**, in raster order where the pixel's low raster bits are set: byte 3 takes 2, 0x12, 1 or 0x11, each test in its fixed order, and the sides the
  cut takes away go unless the neighbour across has the matching side.
- **The spreads**, in raster order: a pixel with no cut in byte 3 takes the code of the first of its four neighbours' cuts that matches - 4, 5, 6 or 3 -
  with 0x10 where that diagonal is of another region; a cut pixel spreads its sides to the pixels across, unless they already hold a corner value, which the
  pixels after it then see.
- **The end**: pairs of a region value and a side become one code, the two masks are ORed into byte 0, and the shade of the two columns at each side is copied
  in from the third.

Byte 2 is never written; the controllers go there as a game runs (`mapdraw.md`, section 4). Kept as read, the first spread's guard reads the pixel's own mask
where the other three read the neighbour's, and (0x18, 2) makes code 3, the same as (0x11, 4). **Do not tidy them**: both are in the shipped file.

## 6. The land mesh

For each province of the tile's list a group of 16-bit indices, sea provinces' empty [exe]. A province's pixels are taken as maximal runs along a row, one quad a
run. With a run from xs to xe in row y and `m(c)` the raster byte at column c of that row, the left corners begin at xs − 1 and the right at xe, and the first
bit found of 2, 8, 4, 1, 0x20 at `m(xs − 1)` and at `m(xe)` moves one [exe]:

| bit | left end | right end |
| --- | --- | --- |
| 2 | nothing | nothing |
| 8 | south corner xs − 2 | south corner xe − 1 |
| 4 | north corner xs | north corner xe + 1 |
| 1 | north corner xs − 2 | north corner xe − 1 |
| 0x20 | looks at `m(xs)`: 2 south corner xs; 8 nothing; 4 north xs; 1 north xs − 2 | looks at `m(xe + 1)`: 2 south xe + 1; 8 nothing; 4 north xe + 1; 1 north xe − 1 |

The vertices are (left south, y − 1), (right south, y − 1), (left north, y), (right north, y) from the tile's middle, and the triangles n+1, n, n+2 and
n+1, n+2, n+3. After a province's runs its corners are welded, each kept at its first occurrence within that province, never across provinces, and a triangle
with two equal corners goes; a triangle that is merely flat stays. That the mesh's edges leave the pixel grid (`mapdraw.md`, section 2) is these moved corners.

## 7. The land borders and `border_adj.bin`

Every pixel of a land province is an entry, with the shape of its raster bits: 2 → type 4, 8 → 3, 4 → 2, 1 → 1, else 0. A cut pixel has a second entry, the other half of
the diagonal, of type 1 (west, for bit 2), 3 (west, for bit 4), 2 (east, for bit 8) or 4 (east, for bit 1), which names the province beside it in its colour.

An entry is a border where one of its eight neighbours is a land province other than its own and not nought; the neighbour chosen is the last of north, west, east, south
that qualifies, else the first diagonal. It is a junction where, of north-west, south-west, north-east and south-east, a pair of the sides differs from the province and from each other
and neither is sea - the map's edge counts as such a side, as province 0, as in the original. A junction's pixel goes on the province's list for `border_adj.bin` once, in the walk's
order. **The texel it reads** is `province x 14 + slot`: a junction's slot is the count of the province's neighbours plus its place on the list, held at 7 [exe] - which is why the ninth junction of
province 10100 reads the eighth's texel, 45 pixels away - otherwise the chosen neighbour's place among the province's ways out, or −1 for a diagonal one.

The entry is four vertices with a pixel's corners by its type, the colour's bytes `byte 3 x 8, id low, id high, byte 0 x 8`, id the province at the entry's offset. Vertices with both
codes nought go; the rest are welded by all their fields, each keeping its first place; a triangle with two equal corners goes. Written as x and z each with 1000 taken off or added, by the
corner's own 0 or 1, then the texel's two words and the colour.

## 8. The beaches

For each land pixel, in the walk's order, segments by how it lies against the water (`CBeachMesh.PixelKinds`; `FINDINGS-mapcache.md`, C has the fifteen). A kind from 1 to 11 is a straight strip of seven
vertices and fifteen indices, 12 to 15 a corner of eight and eighteen. The seaward edge is moved by the noise: `D(x, y)`, five octaves of Perlin's noise, H 0.3, lacunarity 0.4, its
size folded by halves to at most 0.5 - every step in single precision stored after double arithmetic, so that a beach comes out to the bit [exe]. After the tile's segments, one triangle a segment closes the gap
to the nearest other segment, under a distance of 2 and with near and far points under 1 apart; otherwise it collapses on itself. The colour names the province under the pixel, offset by the pixel's *place in the
pixel list* into a list where a cut pixel has two entries, so that an earlier cut shifts what later pixels read: as read, and it is in the shipped file.

**The noise's tables depend on how many times the C runtime's `rand` had been called on that thread before the first beach of the process**: 292 for Their Finest Hour's shipped cache, 1,140 and 364 for BlackICE's
built at different times, none of which the map files decide; nothing seeds it. *Ours*: **292**, `CBeachNoise.DefaultCallsBefore`, which makes Their Finest Hour's cache whole. Any other map has no right count, and differs from
what the original would build by the wobble of its beaches alone - under half a pixel - which nobody can see.

## 9. The surf

The walk's land pixels' records of `mapdraw.md`, section 4, in the walk's order, each 28 bytes: the water pixel, its kind, and the four offsets every record of that kind has (*measured*, the same in both maps).

## 10. The terrain-index pictures

`(w + 4) x (h + 4)` texels over the tile's land rectangle, the union of its land provinces' boxes, from two west and south of it. Unset is 0xFF. For each land pixel of the tile's land provinces, those of a water terrain
skipped whole [mod: terrain.txt], four values are written, each the palette entry's `priority` times `256 / terrain` [mod: terrain.txt]: the pixel's own (2 [exe] where it has none), then up-left, left and up, each the pixel's own where
that neighbour is water, off the map, has no entry or has no priority, or - for the two to the west - where x − 1 is not above 0. The byte order is own, up-left, left, up. **Each byte written is offered to the eight texels around
where they still read 0xFF**, so a texel off the land keeps the first value offered, in the walk's order; column 0 and row 0 are never offered to. The file is the 128-byte head D3DX writes for one level of A8R8G8B8, then the texels from the south row.

## 11. Which cache is used

`CMapCache.OpenOrBuild`: **the player's folder under Documents and nothing else** - `Documents\Paradox Interactive\Hearts of Iron III\map\cache` - by the maintainer's word of 2026-11-08, "Only use the one from documents" (`docs/compatibility.md`, entry 22). The install's shipped cache is never read by the game. **With a mod the folder is the mod's player folder's**, `...\Hearts of Iron III\<user_dir>\map\cache` (`mods.md`, section 2), which is where the original writes it then. Where the folder's cache is not the map's, one is built into it, over whatever stale files are there; a build takes the main thread about 2.3 seconds.

A cache there is current if its `time` holds the map's stamp, whoever wrote it. The original takes a current `time` as saying every file of the folder is there, and reads `navaldist.bin` and the rest with nothing to fall back on, which is why a build makes the whole folder. The stamp is written last, so that a build cut short is never read.

## 12. The rest of the folder

**These five files are built for compatibility with the original, and nothing here reads them** (the maintainer's word, 2026-11-08). The game works adjacencies, sea distances, climate and the province raster out for itself as it starts; they are written so that the folder is whole and the original can take it for its own. Whether reading them would save time at the start has not been measured.

- **`adjacencies.bin`**: for each province from id 0, a count and that many records of five 32-bit numbers: the kind of way, the neighbour, the province crossed, the distance in thousandths and the bearing (`movement.md`, `weather.md`).
- **`navaldist.bin`**: the upper triangle of the table of distances by sea, its diagonal in it, row by row in sixteen bits (`fleetsandair.md`, section 3). The rows are measured side by side.
- **`climate.bin`**: each province's climate index in 32 bits, from id 0 (`weather.md`, section 1). Province 0 has no point and reads the map's first pixel [exe].
- **`provincecache.bin`**: the province raster as a 16-bit Windows bitmap, a 54-byte head and the rows **from the north**, though a positive height says they run from the south. The head's file size is `width + 3 x height + 110` [exe], a slip the image writer makes in every depth; nothing reads it, and it is in the shipped file.
- **`quad_format.bin`**: the two vertex declarations of `mapdraw.md`, section 2, each element its method, offset, stream, type, usage and usage index in eight bytes, the land's three and the beaches' four - counted one long, so that it carries an element past its end marker - and a last 1 [exe; *measured*, 68 bytes the same in every cache that has one].

**Not read**, and not exercised by either map: a land pixel in column 0 or 1, a `terrain` other than 256, a raster id at or above `max_provinces`, a province id with no pixels in a tile's rectangle.
