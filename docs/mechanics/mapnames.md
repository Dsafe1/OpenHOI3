# The far view's country names

In the far view every country owning land, but the rebels, has its name laid over the land it controls that is joined to
its capital, bent along that land, in the font of its faction. Read in `reversing/findings/FINDINGS-farmap.md`, B; the
rest of the far view - its techniques and its thresholds - is `mapdraw.md`. **[exe]** a rule the executable holds;
**[mod: file]** what a file may change; *Ours* where this departs. Built 2026-11-06 (`Interface/Map/CCountryMapNameLayout`,
`CMapLine`; `src/Map/CCountryMapNames`).

## 1. Which names

One for every country that owns a province, but `REB` [exe]. Its land is a flood from its **declared** capital - taken
without a test - over neighbours, in the map's order, that are land the country **controls** [exe]: occupied land counts
for the occupier, only the piece joined to the capital is named, so a country in parts has one name, and a country wholly
occupied a fill of its capital alone. The text is the country's name, upper-cased where its font's `effect` is on - the
default - by Windows' ANSI table for Windows-1252 (a to z, the accented small letters, š œ ž ÿ) [exe]; TFH's Axis font
says `effect = no`, so the Axis names keep their case [mod: interface/core.gfx].

## 2. Which font

`neutral_country`, `comintern_country`, `axis_country`, `allies_country` [exe the names; mod: interface/core.gfx], by
the country's faction's `icon` [mod: common/ideologies.txt] - none 0, TFH's Comintern 1, Axis 2, Allies 3. *Ours*: an icon
above 3 takes the neutral font, where the original reads past its table. The names are the colour of the font's own
atlas - TFH's are black ink [mod: gfx/fonts/*.tga] - the font's `color` key unread on this path [exe]. *Ours*, by
`CLAUDE.md`'s settled decision on fonts: each letter is drawn from the typeface the font stands for, its glyph's box -
from the `.fnt` - where the original's textured quad stands, its pen and the line it stands on found from the box's middle
by the glyph's offsets.

## 3. Where a name goes

In map units, the province bitmap's pixels, y counted from the south [exe]:

1. **Sampling**: every second pixel of each province's box of the piece - from its corner, ((w − 1) >> 1) + 1 steps each way
   - counts where it is that province or any sea, lakes too, so sea inside a coastal box counts, once for each box over it.
   N, the sums of x, y, xy, x², y² in 64 bits, and the extent of what counted. N = 0: no name.
2. **The line**: least squares y on x from the least x to the most, and x on y from the least y to the most; each line's
   ends truncated to whole pixels, rasterised by Bresenham's walk (at most 256,000 points), **trimmed** - cut to its first
   and last pixel of the piece; nothing where any point between is off the map; then the longest run of pixels of the
   piece, land nobody controls, or sea, the first of equal runs winning - and its ends put left to right. The first line
   is kept where its squared length is strictly the greater [exe]. **A name may cross the sea, never foreign land.**
   *Ours*: "own land" in the trim read as the piece, which the bend's own trim names; the two differ only for a country
   in parts.
3. **The size**: s = min((L / w) × 10, (N / L × 40) / h, 25), w the name's width in the font's pixels - the sum of its
   advances, *ours* without the kerning pairs, which the descriptor reader drops - h its line height. **No name** where six
   units a character, spaces counted, exceed L, or s < 2.5 [exe].
4. **Blanks**: while the spaced name's width × s / L < 11, one more blank after every character but the last, a space in
   the name among them [exe]. *Ours*: at most 64, where a font with no blank would loop the original for ever.
5. **The anchor**: the line's middle less its left normal × (h × 0.5 × s × 0.04) [exe].
6. **The letters**: the spaced name centred on the anchor along the line, the pen from half its width back; each glyph's box
   from pen + xoffset to pen + xoffset + width along, (h − yoffset − height) to (h − yoffset) up, all × 0.05 s; a glyph
   under one high only moves the pen [exe].
7. **The bend**, but where the line rises by 0.1 or less: through each letter's middle the line of slope −dx/dy from the
   least x to the most, trimmed **without** the sea, and its run's middle; F the first letter's, Lst the last's, A the
   mean of all; P₀ = F + |F − A| · dir(2·angle(F − A) − angle(A − Lst)) and P₃ the same at Lst - the fact base marks this
   closed form *inferred* as geometry; both pairs swapped where P₀ is east of P₃, so the name reads from the left; then
   character i of the name, n its length, t = i/(n − 1), spaces skipped: D3DX's Catmull-Rom through (P₀, F, A, Lst) at
   2t for t ≤ 0.5 and (F, A, Lst, P₃) at 2t − 1 after; the letter's box made again on the sample along the tangent, taken
   0.01 back where the parameter is above 0.5 and ahead otherwise [exe]. So a bent name's letters stand evenly by
   character from F through A to Lst. *Ours*: a letter whose across-line the trim empties stands at its own middle, where
   the original reads before its vector.

## 4. When

**Shown** in the far view - the camera above the first of `terrain_sheet_heights`, 500 [mod: map/default.map] - in any map
style but 0, the terrain and weather modes [exe]; no setting turns them off, and they come and go with no fade [exe].
Between heights 100 and 500 no name of either kind is drawn [exe]. They are drawn after the land, the borders and the sea
and before the units' layers. **Laid out** when next drawn after what one rests on changes [exe: a dirty mark on a change
of controller, faction, vassal or annexation]. *Ours*: a name rests on its piece of land, its font and its text; the map
view asks after them as it recolours - every change of hands - and once a day, for the factions, and only a changed one is
laid out again. A change of capital alone does mark nothing in the original (*inferred* there), and here it changes the
piece and so the name.

## 5. Checked

`CountryMapNameTests`: a level strip named along its middle, spaced and unbent at the size the land gives; a name too long
for its line not drawn; the trim's run over the sea and not over foreign land; a slanting band named along its middle and
read from the left; the spline's ends, the ANSI upper case and the blanks. By eye with `--screenshot` on 2026-11-06, the
start of 1936 over Europe: every large country named and bent along its land - SWEDEN up its length, ITALY down the
peninsula, `Germany` in the Axis font's own case - and the small ones, Belgium, the Netherlands, Denmark, Switzerland,
too small for the rule of six. The maintainer has not looked at it.

## 6. Not established

The original's GetTextSize - whether a name's width counts the kerning pairs; the TEXTUREFACTOR the names are drawn under,
so whether they are wholly opaque; what fills the country-name table, so whether a name given by script reaches the map
(here it does).
