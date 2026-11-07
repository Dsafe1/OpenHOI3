# Shaders: the original's effect files, translated

The original draws its map and much of its interface through **effect files** - `gfx/FX/*.fx`, DirectX 9 HLSL at shader
models 1.1 and 2.0, plain text in the install - and a mod may ship its own: BlackICE changes `terrain_2_0.fx` (its terrain
tiling, edge noise, brightness and fog of war), `water_include.h` (its sea's colour, no waves, no fog) and `circle.fx`
(the air map's green fill). So they are not rewritten by hand here: **each is read as it is and translated, pass by pass,
into a Godot shader** when the game loads, by the maintainer's word of 2026-11-06 - "We need to actually support moddable
shaders". Built that day. **The interface's buttons, icons and scrollbars draw through `buttonstate.fx` since then**
(section 3.10), and **the map's land, beaches and sea through `terrain_2_0.fx` and `water.fx`** (`mapdraw.md`). Ours
throughout: the effect files are [mod] and what is done with them
is this project's.

## 1. What a file holds

Textures - each with an annotation naming the file it starts as, `< string name = "Base.tga"; >` or `ResourceName` -
samplers reading them with their filter and address states, parameters the engine sets (matrices, colours, sizes),
constants, structs whose fields carry semantics (`POSITION`, `TEXCOORD0`, `COLOR`), functions, and **techniques**: named
ways of drawing - `WaterFar`, `TerrainShader_General_Low` - each of passes that set render states and compile a vertex and
a pixel shader, `VertexShader = compile vs_2_0 Name(arguments)`, the arguments filling the entry's `uniform` parameters.
**The engine picks a technique by name** and sets the parameters by name: which, when and with what is the executable's
(`reversing/findings/FINDINGS-farmap.md`, A, for the map's).

The base game, Their Finest Hour and BlackICE hold 79 effect files and includes between them: 88 passes once BlackICE's
`gfx/FX` is laid over the install's, every one with both shaders but `highlight.fx`'s and `waterdot4.fx`'s, which leave
both to the fixed pipeline. About twenty intrinsics are used; `#define` is object-like only.

## 2. Reading (`Content/Shaders`)

- **Through the layers**: a file and its `#include`s are looked for in `gfx/FX` of the highest layer that has them
  (`CFxEffectReader`), a mod's over the install's.
- **The preprocessor** (`CFxPreprocessor`): comments out first, lines kept in place; `#include` of a file again each time
  it is named - `water.fx` includes `water_include.h` six times under different flags, and that file's own include guard
  covers its declarations alone; object-like `#define` and `#undef`, a macro replaced word by word, again inside what
  replaced it, never inside itself or inside a number (the `f` of `0.5f`); `#ifdef`, `#ifndef`, `#else`, `#endif`,
  nested. A directive it does not follow is reported.
- **The parser** (`CFxParser`): structs, textures with annotations, samplers with `sampler_state` blocks, global
  variables with qualifiers, semantics, annotations and initial values, functions with `in`/`out`/`inout`/`uniform`
  parameters and semantics, statements (blocks, declarations, `if`, `for`, `while`, `return`, `break`, `continue`,
  `discard`) and C's expressions with casts and initialiser lists; techniques and passes, a state's value kept as written.
  What it cannot read is reported by line and skipped to the next `;` or block.

## 3. Translating (`CGodotShaderWriter`)

Each pass becomes **a Godot spatial shader** (`CGodotShader`): its code, the parameters and textures the engine is to set
on it, which channel of a mesh each vertex input is read from, the pass's states as written, and what could not be
translated.

1. **Only what the pass reaches**: the two entry points and the functions, structs, parameters, constants and samplers
   they name, in the file's order.
2. **Types as HLSL types them, written as Godot's stricter language wants**: every conversion HLSL makes without a word
   made explicit - a vector cut down to its first components (`float3 rgb = c` is `c.xyz`), a scalar spread over a vector,
   an int where a float is wanted (`2` is `2.0`), a vector comparison's bools turned to floats (`float3 m = c < .5` is
   `vec3((lessThan(c, vec4(0.5))).xyz)`), a swizzle of a scalar (`grey.rrr` is `vec3(grey)`), a condition that is a number
   tested against nought. HLSL's `fmod` keeps the dividend's sign where Godot's `mod` keeps the divisor's, so it is
   `a - b * trunc(a / b)`; `clip(x)` discards where `x` is below nought; `(VS_OUTPUT)0` is the struct of noughts; `lerp`,
   `saturate`, `frac`, `rsqrt`, `atan2`, `ddx` and the rest are Godot's names for them.
3. **Matrices are kept transposed**: an HLSL row is a Godot column. That makes `mul(v, M)` `M * v` and `mul(M, v)`
   `v * M`, and leaves indexing (`M[i]` is row i) and constructors (`float3x3(r0, r1, r2)`) as written. A matrix of rows
   by columns that is not square - `float4x3` - is the padded square, `mat4`, whose missing entries are nought; a vector is
   padded with noughts before the product and the result cut to the HLSL size, which also gives HLSL's implicit
   truncation of `mul(float3, float4x4)`. **The engine sets a matrix parameter so**: the HLSL row-major matrix's rows as
   the Godot matrix's columns.
4. **Names**: the file's own, but one of Godot's words or built-ins is prefixed `fx_`, and a local or parameter that would
   hide a global, a function, a struct or an enclosing block's local - which HLSL takes and Godot calls a redefinition -
   gets a fresh one. `CShaderUniform` gives the parameter's name in both.
5. **The two stages**: `vertex()` builds each input of the entry point from a mesh's channels by semantic - `POSITION`
   from `VERTEX` with w one, `NORMAL`, `TANGENT`, `COLOR0` from `COLOR`, texture coordinates `UV` and `UV2` while they fit
   in two components and otherwise and after them `CUSTOM0` to `CUSTOM3` (`CShaderVertexInput`), a `POSITION` of four
   components - the sea's (x, z, u, v) - a whole custom channel - calls it with the `compile` line's values for its
   `uniform` parameters, writes the clip-space position to `POSITION` - its depth turned with `z = w − z`, Godot's being
   reversed, and its y turned over, the rendering device's clip y running down the screen where Direct3D's runs up; both
   are what Godot's own projection does and a written `POSITION` skips, and the map's screenshot showed the second - and every other
   output to a varying named by its semantic, **a colour clamped to nought and one as Direct3D 9 clamps it** between the
   stages. `fragment()` reads the pixel shader's inputs from those varyings by semantic - one the vertex shader does not
   give is nought and reported, as is the position, which these shader models cannot read - calls it and writes its
   colour as the states in force say (7).
6. **The fixed pipeline**, for a pass with no vertex shader: the position by a transform the engine sets
   (`fx_fixed_transform`), the vertex's colour and first texture coordinate passed on; with no pixel shader, the vertex's
   colour.
7. **Render states**: a pass draws under **the device's states as the engine leaves them at the draw, with the pass's own
   laid over them** - Direct3D's way, and the effect files lean on it: `buttonstate.fx` turns blending on and leaves the
   factors to the engine, and the far beaches' technique sets nothing and draws SRCALPHA / ZERO because the engine set that
   (`reversing/findings/FINDINGS-mapdraw.md`, C). The caller hands the device's states by name; where it hands none, a
   blend by source alpha is assumed, as the interface draws. From what is then in force: drawn unshaded, without Godot's fog
   and never culled (the map is flat, and the winding the original's matrices give was not set against Godot's); the two
   blend factors as Godot's blend modes exactly - SRCALPHA / INVSRCALPHA mixed, SRCALPHA / ONE and ONE / ONE added (the
   latter with a solid alpha), ONE / INVSRCALPHA premultiplied, DESTCOLOR / ZERO and ZERO / SRCCOLOR multiplied, ONE /
   ZERO solid, and **SRCALPHA / ZERO, which no mode of Godot's is, as the colour times its alpha written solid** - any other
   pair mixed and reported; **the alpha test** where `ALPHATESTENABLE` is on, `ALPHAFUNC` against `ALPHAREF` over 255 - the
   engine's 8 and GREATEREQUAL, so a tested pass discards alpha under 8/255 - as a `discard`; and `ZENABLE` and
   `ZWRITEENABLE` off the depth test and depth writes - **and `ZWRITEENABLE` on, for a pass that blends, as
   `depth_draw_always`**: Direct3D writes depth whether a pass blends or not, Godot's blended materials do not unless told,
   and the map's sea, drawn after the beaches, covered them until this (`mapdraw.md`, section 6).
8. **Samplers**: a `uniform sampler2D` with Direct3D's defaults where the file says nothing - point filtering, no mipmaps,
   wrapping - linear where either filter is linear or anisotropic, mipmapped where `MipFilter` is not `None`, repeating
   where `AddressU` wraps (`Mirror` and `Border` do not repeat). `CShaderTexture` gives the texture it reads and the file
   that texture's annotation names; two samplers of one texture are two uniforms given the same texture.
9. **Colour space**: Direct3D 9 draws in gamma space and Godot's 3D renderer in linear, its output converted to sRGB; so
   textures are sampled as they are (no `source_color`), and the colour written is converted from sRGB to linear
   (`fx_to_linear`), which the renderer's own conversion undoes. Blending happens in linear space here and in gamma space
   in the original - a difference for blended passes, not yet measured.

10. **The canvas** (`EShaderTarget.Canvas`), for the interface: Godot's 2D renderer places the element, so the effect's
    vertex shader runs for what else it gives - its texture coordinates, `buttonstate.fx`'s frame offset - its position
    read from Godot's `VERTEX` with nought depth, its first texture coordinate from `UV` and its colour from `COLOR`, and
    what it works out for `POSITION` dropped; a canvas has no normal or second coordinate (reported, nought). **The drawn
    texture**: one texture of the effect may be named the element's own - `buttonstate.fx`'s `tex0` - and every sampler of
    it then reads Godot's `TEXTURE`, handed down as a hidden parameter to every function that samples it, since Godot
    shows `TEXTURE` to the stage functions alone; so an element is drawn as any other, a scrollbar's three textures through
    one material among them, and the sampler's filter is the element's own. The colour is written as it is - Godot's 2D
    draws in gamma space as Direct3D 9 does - its alpha solid where the pass does not blend, and multiplied by the
    element's modulate, which stands for the colour the engine gives an element; not again where the vertex shader read
    the vertex colour, which already carries it. `CEffectShaders` in the client reads and translates a file once and
    keeps each technique's `Shader`; `CButtonShader` picks `buttonstate.fx`'s `up`, `over`, `down` and `disable` by the
    element's look (`interface.md`, section 5). Checked on 2026-11-06: the main menu's greyed buttons grey, and the mouse
    over Single Player raises it 22 a channel on average - the file's 0.1 of 255 less the pixels already full - the button
    beside it unchanged.
11. **Shared parameters**: a caller may name parameters to be Godot's **global uniforms** under names it gives -
    `global uniform mat4 fx_map_view;` in place of the effect's `ViewMatrix` - which it registers with the rendering
    server before any shader naming them is compiled, and then sets once for every material at the same time. Godot gives
    a global uniform no value of its own, so a shared parameter's initialiser is dropped, and an array is never shared.
    `CShaderUniform.Shared` marks one. The map's land and beaches share their view and projection: 2,700 materials take
    one matrix each (`mapdraw.md`, section 3).

What is kept that looks wrong, the files' own: `terrain_2_0.fx`'s overlay blend in `TerrainShader_Graphical` and
`TerrainShader_General_Low` tests the overlay's red channel where its green is blended; `shadowmap.fx`'s `RenderShadow`
reads a texture coordinate its vertex shader never gives (reported, nought). The slip shows only where the overlay
texture's red and green fall on opposite sides of a half: the base game's `map/terrain/map_overlay_tile.dds` is grey in
every one of its 262,144 pixels, so nothing; BlackICE's, a coloured 1024 by 1024, in 0.25% of its pixels, moving their
green by at most 5 of 255 - not to be seen (measured 2026-11-06).

## 4. What is checked, and how

- `OpenHOI3.SaveCheck shaders <install> [mod folder] [out=folder] [file.fx] [pre=file] [canvas]` reads and translates
  every effect file, a mod's folder laid over the install's, and lists every problem; `out=` writes each pass as
  `file.technique.pass.gdshader`, `pre=` the one file given preprocessed, `canvas` every pass for the canvas with the
  file's first texture the drawn one. On 2026-11-06: the install alone and with BlackICE, **37 of 38 files and 87 of 88
  passes without a problem**, the one `shadowmap.fx`'s above; for the canvas every pass compiles, the map's reported as
  wanting what a canvas does not have.
- `--shadercheck=folder` gives every `.gdshader` in a folder to Godot's renderer to compile and quits (it needs a window,
  a headless run compiles nothing), each name printed before its `SHADER ERROR`s. On 2026-11-06 **all 88 passes of the
  install with BlackICE, and of the install alone, compile without an error** - the positive control the run before it,
  whose three errors (a local hiding a global) it printed.
- `ShaderTranslatorTests`: the preprocessor's conditionals and repeated includes, macros, the parser's samplers and
  passes, `mul` by a matrix and a padded `float4x3`, the silent conversions, the renamed local, the samplers' hints, a
  pixel shader reading what the vertex shader does not give.

## 5. Not built

- Shader model 3 and asm effects, function-like macros, `#if` expressions: none of the files has them.
- Caching the translations: to be measured first, by `CLAUDE.md`'s rule. On 2026-11-06 the map's six terrain techniques
  and six of the sea's are translated as a game is made, inside the surface's 0.9 s (`mapdraw.md`, section 6).

## 6. Next

The map's surface is drawn through its own files whole (`mapdraw.md`, section 6), and since 2026-11-07 the hierarchy's
lines and the air map's rings through `hierarchy.fx` and `circle.fx`. Left: the counters and the flags, which this
project still puts together on the CPU - `counter.fx` and `maskedflag.fx` are the original's for them, read in
`reversing/findings/FINDINGS-counters.md` (section 10) and `FINDINGS-maskedflag.md`. The flags want the canvas route to
take a second set of texture coordinates (`maskedflag.fx`'s mask's), which it gives as nought today.
