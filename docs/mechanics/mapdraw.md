# Drawing the map through the install's own effect files

The map's surface - its land, beaches, sea, borders - drawn as the original draws it: the install's own meshes, through
the install's own `gfx/FX` effect files translated to Godot shaders (`shaders.md`), each fed what the executable feeds it,
so that a mod's terrain or water shader - BlackICE changes both - is the one seen. By the decision of 2026-11-06,
moddable shaders are to be supported, and that before the far view. Read in
`reversing/findings/FINDINGS-mapdraw.md` (A the land and camera, B the sea, borders and generated textures, C the scene)
and `FINDINGS-farmap.md` (A the far view's thresholds). **[exe]** a rule the executable holds; **[mod: file]** what a file
may change; *Ours* where this departs.

## 1. What is drawn, in order

The original's scene, `FINDINGS-mapdraw.md` C.1: the land; the beaches; the rivers; the land borders under `border_cutoff`;
the sea; the surf where `waves`; the water borders; the trees under height 100; the country names in the far view; the
province names under 100. **Built so far: the land, the beaches, the rivers, the land borders, the sea, the surf, the sea zones' borders, the trees, the highlight, the country names and the province names, and over them the air map's rings and the hierarchy's lines** (section 6). The overlays this project draws
in 2D - counters, buildings, arrows, the hierarchy, the selection, the weather and the air map's layers - stay as they are,
over the map.

## 2. The meshes (`Content/Map/CQuadCache`)

The original does not make the map's meshes as it starts: it reads them from a cache the install ships,
`map/cache/quad_index.bin` and `map/cache/all_quads.bin` [mod: the map's cache], and so does this. Tiles of 50 map pixels a
side [exe], columns = the map's width / 50 + 1; each record the land's vertices (a tile-local pixel corner and a province
id), its triangles in one group for each province, the land borders' vertices and triangles, the beaches' vertices (floats
and a colour carrying the province id) and triangles, and the surf's records (`CQuadTile`). A record that does not read to
its stated size is reported and left out. `OpenHOI3.SaveCheck quads` reads it whole: on 2026-11-06 TFH's 1,774 tiles, 565,507
land vertices, 443,825 land triangles over 10,621 provinces, 572,496 beach triangles in 941 tiles, tile-local coordinates
−106 to 108, no triangle mixing provinces - the fact base's figures, every one. **A map changed without its cache** - a mod
shipping a new provinces.bmp and no cache - the original makes the cache again (`CProvinceManager_BuildQuads`, not read);
*not built*: such a map has no surface drawn and says so.

### 2.1 Where the cache is found, and when it is the map's (`Content/Map/CMapCache`)

Read in `reversing/findings/FINDINGS-mapbuild.md`. **Where.** The original resolves a file of `map/cache` in the player's
folder under the Documents folder first - `Paradox Interactive\Hearts of Iron III`, or for a mod the folder its `.mod` file
names as `user_dir` - and then through the content's layers [exe]. It writes every cache it builds there, so a mod's whole
cache is in its own user folder and never in the install: on this machine BlackICE's sit under `Documents\...\Hearts of Iron
III\BlackICE GitHub\map\cache` and the like, while TFH's ships in `tfh/map/cache` and its user folder holds only `map.bin`
and `time`. Each file is looked for so here, one at a time, the user folder's copy first.

**When.** `map/cache/time` holds four bytes, the stamp of the map files the cache was built from: the Fletcher-32 of
`map/default.map` and of the six files it names as `positions`, `terrain_definition`, `provinces`, `terrain`, `definitions`
and `adjacencies`, each through the layers, summed in 32 bits [exe]. The checksum's two sums start at nought and each is
brought back by subtracting 65535 once whenever it passes 65535 - not a remainder, so a sum that lands on 65535 stays there.
A file not there counts nought. `climate.bmp`, `rivers.bmp`, `continent.txt`, `region.txt`, `coastlines.txt` and
`trees.txt` are not in it [exe], so a mod that changes only those keeps its cache - and the original's `climate.bin` goes
stale unseen. **A cache whose `time` is missing or holds another stamp is stale**: the original builds a new one; here
nothing of it is read - no meshes, no border corners. **Since 2026-11-08 the game asks the player's folder alone and builds the cache there where it is not the map's** (`mapcache.md`, section 11; `docs/compatibility.md`, entry 22), the map drawn
flat only where it has no terrain to build from; the savegame tool's `quads` still looks in both, as the original does. *Measured* on
2026-11-06: TFH's map files make `93E125C4`, as `tfh/map/cache/time` and the user folder's `time` hold, and BlackICE
GitHub's make `383756A4`, as its user folder's does. Working the stamp out takes about 140 ms, as the session's map is set
up.

*Not built*: a mod's `user_dir`, since no mod is played yet - the base game's user folder is the one asked. With a mod
layered, its user folder is the one to pass.

## 3. Space and the camera

The original's world: x the map's pixel column, z its row counted from the south, y up; a pixel's middle on whole
coordinates; the land at y 0.35, the sea at −0.32 [mod: the effect files]. This project's 2D map has north at the top, a
pixel spanning [x, x+1] and display rows counting down, so **a 2D point (X, Y) is the world's (X − 0.5, H − 0.5 − Y)**, H the
map's height in pixels.

**The points of `positions.txt` are world points.** Every picture the original hangs on a province - a building's, the
capital marker, the large air and naval base pictures, a ring of range, a resource icon - is hung on the province's anchor,
which stands at the label point, after a translation by its point less the label point [exe]
(`reversing/findings/FINDINGS-citypoint.md`, section 3; `FINDINGS-seaair.md`, section 6; `FINDINGS-airmap.md`, section 8):
so it stands at the file's x and y exactly, a pixel's middle on whole figures, and a building the file does not place at
the label point (`mapunits.md`, section 9). The 2D map draws every one of them half a pixel east and south of its figures,
by the rule above (`Interface/Map/CWorldPoint`); the 3D surface takes them as they are - a ring's `WorldMatrix`. *Corrected
2026-11-15*: until then a building the file placed was drawn at its figures as they are, half a pixel west and south of the
original's, while one it did not place, the capital marker and the counters carried the half pixel.

The camera is the original's: field of view 0.785, near 8 [exe], left-handed (`D3DXMatrixLookAtLH`,
`D3DXMatrixPerspectiveFovLH`), hanging at (0, h, 0) over the origin with the map moved to it - each tile's `WorldMatrix` its
middle less the camera's (x, z), `AbsoluteWorldMatrix` its middle [exe]. Its height is what makes the 2D view's zoom: from
height h it sees 2 h tan(0.785 / 2) = 0.828 h map units from the top of the screen to the bottom, so **h = (the screen's
height / the zoom) / 0.828**, and the 3D surface lies under the 2D layers pixel for pixel. *Ours*:

- **The view looks straight down with north up** - `LookAtLH((0, h, 0), (0, 0, 0), (0, 0, 1))`. The original's own aim -
  (0, −8, 0) straight below with counters only, as this project always is (`CLAUDE.md`, the settled decisions) - and the
  scene camera the effects are handed were not found to meet (`FINDINGS-mapdraw.md`, A.6); this is the view that agrees
  with the original's on screen.
- **The far plane is 1200 or the camera's height and a hundred**, whichever is greater: the 2D view zooms out to the whole
  map, past the original's highest 1000.
- **The camera's place is in the view, not in each tile's world matrix**: the view looks down from (x, h, z) and a tile's
  `WorldMatrix` is its own middle, moved a map's width where the copy east or west is the nearer. `WorldMatrix × ViewMatrix`
  is the original's product to the float, but the view and the projection are then one matrix for every tile - Godot's
  global uniforms, `shaders.md` section 3.11 - and a tile's own matrix changes only where it crosses the seam. Moving the
  camera set some 8,000 parameters and took 9 ms; it sets two and takes a tenth of one (`CMapSurfaceCamera.ViewAt`,
  `WorldAt`). The sea keeps the original's camera-relative form, its one material taking it as water.fx asks.
- Matrices are handed as the effects take them - D3DX row-major, `mul(row vector, M)` - each HLSL row a Godot column
  (`shaders.md`, section 3.3); and Direct3D's clip depth, nought near, is turned to Godot's, one near (section 5).

## 4. What each effect is fed

As `FINDINGS-mapdraw.md` A.2 and B.1 have it, by parameter name:

- **The land and beaches, terrain_2_0.fx**: WorldMatrix and AbsoluteWorldMatrix for each tile, ViewMatrix,
  ProjectionMatrix; ColorMapWidth/Height colormap.dds's size; ColorMapTextureWidth/Height *Ours* the same - the original
  pads the picture to 2048 by 1024 and passes the powers of two, which comes to the same texel; MapWidth/Height the map's;
  BorderWidth/Height 50; TerrainIndexOffset the tile's land rectangle's corner less 2 and TerrainIndexSize its size and 4,
  the tile's `quad_textures/quad_texture_<tile>.dds` its tex1. tex0 base.dds; tex2 and tex3 the two province colour
  lookups (5.1); tex4 colormap.dds in the Terrain and Weather modes, colormap_political.dds in any other; tex5
  noise_test2.dds near, map_overlay_tile.dds far; tex6 texturesheet.tga near, texturesheet2.tga far; tex7 stripes.dds. The
  technique: **map style 0 (Terrain, Weather) Graphical at every height; otherwise General near and General_Low far, far
  being a height above 500** [mod: default.map `terrain_sheet_heights`]; the beaches' Beach* the same.
- **The sea, water.fx**: three quads of the map's size, the map and its copies east and west (x, z, u, v), CameraPosition
  the camera's (x, 0, z, 0), ViewProjectionMatrix, MinMaxUV *Ours* (0.5/w, 0.5/h, (w−0.5)/w, (h−0.5)/h) for the texture at
  its own size; Time a sawtooth from −100 to 0 over 100 seconds; ColorTex colormap_water.dds, OverlayTex map_overlay_tile.dds,
  NormalTex sea_normal.dds, FOWTex the fog (5.2), color0/color1 the sea's colour rasters (5.2) and stripes.dds in the
  coloured techniques. The technique: `simpleWater` WaterSimple at every height, else WaterFar far and WaterNear near, each
  `…Color` in map styles 16, 18 and 19 (the Air and Naval modes).
- **The land borders, TerrainShader_Border** (`FINDINGS-mapdraw.md`, B.3): each tile's border mesh from the cache - one-
  pixel quads whose x and z carry a corner flag of ±1000 the effect takes off, their u and v a texel of the lookup, their
  colour's blue and alpha the pattern's columns - its WorldMatrix, the shared view and projection, tex2 `borders.tga`
  under the first of `border_heights` (150), `borders2.tga` to the second (500), `borders3.tga` above [mod: default.map,
  map/terrain], and drawn only under `border_cutoff`, 500, so never in the far view [mod: default.map]; tex4 **the border
  lookup**, 512 by 512, fourteen texels a province from 14 × id: one for each of its edges in the map's order, blue full
  and alpha a quarter where the neighbour's controller is another than the province's - one band down the picture, from a
  province's line to a country's [exe]. `OpenHOI3.SaveCheck quads` checks the cache against the map: on 2026-11-06 the
  lookup texel's province lies within a pixel of its quad on 2,790,070 of 2,790,564 vertices with one, and the slot's
  neighbour, by the map's order of edges, within three pixels on all but 456 - the order is ours and the cache's. **The
  texels after the edges are the province's junction pixels** (`FINDINGS-mapdraw.md`, B.7) - where three or four provinces
  meet, 151,408 vertices of 2,941,972 - each from **the corner cache** (`Content/Map/CBorderCache`): the pixels
  `border_adj.bin` lists for each province and their four neighbours, with the four bytes `border_bitmap.bin` holds at each
  and `map.bin`'s raster byte [mod: map/cache]; texel A the province pattern's column × 8, R the country pattern's, G the
  diagonal's, B nought [exe]. The country byte, nought in the file, is made from the controllers by three passes over the
  junction pixels, by rows from the south (`Interface/Map/CBorderCorners`): a bit for each neighbour held by another where
  the map cache flags no cut that way, the diagonal cuts, and the bits spread to the neighbours - their state carried from
  one change of hands to the next, as the original's is, so the passes run only when a province has changed hands [exe].
  **No cap at fourteen** [exe]: a province with more edges and junctions runs into the next one's texels and the next
  overwrites them - 162 of TFH's. Without the three files the junctions are not drawn and it is reported. *Ours*: the
  lookup made again at most four times a second of the wall clock, a change of hands marking it - the passes and the
  lookup take about 9 and 10 ms in the optimised build, and at the top speed of a war hands change most hours; the corner
  cache read as a map view is made, about 0.1 to 0.2 s.
- **The sea zones' borders, borders_2_0.fx's WaterBorderShader** (`FINDINGS-mapdraw.md`, B.8): a one-pixel quad on every
  water pixel - terrain.bmp's category water [mod: map/terrain.bmp, terrain.txt] - from x 2 to the width less one and y 2
  to the height less one whose `border_bitmap.bin` byte 0 or byte 3 is not nought, its corners (x − 1, y − 1), (x − 1, y),
  (x, y), (x, y − 1) with uv (0,1), (0,0), (1,0), (1,1) and the two bytes, which pick a 64-texel column each of
  `water_borders.dds` and `water_diag_borders.dds` [mod: map/terrain] - a code of 16 or more drawing nothing; gathered by
  tile of 50 pixels, x outer and y inner, as the corner cache is read (`CBorderCache.WaterBorders`), built once and never
  again [exe]. tex1 and tex3 those two pictures; the depth test off, depth written, blended by source alpha; drawn after
  the sea and at every height - what hides them in the far view is their art, at most a fifth of black over a fifth of a
  pixel (*inferred* there; a screenshot of the original's far view shows none). On TFH 668,662 quads in
  3,638 tiles: the borders between sea zones, and never the coast, which is the surf's. *Ours*: a vertex holds the map's
  own coordinates where the original's holds the tile's, the product with the world-view-projection the same, so three
  materials - a tile drawn a map's width west, where it is, a width east - serve every tile and a move of the camera sets
  three matrices.
- **The highlight** (B.9): a selected land province's texels in both lookups lifted - in style 0 the green 25 and nought
  for any other province, in any other style red, green and blue +25 (255 from 230 up) and alpha full - so the land shader
  shows it +0.098 in the terrain modes and about +0.044 in the others, and the sea never [exe]. Which provinces is the
  selection; the lookups are written again whenever it changes (`CMapSurface.SetSelection`); nothing marks the province
  under the mouse.
- **The rivers, river.fx's RiverShader** (`FINDINGS-mapdraw.md`, C.6; `Interface/Map/CRiverTracer`, `CRiverMeshes`): made
  once as the map is built and never again [exe], from `map/rivers.bmp` [mod], 8 bits a pixel - index 0 a source, 1 a
  junction, 2 to 11 a river of width index − 2, 12 to 22 the same with a mouth of frame index − 12, anything else nothing.
  **The trace**: each source in turn, by rows from the south and then from the west; from it the walk tries east, west,
  north and south in that order, never straight back, and steps onto the first river pixel - dropping the last node where a
  step north or south follows one east or west, unless the new pixel stops the river - and stops on a pixel the terrain
  paints as water [mod: terrain.txt `is_water`], keeping it. With no step left it takes each junction about it not yet
  visited, east, west, north, south, with the pixel beyond it, at the last node's width - one found west moving x and one
  found north moving y for the tests after it [exe] - and stops. One list of visited pixels serves every source, and
  nothing but a junction is tested against it. Then a river with a mouth drops its last two nodes, a river that did not end
  at a junction gains one more node a pixel on in its last direction, and a river of one node is none. **The strip**: a
  cardinal spline of tension 0.7 through the nodes' middles, a point before the first (2·P0 − P1, width 1) and one past the
  last carried on from the segment before; three steps a segment, at t = 0, ½, 1, each a pair of vertices across the
  spline's direction to the next step, the half-width (0.1·W + 0.15 + 0.1·r)/2 with r a draw of a generator of the
  original's kind seeded 42, one draw a step in the rivers' order [exe]; the height 0.40, 0.02 more where the side's or the
  middle's pixel one back and one south is not a sea province [exe]; `river_body.tga`'s u 1 on the north-or-left side and
  0 on the other, its v k/8 on the first segment - the picture's tapered tip - and k/8 + ¼ after, each later segment opening
  with the last pair again at ¼; `river_movement.tga`'s v the vertex's count over 3. Six vertices a segment, drawn as a
  strip. A mouth is a quad on the last pair's middle, twice their spacing across and three pixels on, a frame of
  `river_exit.dds` across it - TFH's map has none, so it is built from the reading alone. On TFH's map 722 sources and 282
  junctions make 721 rivers, 61,532 nodes and 364,866 vertices - the fact base's figures - in about 0.1 s.
  **The draw**: at every height and in every mode, after the beaches and before the land borders, blended by source alpha,
  tested at 8, the depth tested and not written [exe]; tex0 `river_body.tga`, tex1 `river_movement.tga`, tex3
  `colormap_water.dds` with its size twice and the map's, AbsoluteWorldMatrix the identity, vTime the clock - river.fx scrolls the
  flow by 0.3 a unit and darkens it to the water's colour; its tex2, the fog, it never reads. **The sources are `map.bin`'s
  list** (`Content/Map/CMapBin`), as the original takes them: a 20-byte head, a count and that many (x, y) pairs, then the
  raster the junctions read. The original tests `map.bin` by date - it is the map's while none of positions,
  provinces.bmp, terrain.bmp, definition.csv, adjacencies, terrain.txt, continent, region and coastlines is missing or newer
  [exe] - and not `rivers.bmp`, so it traces an edited river bitmap from the old sources. **Here `rivers.bmp` is tested
  too** (decided on 2026-11-06; `docs/compatibility.md`, entry 20), and where the file is missing, stale or
  does not read the sources are those its builder would write - every index-0 pixel by rows from the south and then from the
  west [exe] - and the log says why. On this machine the player's folder's `map.bin` is the original's rebuild of TFH's,
  newer than every file, and its 722 sources are the bitmap's in order; the one TFH ships is older than all ten, some by
  hundredths of a second as the install was written, so a player who never ran the original gets the builder's list -
  the same 722. *Ours*: a tile's rivers - by their source's tile - in one
  mesh, under 65,536 vertices, with three materials for the copies of the map as the sea zones' borders have; a loop of
  river pixels, which the original would walk for
  ever, cut and reported; vTime in seconds - its unit not read, and the flow's and the foam's speed were found
  right in play on 2026-11-06.
- **The surf, beach.fx's BeachShader near and BeachShaderColor far** (C.7; `Content/Map/CWavePixel`,
  `Interface/Map/CSurfQuads`): only where settings.txt's `waves` is on, and made once [exe]. The cache's seventh piece of a
  tile is its wave pixels, 28 bytes each - a coastal pixel the terrain paints as water, x and y, its type of 52 by where
  the land lies about it, and four offsets each type fixes; a province's records go to its own tile. One quad a record:
  its four corners and its texture coordinates by the type, from the original's table, and every fourth record of a tile,
  counted from its first, taking coordinates a tenth larger - but for one set whose u is 60 where a tenth more is 55, and
  0.825 cut to 82 [exe]; each vertex x and z twice the record's place less the tile's middle plus the corner, u and v in
  hundredths, every figure cut towards nought - so sixteen types' quarter corners move a quarter pixel towards the tile's
  middle. The quad's triangles 0-1-2 and 2-3-0, nothing culled. **The draw**: after the sea and before the sea zones'
  borders, at the height 0.31 [exe] between the sea's and the land's, the depth tested and written, so the land hides
  what runs under it; near, under the first of terrain_sheet_heights, `shore.tga` scrolled by 0.2 of vTime and masked by
  `shore_alpha.tga` twice, added (SRCALPHA / ONE, the pass's own) - the foam; far, black at 0.7 of
  `shore_alpha_outline.tga`'s alpha, blended by source alpha (the sea's left states) - the coast's line [mod: map/terrain,
  gfx/FX/beach.fx]. On TFH's cache 141,831 records and 567,324 vertices, x −173 to 219 and z −193 to 207 half pixels from
  their tile's middle - the fact base's figures. *Ours*: each vertex holds the map's own place, twice the tile's middle
  added, so three materials for each technique serve every tile; each tile's mesh drawn by two instances, a layer for each
  technique.
- **The trees, terrain_2_0.fx's TreeShader** (C.8; `Content/Map/CTreeReader`, `Interface/Map/CTreeField`): planted once as
  the map is made, and only where settings.txt's `renderTrees` is on then [exe] - `--trees` plants them whatever it says.
  `map/trees.txt` [mod] gives the tree types in its order, each a sheet and a `tree_density` - read in thousandths, 1.0
  where not given - which is **the chance a pixel has a tree**; terrain.txt's palette entries [mod] name a type in `tree`
  and give a `tree_density` that is **not a density but the sheet's row**: the row is 4 − d, d the key's whole number or,
  where the entry gives none, 4 less the index's place in its `color` list, and nought or less no trees [exe]. **The
  planting**: tile by tile (50 pixels, rows from the south), each tile's own pixels from its south row and west to east; a
  pixel whose province exists and whose palette index has a type and a d above nought takes a draw of a generator of the
  original's kind seeded 42, and has a tree where the type's density is at least the draw as a share of one - every such
  pixel spending a draw - its tree at the pixel's whole coordinates, 0.02 higher on land than on a sea province [exe]. A
  tile keeps its trees by type, then by province in the order of their first tree, then as walked. A hundred more draws
  turn a hundred template trees: **four flat horizontal cards** at heights 0.5, 0.375, 0.225 and 0.05, each a square of
  diagonal 2.4 turned by the tree's angle, from the top the sheet's columns of highlights, middle, dark base and shadow
  [exe] - nothing faces the camera, a canopy seen from above. A tile's n-th tree is template tree n mod 100 on its pixel,
  its v moved down its row by a quarter a row, and in the weather mode a tree of a province colder than −5 takes the
  sheet's snowed right half [exe]. On TFH's map 1,136,896 pixels draw and **1,002,520 trees** stand - forest, forest2,
  jungle and jungle2, whose density is 1 or more, every pixel of theirs, the fact base's figures; woods, woods2 and
  treemoun their 0.7, 0.7 and 0.5 shares - in 1,718 tiles, at most 2,500 a tile. The generator is the original's, its
  seeding and twist both as the bytes have them since 2026-11-07, so the chance types' trees should stand where the
  original's do; the fact base's simulation, its twist the textbook one, gave 1,002,313. **The draw**: under height 100 [exe], in
  the terrain and weather modes only, after the sea zones' borders, a pass for each type with its sheet; the shader lifts
  every card by the land's height; blended by source alpha, tested at 8, the depth tested and written. **At most 16 tiles
  show trees at once** [exe]: each tile in view, in the tiles' order, takes a slot with no tile or else one whose tile is
  out of view, and keeps it while in view; a slot's snow is fixed when it is filled, and choosing a mode empties them all.
  *Ours*: the planting runs on a thread of its own as the surface is made, about 0.4 s in the optimised build, no tree
  drawn until it is done; the tiles in view the ground the camera looks straight down on and two pixels more, where the
  original's list of visible tiles was not read; a tree's vertices in the map's own coordinates, three materials a type for
  the copies of the map.
- **The air map's rings, circle.fx's Standard** (`reversing/findings/FINDINGS-airmap.md`, section 8): a ring for each
  group `airmap.md` gives, nine vertices about the air base - the centre, then from north round anticlockwise seen from
  above at the radius - their uv `1 − |x|/r, 1 − |z|/r`, so `map/Sector.dds` [mod] is mirrored into each quarter with its
  corner (1, 1) at the centre; the triangles `3,2,1 3,1,0 0,1,8 0,8,7 4,3,0 4,0,5 5,0,7 5,7,6` [exe]; WorldMatrix the
  translation to the air base's position 1.0 above the ground [exe], View and Projection the shared camera's, vStrength
  (share, dim), SectorTex the picture; no technique chosen, so the file's one [mod]; the pass's depth test and write off,
  blended by source alpha; drawn after the whole map and its pictures, in the air mode alone and at every height.
  *Ours*: each ring's world matrix moved to the copy of the map nearest the camera, where the original makes no second
  ring across the seam.
- **The hierarchy's lines, hierarchy.fx's Hierarchy** (`reversing/findings/FINDINGS-hierarchy.md`, section 7): for the
  selected land unit and every land unit under it whose commander stands elsewhere, four vertices from its province's
  unit point to its commander's - `f ∓ a⊥` and `t ∓ a⊥`, a the line's direction times half its width, (size + 1) × 0.2 by
  the unit's rank [exe] - u 0 and 1 across, v 0 at the unit and `|t − f| / 2w × 0.2` at the commander [exe], range 1 in
  radio reach and 0 out, the colour by where the unit stands under the selected one (`mapunits.md`, section 8); triangles
  `0,1,2 1,3,2`; the end further east moved a map's width west where the ends are more than half the map apart [exe].
  The effect places them 2.0 up and runs the arrowheads by Time; drawn after the rings and the whole map, depth tested
  and written by nothing, **where they are and a map's width east, never west** [exe], under the near view's height and
  with `render_hierarchy` on. *Ours*: the effect's camera position nought and its world-view-projection the shared view
  × projection, the shared view holding the camera's place; Time 12 a second, the original's 0.2 a drawn frame at sixty
  frames.
- **The province names** (`reversing/findings/FINDINGS-farmap.md`, B.7; `Content/Map/CProvinceLabel`,
  `Interface/Map/CProvinceNameLayout`, `src/Map/CProvinceNames`): **under height 100, in every map mode and whatever the
  settings** [exe] - the original's shown byte is 1 for every province and nothing clears it - every province, land and
  sea, has its `PROV<id>` [mod: localisation] written **in its own case** in the font called `tahoma_60` [mod:
  interface/core.gfx], TFH's kart_font, black. positions.txt [mod] gives `text_position` as written, `text_rotation` in
  radians (0 where not given) and `text_scale` (1 where not given); a province with no `text_position` - 8,001 of TFH's -
  has its name **two map pixels south of the centre of its bounding box** [exe the 2.0], whatever its scale - the
  point the original's geometry pass writes before positions.txt is read (`reversing/findings/FINDINGS-provincenamepoint.md`).
  The fact base's reading had it at the map's (0, 0), never seen, from the template's constructor; a
  screenshot of the original, 2026-11-07, named Stolp, Belgard and Falkenburg, none with a `text_position`, where ours
  had nothing, and the reading was corrected to the centre; a screenshot of 2026-11-12 had Bautzen, Lauban, Sagar and
  four more 1.9 pixels south of ours, and the 2.0 was read. Its terrain is still counted around the plain centre
  (section 4 of `map.md`). **The layout** [exe]: a name over 50 characters cut to
  49; broken only at a glyph with no height - the space - and only where the line, the word and its space taken in, is
  wider than 320 font pixels, the break going before that word, so a word is never split and the last never tested; each
  glyph its advance and its kerning with the next; each line centred on the point, the lines stacked upwards, so **the
  block stands on the point**; turned anticlockwise by the rotation; a font pixel 0.05 × text_scale × 0.5 map pixels.
  **The slots** [exe]: at most 1000 names at once; each tile in view (50 pixels, a province's tile by the middle of its
  bounding box), in the tiles' order, gives its provinces' names - by id - free slots until none is left; a name **fades
  in over 1.5 seconds**, alpha 170 a second, nought not drawn, and only under 100; **a tile leaving the view frees its
  names**, while the camera is under 970, so they fade in again when it comes back, and one that stays in view keeps
  them across a zoom past 100. No depth: a later name over an earlier one. *Ours*: drawn on a 2D layer over the surface
  as the country names are, each letter the typeface standing for the font at its glyph's box; the tiles in view the
  ground the 2D view covers, where the original's list of visible tiles was not read; the names fade by the wall clock
  from the frame, as the original's clock is the renderer's seconds; a rename (`change_province_name`) not built, the
  province's name its history's.
- **The device states** around each draw, `FINDINGS-mapdraw.md` C.1 and C.2: the land unblended (ONE / ZERO); the beaches
  SRCALPHA / ZERO, which their Graphical and General passes override; the sea SRCALPHA / INVSRCALPHA; the baseline alpha
  test at 8 / GREATEREQUAL where a pass turns the test on - each handed to the translator as the states the pass is laid over.
  **The land is drawn with the alpha test off** since 2026-11-14 (*inferred*, `FINDINGS-airorders.md`, section 8): the land
  passes set none, the last write before the scene is the unit layers' test off, and the General shaders' alpha is the
  lookup's less 0.7, so under the test every colour of an alpha under 0.7 - the air and naval modes' a half, the air orders
  window's "nothing" - would drop its land whole. Until then the lookups were written at full alpha and nothing was dropped.

## 5. The generated textures

**5.1 The province colour lookups**: two 256 by 256 pictures, a texel for each province at (id mod 256, id / 256): the first
each province's colour of the map mode, the second its second colour, which the stripes blend in where it differs; texel 0
nothing [exe]. In style 0 the first holds the province's state instead: red 1.0 where the player knows it at 2 or more, 0.5
under, nought where it is not simulated; blue the share frozen in the Weather mode; green the highlight [exe]. In any other
style both hold the mode's colours as written, **alpha and all** - a selected province's alpha full (section 4, the highlight)
- and the sea's rasters take them word for word (B.2); so the air orders window's "nothing", alpha nought, reaches the
shaders as nought (`fleetsandair.md`, section 4). Until 2026-11-14 every alpha was written full here.

**5.2 The sea's**: two 1024 by 1024 rasters of the map, each texel the colour of the province under it (sampled nearest), grey
(0x80808080) off any province; and the fog, 1024 by 1024 of one channel, each texel four samples blended bilinearly -
1.0 where the province is simulated and the player knows it at 2 or more, 0.6 simulated and less, nought not simulated, and
a sea pixel 1.0 within 3 pixels of a simulated province [exe].

*Ours*: Godot's depth is reversed - one near - and its rendering device's clip y runs down the screen, so the translation
turns Direct3D's clip depth with `z = w − z` and its y over after the vertex shader (`shaders.md`, section 3.5).

## 6. Where it stands

**Built 2026-11-06: the land, the beaches and the sea** (`src/Map/CMapSurface`, `CSurfaceTile`), drawn through
terrain_2_0.fx and water.fx as translated, under the 2D layers, which it lies under pixel for pixel; tried with
`--screenshot` at four heights in the political and terrain modes, and it has not been reviewed in play. What was seen:
the far view's General_Low with the overlay laid over the political colours and their stripes; near, General's greyed
terrain sheet under them; in the terrain and weather modes Graphical, the terrain sheet itself - forest, plain, town -
dark where the player knows a province under 2, which for Germany on the first day is its own land and one ring round it
(346 provinces, 83 of them at 3); the beaches rounding the coast off the land's pixel steps. **The first screenshot
showed the surface upside down**, Godot's clip y running the other way to Direct3D's (section 5, *Ours*).

- **The map mode's style**: political, and revolt risk (ours, its style not read), 2; terrain and weather 0; supply 9;
  air 18 - which picks the sea's `…Color` techniques.
- **The colours**: `CMapView.RefreshColours` hands its two palettes to the surface, which makes the lookups, the sea's
  rasters and the fog from them (section 5) - about 15 ms, the texel each samples worked out once for the map.
- **What it costs**: at rest a frame of about 7 ms, the 2D map's own; a move of the camera a tenth of a millisecond; the
  surface's making about 0.9 s - the cache read in 0.2 s, 2,700 meshes and 1,774 terrain-index pictures in 0.7 - in the
  optimised build. **A crossing of 500** cost a frame of 120 to 145 ms at first, every time (reported in testing,
  2026-11-06): our own work was some 17 ms, and the rest the rendering server's - giving 2,700 materials another shader,
  and then, measured with each switched off in turn, showing or hiding a few thousand instances, the land's or the
  borders' alike. *Ours* since: **the land and the beaches are drawn four ways** - Graphical near and far, General,
  General_Low - each a material and an instance for every tile, sharing the tile's mesh and made the first time the way
  is wanted, the same style's other height made with it; the sea one material and instance
  for each technique the same way; and what is drawn chosen by **render layers and the camera's cull mask** - a layer for
  each way, one for the land borders, one for each technique of the sea - so a crossing changes one mask and no
  instance. A crossing now costs 13 to 21 ms, the first zoom inwards about 70, of which 37 is the counters' first near
  draw, as on the flat map.
  **The first choice of a mode drawn another way** - any but terrain and weather, the land's General ways, or air and
  naval, the sea's `…Color` techniques - then stalled most of a second on the materials of the way it wanted (reported
  in testing, 2026-11-07, since a game starts in the terrain mode). *Ours* since: the surface makes the rest one at a
  time, a way or a sea technique every fifteen frames, while the player looks at the map; a switch then costs what any
  colouring does, 80 to 150 ms, the colours some 40 of it and the rest the textures they rewrite. **Nothing is drawn ahead
  of its use**: each way made ahead was drawn under the one shown for two frames to warm it, which flickered the map as a
  game began (reported in testing, 2026-11-07) and, measured, made a first switch no faster; without the warming of
  the other height a first zoom inwards costs about 50 ms instead of 35.
- **What the camera does not see is not drawn** (`CMapCulling`, *ours*, 2026-11-19). The effects place every vertex
  themselves, so Godot cannot tell what is on screen and drew every tile's land, beaches, borders, water borders, rivers
  and surf each frame - 10,010 draw calls and 7 ms of the rendering server's time at the start's view, wherever the
  camera was. Each of those meshes now has the rectangle of the map its vertices reach - a tile's land is the whole of
  the provinces it was given, not its square of 50 - and is drawn only where that rectangle, moved to the copy of the
  map its tile is drawn at, meets what the camera sees: hanging straight down, it sees a rectangle of the ground, and what
  stands above the ground is drawn further out, never nearer. At the start's view 1,617 draw calls and 2.5 ms. **A mesh is
  hidden by taking its render layers away**: hiding the node took it out of the rendering server's tree of bounds, which
  with every mesh's bounds the whole world made the first zoom outwards run a second at a quarter of its frames. A mesh is
  left drawn for its first three frames. **The meshes are drawn in the order they were made**, the tiles' order, by
  each one's sorting offset: with every mesh's bounds the same, Godot's sort had no depth for the transparent ones and
  drew overlapping rivers, surf and borders in an order that changed with what was drawn - which culling would have
  made change as the camera moved. Set beside the uncull'd picture with the clock of the water and the rivers stopped,
  six views - the start's, near, far across the map's seam, and three others - came out the same to the pixel.
  `--nocull` draws every mesh, for setting the two beside each other.
- **A surface gives back everything it made as it leaves the tree** - its meshes, its some 4,500 materials and their
  textures - where until then each lived on until the managed side's collector took its wrapper. Under the debugger that
  was long enough for the lobby's surfaces, one for each world it shows, to still hold their texture sets when the game's
  borders were first drawn on zooming in, and the D3D12 rendering device's descriptor heap ran out, Godot logging every
  frame after (a test run, 2026-11-06). The heap is also raised to 262,144 descriptors in `project.godot`
  (`rendering_device/d3d12/max_resource_descriptors`), one surface zoomed in using a good part of the default.
- **A DXT1 file's see-through texels are decoded as Direct3D decodes them** - black with no alpha - where Godot's decoder
  makes every DXT1 texel solid (`CTextureManager`, for a file that has any). `map/terrain/stripes.dds` is one, and the
  province techniques mix in the second colour by its alpha: with every texel solid, occupied land was drawn wholly in the
  owner's colour and showed no stripes (reported in testing, 2026-11-06).
- **A compressed picture whose size is not a power of two is unpacked and each texel set to its nearest colour of five,
  six and five bits** (`CTextureManager`) - *inferred* as what the original's device holds: its texture handler rounds the
  size up to powers of two with no filter (A.3), so D3DX compresses the picture afresh into a texture of the new size, and
  a flat stretch then comes out as that nearest colour. TFH's `colormap_political.dds` is the case that showed it: a land
  and sea mask in DXT3, land 121 and sea 102, which the far technique lays under the province colours. Its own blocks
  decode to (123, 121, 123) near the coasts and (121, 121, 121) inland - a little greener on the GPU - so the far view's
  land near a coast and inland differed by a shade in 12-pixel steps (reported in testing, 2026-11-06). A screenshot
  of the original has the land (123, 121, 123)'s tint everywhere, red and blue two above green, inland Germany (151, 149,
  151); with the picture set so, ours is (150, 148, 150) and the step gone.
- **The original's `map/terrain/base.dds` keeps five of the ten levels its mipmap chain should have**, which Godot's
  decoder refuses; `CTextureManager` reads such a file's full-size picture alone and makes the levels again.
- **The land borders, built the same day** (section 4, *the land borders*): province lines grey and country lines in the
  original's red dashes, `borders.tga` under 150 and `borders2.tga` to 500, none in the far view. **A blended pass writes
  depth in Direct3D and not in Godot unless told**, so the translator now says `depth_draw_always` for one with
  `ZWRITEENABLE` on (`shaders.md`, section 3.7): without it the sea, drawn after, covered the General technique's beaches,
  which blend, and the coast showed the land's pixel steps. What a General beach lets through is the frame's clear colour,
  the original's mid-grey, which the surface's camera now clears to.
- **The far view's country names, built the same day**: `mapnames.md`.
- **The sea zones' borders and the highlight, built the same day** (section 4), and with them **nothing of this project's
  own is drawn over the surface any more**: `PoliticalMap.gdshader` draws the flat map only where the install has no quad
  cache - a mod with a map of its own and no cache - and the hover marking it drew is gone, the original marking nothing
  under the mouse. Checked with `--screenshot`: the Baltic's faint lines between its zones, and a click on a province
  lifting it while the one before goes dark.
- **The junctions of the land borders, built the same day** (section 4), after testing found the lines "very cut
  up" in a save of 1941: every place three provinces met lost its corner piece on every line into it.
- **The rivers and the surf, built the same day** (section 4), after two readings in the fact base (C.6, C.7), their
  counts the fact base's every one (`OpenHOI3.SaveCheck quads`). Checked with `--screenshot` and not
  reviewed in play: near, the Elbe, the Weser and the rest light bands over the land; far, the rivers fine white lines and
  every coast a dark line. **The near surf's foam was not made out** on a screenshot zoomed in on the German coast - the
  pass adds white by three alphas, so it is faint by its own art, and whether as faint as the original's is for a review in play.
- **The trees, built 2026-11-07** (section 4), after a reading in the fact base (C.8), which corrected the record's
  trees.txt reader - it reads `tree_density` too - and its quad a tree - four. Checked with `--screenshot` and `--trees`:
  the Black Forest a canopy of clumps seen from above, the Rhine plain bare; Karelia's trees snowed in the weather mode
  and green in the terrain mode. **The reference settings.txt has `renderTrees=no`**, so without `--trees` none are
  seen, as the original would show none. They have not been reviewed in play.
- **The province names, built 2026-11-07** (section 4), after a reading in the fact base (`FINDINGS-farmap.md`, B.7),
  which found the shown byte gating nothing and the record's `MapTile_Update` an instruction inside another function.
  Checked with `--screenshot` under Kassel: every name on its point, Göttingen, Duderstadt and Wuppertal turned as
  positions.txt turns them, in its own case, black. **kart_font's "Another Typewriter" had no stand-in**, and Liberation
  Sans drawn into its boxes left gaps round the narrow letters - "Bi el ef el d"; it is drawn in Liberation Mono since
  (`fonts/readme.md`), its letters 28 to 40 pixels wide being nearly a monospace. They have not been reviewed in play.
- **The hierarchy's lines and the air map's rings, built 2026-11-07** (section 4), through `hierarchy.fx` and `circle.fx`
  after readings in the fact base (`FINDINGS-hierarchy.md` 7, `FINDINGS-airmap.md` 8), which corrected the lines' seam
  - the end further east moved a width west, a copy drawn a width east only. Checked with `--screenshot`: OB West's
  corps and divisions in their blues with the arrowheads running, and the air mode's rings green round Germany's bases at
  every height. The 2D layers that drew both stay for a map drawn flat.
- **Next**: the map surface is whole but for terra incognita, a scenario's alone, which there are none of here. The
  counters and the flags are the interface's effects left.
