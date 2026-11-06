# Drawing the map through the install's own effect files

The map's surface - its land, beaches, sea, borders - drawn as the original draws it: the install's own meshes, through
the install's own `gfx/FX` effect files translated to Godot shaders (`shaders.md`), each fed what the executable feeds it,
so that a mod's terrain or water shader - BlackICE changes both - is the one seen. By the maintainer's word of 2026-11-06,
"We need to actually support moddable shaders", and his choice to do that before the far view. Read in
`reversing/findings/FINDINGS-mapdraw.md` (A the land and camera, B the sea, borders and generated textures, C the scene)
and `FINDINGS-farmap.md` (A the far view's thresholds). **[exe]** a rule the executable holds; **[mod: file]** what a file
may change; *Ours* where this departs.

## 1. What is drawn, in order

The original's scene, `FINDINGS-mapdraw.md` C.1: the land; the beaches; the rivers; the land borders under `border_cutoff`;
the sea; the surf where `waves`; the water borders; the trees under height 100; the country names in the far view; the
province names under 100. **Built so far: the land, the beaches, the land borders, the sea, the sea zones' borders, the highlight and the country names** (section 6). The overlays this project draws
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
nothing of it is read - no meshes, no border corners - the map is drawn flat and the log says why. *Measured* on
2026-11-06: TFH's map files make `93E125C4`, as `tfh/map/cache/time` and the user folder's `time` hold, and BlackICE
GitHub's make `383756A4`, as its user folder's does. Working the stamp out takes about 140 ms, as the session's map is set
up.

*Not built*: a mod's `user_dir`, since no mod is played yet - the base game's user folder is the one asked. With a mod
layered, its user folder is the one to pass, and a mod played before the original has built its cache gets the flat map.

## 3. Space and the camera

The original's world: x the map's pixel column, z its row counted from the south, y up; a pixel's middle on whole
coordinates; the land at y 0.35, the sea at −0.32 [mod: the effect files]. This project's 2D map has north at the top, a
pixel spanning [x, x+1] and display rows counting down, so **a 2D point (X, Y) is the world's (X − 0.5, H − 0.5 − Y)**, H the
map's height in pixels.

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
  pixel (*inferred* there; the maintainer's screenshot of the original's far view shows none). On TFH 668,662 quads in
  3,638 tiles: the borders between sea zones, and never the coast, which is the surf's. *Ours*: a vertex holds the map's
  own coordinates where the original's holds the tile's, the product with the world-view-projection the same, so three
  materials - a tile drawn a map's width west, where it is, a width east - serve every tile and a move of the camera sets
  three matrices.
- **The highlight** (B.9): a selected land province's texels in both lookups lifted - in style 0 the green 25 and nought
  for any other province, in any other style red, green and blue +25 (255 from 230 up) and alpha full - so the land shader
  shows it +0.098 in the terrain modes and about +0.044 in the others, and the sea never [exe]. Which provinces is the
  selection; the lookups are written again whenever it changes (`CMapSurface.SetSelection`); nothing marks the province
  under the mouse.
- **The device states** around each draw, `FINDINGS-mapdraw.md` C.1 and C.2: the land unblended (ONE / ZERO); the beaches
  SRCALPHA / ZERO, which their Graphical and General passes override; the sea SRCALPHA / INVSRCALPHA; the baseline alpha
  test at 8 / GREATEREQUAL where a pass turns the test on - each handed to the translator as the states the pass is laid over.

## 5. The generated textures

**5.1 The province colour lookups**: two 256 by 256 pictures, a texel for each province at (id mod 256, id / 256): the first
each province's colour of the map mode, the second its second colour, which the stripes blend in where it differs; texel 0
nothing [exe]. In style 0 the first holds the province's state instead: red 1.0 where the player knows it at 2 or more, 0.5
under, nought where it is not simulated; blue the share frozen in the Weather mode; green the highlight [exe].

**5.2 The sea's**: two 1024 by 1024 rasters of the map, each texel the colour of the province under it (sampled nearest), grey
(0x80808080) off any province; and the fog, 1024 by 1024 of one channel, each texel four samples blended bilinearly -
1.0 where the province is simulated and the player knows it at 2 or more, 0.6 simulated and less, nought not simulated, and
a sea pixel 1.0 within 3 pixels of a simulated province [exe].

*Ours*: Godot's depth is reversed - one near - and its rendering device's clip y runs down the screen, so the translation
turns Direct3D's clip depth with `z = w − z` and its y over after the vertex shader (`shaders.md`, section 3.5).

## 6. Where it stands

**Built 2026-11-06: the land, the beaches and the sea** (`src/Map/CMapSurface`, `CSurfaceTile`), drawn through
terrain_2_0.fx and water.fx as translated, under the 2D layers, which it lies under pixel for pixel; tried with
`--screenshot` at four heights in the political and terrain modes, and the maintainer has not looked at it. What was seen:
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
  optimised build. **A crossing of 500** cost a frame of 120 to 145 ms at first, every time (the maintainer's report,
  2026-11-06): our own work was some 17 ms, and the rest the rendering server's - giving 2,700 materials another shader,
  and then, measured with each switched off in turn, showing or hiding a few thousand instances, the land's or the
  borders' alike. *Ours* since: **the land and the beaches are drawn four ways** - Graphical near and far, General,
  General_Low - each a material and an instance for every tile, sharing the tile's mesh and made the first time the way
  is wanted, the same style's other height made and drawn with it for the first frames; the sea one material and instance
  for each technique the same way; and what is drawn chosen by **render layers and the camera's cull mask** - a layer for
  each way, one for the land borders, one for each technique of the sea - so a crossing changes one mask and no
  instance. A crossing now costs 13 to 21 ms, the first zoom inwards about 70, of which 37 is the counters' first near
  draw, as on the flat map.
- **A surface gives back everything it made as it leaves the tree** - its meshes, its some 4,500 materials and their
  textures - where until then each lived on until the managed side's collector took its wrapper. Under the debugger that
  was long enough for the lobby's surfaces, one for each world it shows, to still hold their texture sets when the game's
  borders were first drawn on zooming in, and the D3D12 rendering device's descriptor heap ran out, Godot logging every
  frame after (the maintainer's run, 2026-11-06). The heap is also raised to 262,144 descriptors in `project.godot`
  (`rendering_device/d3d12/max_resource_descriptors`), one surface zoomed in using a good part of the default.
- **A DXT1 file's see-through texels are decoded as Direct3D decodes them** - black with no alpha - where Godot's decoder
  makes every DXT1 texel solid (`CTextureManager`, for a file that has any). `map/terrain/stripes.dds` is one, and the
  province techniques mix in the second colour by its alpha: with every texel solid, occupied land was drawn wholly in the
  owner's colour and showed no stripes (the maintainer's report, 2026-11-06).
- **A compressed picture whose size is not a power of two is unpacked and each texel set to its nearest colour of five,
  six and five bits** (`CTextureManager`) - *inferred* as what the original's device holds: its texture handler rounds the
  size up to powers of two with no filter (A.3), so D3DX compresses the picture afresh into a texture of the new size, and
  a flat stretch then comes out as that nearest colour. TFH's `colormap_political.dds` is the case that showed it: a land
  and sea mask in DXT3, land 121 and sea 102, which the far technique lays under the province colours. Its own blocks
  decode to (123, 121, 123) near the coasts and (121, 121, 121) inland - a little greener on the GPU - so the far view's
  land near a coast and inland differed by a shade in 12-pixel steps (the maintainer's report, 2026-11-06). His screenshot
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
- **The junctions of the land borders, built the same day** (section 4), after the maintainer found the lines "very cut
  up" in a save of 1941: every place three provinces met lost its corner piece on every line into it.
- **Next**: the rivers, the surf and the trees, in the scene's order - each wanting a reading first: what the rivers' meshes
  are made from, how the surf's records of the cache become quads, where the trees' points come from.
