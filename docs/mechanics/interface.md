# The interface: what the original's files say, and how they are put on screen

How the original reads its interface files and turns them into what is on screen, written
as rules this project can implement. No addresses and no offsets. Each rule says how sure
it is - `confirmed`, `likely`, `inferred` in the fact base's vocabulary - or **measured**,
meaning counted on 2026-10-08 over the 58 interface files an install of Their Finest Hour
ends up with, or seen in a screenshot of this project drawing them. Where this project
chooses its own behaviour the paragraph is marked **Ours**.

Sources: `reversing/findings/FINDINGS-gui.md` (the registries and the lookup),
`reversing/findings/FINDINGS-guicontainers.md` (the keys a window takes),
`reversing/findings/FINDINGS-guistatic.md` and `reversing/findings/FINDINGS-guilive.md`
(how a live window keeps its children), `reversing/findings/FINDINGS-text.md` (fonts), and
`reversing/findings/FINDINGS-topbar.md` (the orientation keywords, frames, and how a text
box is set).

What a particular screen puts in its elements is that screen's own spec: `topbar.md`.

## 1. The files

- **`interface/*.gui` holds layout and nothing else.** `confirmed` Each file is one or more
  `guiTypes = { ... }` blocks holding elements. What a window *does* is in the game's code,
  which finds elements by name (section 4).
- **`guiTypes` is the only key the original reads at the top of a file.** `confirmed` Any
  other is dropped without a word, so a file that opens with a `windowType` loads nothing.
- **`interface/*.gfx` names the pictures.** `confirmed` A `spriteTypes` block declares
  sprites - a name beginning `GFX_` by convention, a texture, and for a sheet its number of
  frames - and a `bitmapfonts` block declares fonts. An element names a sprite or a font;
  it never names a file.
- **A file in a higher layer replaces the file of the same name below it, whole.** `measured`
  The base game has 53 and Their Finest Hour 36, of which 31 replace one of the base
  game's and 5 are new; 58 are left.
- **Names are compared without regard to case, and a second element of a name takes the
  place of the first.** `confirmed` The first is leaked and nothing is said. The 58 files
  have no such pair at the top level; BlackICE has three.
- **In a quoted string a doubled backslash is one, and a single one is one too.**
  `measured` The files write paths as `"gfx\\interface\\x.dds"`, and here and there
  forget to double: `"gfx\mapitems\strategic_resources\\x.tga"`,
  `"gfx\\pictures\tech\\noimage.tga"`. Both name files the install has, and the game
  shows them, so a backslash before an ordinary letter is not an escape. This was read
  as one until the production screen's strategic resources came out blank.

## 2. The kinds of element

`measured` for the counts, over the 58 files.

| keyword | how many | what it is | drawn here |
| --- | --- | --- | --- |
| `guiButtonType` | 972 | a sprite that can be clicked, with a caption | yes |
| `iconType` | 843 | a sprite | yes |
| `instantTextBoxType` | 777 | a box of text | yes |
| `positionType` | 400 | a name for a pair of numbers; nothing to draw | - |
| `windowType` | 358 | a box of elements, windows among them | yes |
| `textBoxType` | 341 | a box of text | yes |
| `listboxType` | 128 | a scrolling list the code fills | yes |
| `checkboxType` | 99 | a button that stays down | yes |
| `scrollbarType` | 34 | four buttons and a range | yes |
| `shieldtype` | 22 | a country's flag in a frame, in a dialog - an icon on a masked shield sprite (2026-10-12) | yes |
| `OverlappingElementsBoxType` | 21 | a row of things that may overlap | yes |
| `editBoxType` | 21 | text the player types | shown, not typed in |
| `eu3dialogtype` | 15 | a window that may also hold shields | yes |

Three more child keywords are read by the original and used by no file of its own or of
BlackICE's: `3dButtonType`, `multiSpriteButtonType`, `threeDguiType`. `confirmed`

**A button's words show only on a text sprite.** *Measured*: a `guiButtonType` whose sprite is declared as a plain
`spriteType` draws no words of its own - not its file's `buttonText`, nor any the code gives it - where one on a
`textSpriteType` does. The maintainer's screenshots of the original, 2026-10-14: the research queue's stop button
(`cancel`, `buttonText = "--"`, a plain sprite) shows no "--", and the intelligence screen's `target_priority` (a plain
sprite, `SEND_SPY_PRIORITY`) shows its words once - the label beside it, `target_priority_label`, saying them; Their
Finest Hour's file has the button's text sprite commented out. Of Their Finest Hour's buttons with words, 205 are on text
sprites and 26 on plain ones; of the screens built, the plain ones are those two and diplomacy's `select_button`. The
routine was not read; the rule is the screenshots'.

**What a window takes.** `confirmed` Fifteen keys of its own - `size`, `position`,
`fullScreen`, `parent`, `priority`, `background`, `moveable`, `verticalScrollbar`,
`horizontalScrollbar`, `horizontalBorder`, `verticalBorder`, `click_to_front`,
`orientation`, `upsound`, `downsound` - and thirteen kinds of child. Every element of any
kind also takes `name`, `dontRender`, `pdx_tooltip`, `pdx_tooltip_delayed` and `scope`.

**What the other kinds take is not read out of the original.** *Ours:* the keys each kind
is given somewhere in the 58 files are the keys it takes here, and any other is reported.
A mod that uses a key the base game never does will be told so and may be right.

**Keys and keywords are matched without regard to case.** `inferred` The files write
`Orientation` and `orientation`, `listboxType` and `listBoxType`, and both work in the
original.

**A key an element gives twice leaves what the second said.** `inferred`: the original
reads an element a key at a time, each into its own place, so a later one writes over an
earlier (`reversing/findings/FINDINGS-guicontainers.md`). It matters once in the base
game: `brigade_entry_3`'s `select` button has `position = { x=0 y=4 }` and then
`position = { x= 3 y = 0 }`, and the line it is the ground of sits at the second - drawn
at the first it was "a little too low and left" beside the original (the maintainer,
2026-10-08; `reversing/findings/FINDINGS-builders.md`, section 6). Elements declared
inside an element are each their own, however many share a keyword.

*Ours, and stricter than the original:* a top-level key that is not `guiTypes`, a keyword
that is no kind of element, a key a kind does not take, a key given twice, and an
orientation or a format that is not a known word are each reported, once for each kind
of complaint with a count. The 58 files give six of the fourth - the position above, a
`format` on a text box in two files and four of a window's keys in `metaserver.gui` -
and none of the others.

**Three kinds hold what the code puts in them**, and the files say only how. The list box
has been read out of the original. The other two have not; each of those is drawn here
by the plain reading of its declaration, and is *ours* until it is - **which is what the
list box was, and it was wrong**: its lines were spaced and cut off where the original
does neither.

- **A scrollbar** has a `size`, a range - `minValue`, `maxValue`, `startValue`,
  `stepSize` - a direction (`horizontal`) and four buttons declared inside it, which its
  `slider`, `track`, `leftbutton` and `rightbutton` keys name. *Ours:* a button at each
  end, the track stretched between them - a cornered tile keeps its edges - and the
  slider on the track where the value puts it. The slider is dragged and a click on the
  track brings it to the mouse. `likely` the original holds the four numbers in
  thousandths, so a step is never less than one.
  **An end button** was read on 2026-10-12, after the maintainer's account - "You can hold
  the mouse button for them to continue changing the value. Holding CTRL speed that up
  considerably" (`reversing/findings/FINDINGS-scrollbar.md`). `confirmed` unless said:
  - **a press** moves the value by the declared step, except that **a step of exactly a
    thousandth counts as ten** - the sliders' `0.001` moves 0.01 a press - **with Shift down
    ten times that**, 0.10, **with Ctrl a hundred times**, 1.00, and with both a thousand
    times, 10.00. The original asks the two keys by number: that the one is Ctrl is `likely`,
    the builder windows' tooltip naming the same number Ctrl, and that the other is Shift is
    the maintainer's word, 2026-10-12: "Its shift.";
  - **holding it** repeats the press. The first frame starts a clock; on each frame after,
    **the whole time held is counted out in 0.15 s, a press for each** - nothing for 0.15 s,
    then a press a frame, then two, and so on. So it goes faster the longer it is held,
    **and faster at a higher frame rate**: both the original's, kept. Letting go stops it;
  - *Ours:* a list box's bar moves a line a press, as the original's does, whose bar counts in
    lines where this one's runs from nought to one; the original's own step rule then applies
    to the line. **Which end of a bar that runs up and down adds is not settled**: a flag the
    original's scrollbar is given reverses its two buttons, and what sets it was not read -
    the sliders, across, take away on the left as here.
  **A bar that runs up and down has its `leftbutton`, and its least value, at the
  bottom.** `measured`: the list boxes' scrollbar gives its `leftbutton` the picture
  of an arrow pointing down and its `rightbutton` one pointing up, and in the original
  the arrow at the top points up (the maintainer, 2026-10-08, on seeing them the other
  way round here). So the file's sprites are not swapped, as this project's readme said
  for a long time; the bar is read from the bottom. A list is at its top when its bar
  is at its most.
- **A list box** has a `size`, a `spacing` and the name of a top-level scrollbar, **and
  lays what is put in it out in rows.** `confirmed`, read on 2026-10-09
  (`reversing/findings/FINDINGS-listbox.md`); until then this was drawn by the plain
  reading of the declaration, which was wrong in three ways.

      rows  = the box's height / the height of the line last put in, cut to a whole number
      shown = `rows` lines, from the one the scrollbar is at
      each goes one FIRST line's height under the one before, the first at the top

  - **A box shows a whole number of lines.** One with no row is not drawn at all.
    Nothing is cut off at the box's edge, top, bottom or side, and there is never half
    a line at the bottom.
  - **`spacing` plays no part.** Lines touch. The files are written to it: the build
    queue's box is 280 high and its lines 40, the needs' 176 and 44, the convoys' 132
    and 22 - seven, four and six lines exactly, each with `spacing = 2` beside it.
    `measured`
  - **Nothing but the box's height limits it.** A box higher than the frame drawn for it
    shows lines past the frame: `builders.md`, section 10, has the one that matters.
  - **Scrolling is by the line.** The scrollbar appears when there are more lines than
    rows and has a step for each line that has none.
  - The first line starts, both ways, at the first of `borderSize`'s two numbers.
    `likely`; every list drawn here gives nought.

  Two lists in the whole game go by their lines' own heights instead, with `spacing`
  between them: one made as the game is entered, which is not drawn here, and **the list
  a selected unit's panel is made of** (`unitpanel.md`), which is. In that mode each line
  goes straight under the one before it, **touching it**, and all are shown. *Measured*
  on the maintainer's screenshot of the original's unit panel, 2026-10-09, whose every
  entry begins on the row after the one before ends - though its list declares a
  `spacing` of 2, and the routine was read, as `likely`, as putting the spacing between
  them. The original scrolls such a list when its lines do not all fit; not built.

  *Ours:* the scrollbar stands along the box's right edge, as long as the box is high,
  and a notch of the wheel is a line.
  **The box takes no click itself; only what is in it does.** `inferred`, from what has
  to work in the original: the builders' `stats_pos` is one line in a box 533 by 620 that
  lies over the buttons that order and close, and those are pressed through it. Here
  the box had taken every click in its rectangle, and until 2026-10-09 none of those
  buttons could be pressed. *Ours:* the wheel scrolls the list the mouse is over,
  whatever of it the mouse is on, a line's own button included.
- **A box of overlapping elements** has a `size`, a `spacing` and a `format`. *Ours:* the
  windows put in it stand in a row from the end the format names, the spacing between
  them; when they are wider than the box each starts sooner by an equal amount, so
  that they overlap and the last still ends inside.

## 3. Where an element goes

- **A position is in whole units, measured from the point of the parent the element's
  `orientation` names; the element's upper left corner is that point plus the position.**
  `inferred` from the files, which fit it everywhere looked: a button `x = -25` from
  `UPPER_RIGHT` sits 25 in from the right edge, and a window of 1120 by 768 at
  `x = -555, y = -330` from `CENTER` sits about the middle of the screen. Not read out of
  the original.
- **There are seven orientations and the word is compared exactly.** `confirmed`
  `UPPER_LEFT`, `UPPER_RIGHT`, `LOWER_LEFT`, `LOWER_RIGHT`, `CENTER`, `CENTER_UP`,
  `CENTER_DOWN`. **Anything else - a misspelling, lower case - is the upper left.** The 58
  files use six of them 1922 times and never get one wrong. *Ours:* the same, and reported.
- **Nothing in the files is tied to a screen size.** `measured` 249 of those 1922
  orientations are something other than `UPPER_LEFT`, and the largest fixed window is 1120
  by 768.
- **`fullScreen = yes` makes a window the size of the screen, whatever `size` says.**
  `likely` The top bar declares 2048 by 100 and the original holds the screen's size for
  it.
- **An icon, a button and a checkbox have no size in the file: they are the size of their
  texture**, or of one frame of it. `measured` About 1,900 elements.
- **A text box is `maxWidth` by `maxHeight`.** `inferred`
- **Children are drawn in the order the file declares them.** `confirmed` The original
  keeps one list of a window's children in declaration order across all kinds.
- **The child a window names as its `background` stands at the window's corner, whatever
  position it declares.** It is always a button, usually one named `Background`. **A
  cornered-tile sprite fills the window, repeating; a plain picture keeps its own size and
  is not stretched.** A cornered tile that declares a `borderSize` keeps its corners and repeats
  its edges and its middle between them - the dialogs' 282-pixel art is three cells of 94, and
  repeated whole it leaves a seam of its transparent edge. Thirteen windows have one, the
  dialogs among them, and none was built before the leader window's confirmation on
  2026-10-11; `measured` on that screenshot alone. `measured` on the files and on the orders
  windows, 2026-10-09:
  - of the 41 windows of the base game that name a background, 26 have a cornered tile and
    15 a plain picture, and **not one of the 15 pictures is the size of its window** - the
    brigade view's is 400 by 632 in a window declared 1024 by 768;
  - `naval_unit_order` is declared 400 by 400 with a picture 302 by 502 placed at -8, -12.
    Drawn at the window's corner in its own size, every element of the window lands on the
    art: the rows of the list on its ruled lines, the two figures in their dark slots, and
    the two buttons with the same margin either side. Stretched to the window, or put at
    -8, -12, they do not.
  Until then this said the background fills the window and anything but a tile is
  stretched, which was `inferred` from running the original's main menu in an earlier
  iteration of this project, where every background is a tile.

*Ours - the scalable canvas.* A unit in a `.gui` file is one unit on a canvas 1920 by 1080,
which is scaled to the screen; an orientation becomes an anchor, so an element stays with
the corner or centre it named when the screen is a different shape. Nothing is rewritten
on disk. **Measured:** the top bar drawn at 1920 by 1080 and at 2560 by 1440 from the same
files, with nothing in it placed by hand.

## 4. Names are the contract

- **The game's code finds a window's elements by name.** `confirmed` Buttons, text boxes
  and icons are each asked for separately, by the name the file gave them, case ignored.
- **So a mod may move, restyle or re-skin anything, and may not rename it.** A screen that
  asks for a name its window does not have gets nothing. *Ours:* the log says which name,
  and the screen carries on without it.
- **Where two text boxes of one window share a name, the later is the one found.**
  `confirmed` For buttons and icons it is not established; *ours* finds the first.
- **A top-level element is found in one registry of every file's**, a window inside a
  window only through its parent. `confirmed`
- **A `positionType` is read by the code for its two numbers**, to place what the code
  builds itself: where a list of units starts and how far apart its rows are, for one.
  `confirmed`

## 5. Sprites, frames and flags

- **A texture named `.tga` is its `.dds` wherever one exists, and the `.tga` only where
  none does.** `confirmed` (`reversing/findings/FINDINGS-textureload.md`). The original
  swaps the first `.tga` in the name for `.dds`, asks whether that file exists, and opens
  it if so and the name as written if not. Four things follow:
  - *It goes one way.* A name written `.dds` is opened as written and never looked for
    as a `.tga`.
  - *It is over every layer at once.* `likely` A `.dds` in the base game is found before
    a `.tga` in an expansion or a mod is looked at, **so a mod's `.tga` does not replace
    a picture the base game holds as `.dds`.**
  - *It is lower case and the first found.* A name written `.TGA` is opened as written.
  - *Nothing else is known*, `.png` included.

  Their Finest Hour names 652 textures `.tga`; most exist only as `.dds`. Three exist as
  both and differ: `select_ship_button`, where the `.tga` is an older picture 60 wider
  that runs over the ship builder's frame - which is how this was found, the maintainer
  seeing the lines overflow here and not in the game - `divdesigner_listbutton2`, a row
  higher, and `gfx/test/factions`. `measured`

  *Ours:* a name no layer has under either extension is then tried with the others,
  `.png` among them, where the original reports the file missing and draws nothing.
- **A frame is the texture's width divided by `noOfFrames`.** `confirmed` as the
  declaration; so a sharper texture needs no change to any file. *Ours:* frames are worked
  in fractions of the texture throughout.
- **A button's frame number is counted from one.** `likely` The speed indicator has six
  frames and is set to 1 through 6; the bar behind the date has two and is set to 1 or 2;
  a technology folder's button has two and is set to 2 when chosen and 1 when not. What
  0 does is not known; *ours* draws the nearest frame that exists.
- **An icon's may be counted from nought.** `likely` The bar that shows a theory or a
  practical has 21 frames, the first empty, and is set from 0 to 20
  (`technologyscreen.md`, section 5). *Ours:* that one icon is drawn so; nothing else
  sets an icon's frame from code yet.
- **A `progressbartype` is a bar that fills from the left.** `inferred` from the
  declarations: it has a `size` and two textures. *Ours:* the first texture is drawn
  over the part that is filled and the second over the rest, each cut at the same place,
  and an icon whose sprite is one takes the bar's size and is set in hundredths.
  **Both pictures are drawn upside down.** *Measured*, 2026-10-09, on the maintainer's
  screenshots of a battle: the plate under a unit of the battle window
  (`GFX_combat_unit_bg`) has its light band over its dark one, where the picture has the
  dark over the light; and the unit panel's two standing bars are bright on the left,
  which a quarter turn of their pictures as they are would put on the right. Until then
  one of the panel's two bars was drawn the wrong way round and nobody had seen it. Not
  read.
- **A sprite can be chosen by a name the code makes up** - `icon_` and a category,
  `GFX_` and a strategic resource. `confirmed` Where there is no sprite of that name
  the icon keeps the one its file gave it, and nothing is said.
- **Hover, press and disabling do not change the frame; a shader does it.** `confirmed`,
  from `gfx/FX/buttonstate.fx`, which is plain text in the install and names four
  techniques. `up` draws the picture as it is. `over` adds a tenth to red, green and
  blue. `down` takes a tenth off. **`disable` draws it in grey**: each pixel's red, green
  and blue become their sum weighted 0.212671, 0.715160 and 0.072169. Transparency is
  untouched by all four. The same shader is written again for this engine, and a
  scrollbar's slider is drawn with it too - which is why a locked slider is grey, as
  the maintainer said it should be on 2026-10-08. *Ours:* every button is drawn so,
  whether or not its sprite names that effect file; what a sprite with no effect file
  does under the mouse was not looked for.
- **A `maskedShieldType` draws a country's flag** between two textures: one over it, and a
  mask. **The flag is as see-through at each point as the mask is there** - `confirmed`,
  from `gfx/FX/maskedflag.fx`, which is plain text in the install: the flag's colour with
  the mask's alpha. *Ours:* the flag is squeezed to the mask's size, cut by it, put in the
  middle of the overlay and the overlay blended on top, once for each country and sprite.
  The thin shield's mask and the top bar flag's are solid and their overlay's size, so
  there it comes to the flag under its frame. **The small round shield's is a disc**, 40 by
  26 under an overlay of 32 by 32: this said until 2026-10-09 that every mask shipped was
  blank and applied none, and the battle row of the unit panel showed a square flag behind
  a round frame. That the flag takes the mask's size and sits in the overlay's middle is
  *measured* on the maintainer's screenshot of that row, not read.
- **`allwaystransparent = yes` lets clicks through.** `inferred` from the name. *Ours:* any
  other icon takes the click, so the map under the bar does not; the original tests the
  pixel (`transparencecheck`), and this does not.
- **An icon lying on a button does not keep a click from it.** The maintainer,
  2026-10-09: "The original allows clicks on the icon" - the counter on a line of a
  builder's list, whose sprite has `transparencecheck` and is solid where it was
  clicked. *Ours* for how: the icon hands the press, the release and the mouse's being
  over it to the uppermost button of its own window drawn under it. The last section,
  item 13.
- **An icon can itself be what is clicked.** An alert is an `iconType` and nothing else,
  and the original hooks its handler to the icon as it would to a button
  (`reversing/findings/FINDINGS-deploymenu.md`, section 1). `likely`. *Ours:* an icon with
  no button under it says so when the mouse goes down and comes up on it.

## 6. Text

- **A number is written with as many decimals as asked for, cut off and not rounded, and
  with nothing between the thousands.** `confirmed` 115.999 with none is `115`.
- **A section sign and a letter change the colour of what follows and draw nothing.**
  `confirmed` In the game's encoding the sign is the byte A7. The top bar uses `R`, `G`
  and `Y`.
- **Which colour each letter is, is in the files.** `confirmed`, and found late: the
  escape's letter is looked up in a table the font carries, which its `bitmapfont`
  declaration fills from a `colorcodes` block, and only a font with none falls back to
  a table of the game's own. Seven fonts of `core.gfx` have the block, all alike:
  `W` 255 255 255, `B` 0 0 255, `G` 0 255 0, `R` 255 50 50, `r` 160 50 50, `b` 0 0 0,
  `g` 176 176 176, `Y` 255 189 0. A section sign and `!` goes back to the font's own
  colour. **Ours are not those**: red, green and yellow were chosen by eye before this
  was known - the red is the same to a point, the green darker, the yellow lighter -
  and the maintainer said on 2026-10-08 that they stay. They are fixed in the code, so
  a mod's `colorcodes` changes nothing here. Told where the original keeps them, the
  maintainer said the same day that it can stay so for now: `docs/compatibility.md`,
  entry 5.
- **Text before any escape is drawn in the font's own colour.** `inferred` A `bitmapfont`
  declares one.
- **A text box does not place its text; its font does**, given the box, the border, the
  format and the orientation. `confirmed`, from
  `reversing/findings/FINDINGS-textdraw.md`, read after the maintainer twice pointed at
  a figure off-centre in the circle painted for it. For a box measured from the upper
  left, which 1077 of the 1118 are:

  | set | the line starts, from the box's left edge |
  | --- | --- |
  | left | at the border |
  | centre | at the border, plus half the box's width, less half the line's - each half cut to a whole number |
  | right | where it ends at the box's right edge. **The border plays no part** |

  The line's top is the box's top plus the border's height. A line's width is the sum
  of how far the pen moves for each letter. A box measured from the upper right has
  the border added once more whichever way it is set. The other five orientations have
  rules of their own that were not all worked through; *ours* treats them as the upper
  left.
- **Every glyph is then drawn one unit right and one up of that.** The original adds
  half a pixel to both coordinates of every corner of a glyph, in a space whose second
  axis points up (`confirmed`), and beside its pictures that comes out as a whole pixel
  each way (`measured`, on a screenshot of the running game the maintainer sent: the
  difficulty's `1` and the year beside it are each one right and one up of the
  arithmetic). *Ours:* all text is drawn so. With it the figure's stem is in the same
  two columns as the original's and its top and bottom on the same rows; what differs
  is the letter, which here has a foot the original's has not.
- **Where a line breaks was not read.** *Ours:* a line too wide for its box is broken at
  its spaces - but only in a box high enough for a second line, so that a figure a
  little wider than a one-line box is not pushed out of sight. A colour that is on
  when a line breaks is on for the next.
- **A font is a texture of glyphs and a descriptor**, `gfx/fonts/<name>.fnt` and `.tga`.
  `confirmed` The original reads the descriptor's numbers by position and never its key
  names, and a descriptor that says it has a page and does not name it is normal.
- **Every descriptor names the typeface its glyphs were made from.** `measured` All 35
  in the install open with a line the original never reads: `info face="Arial" size=14
  bold=0 italic=0`, and after it the height of a line and of its baseline. Arial in
  ten of them, Garamond in seven, Trebuchet MS, Franklin Gothic Demi Cond, Impact,
  Tahoma and a few display faces in the rest.

*Ours, and a decision (2026-10-08): the glyph textures are not drawn.* They are a dozen
pixels high and cannot be scaled into anything readable, which is the one thing the
scalable canvas of section 3 could not survive. So:

- **A font is named as the files name it and drawn from its typeface, as outlines.**
  Nothing about a `.gui` or a `.gfx` file changes.
- **Where the typeface comes from**, in order: a `.ttf` or `.otf` of the font's name
  beside its descriptor, in any layer - how a mod supplies one, with a file the original
  ignores; else the typeface shipped in `fonts/` that stands for the one the descriptor
  names. **Nothing is taken from the machine the game runs on**, and a character a
  typeface lacks is left out and not borrowed from an installed font: text is the same
  everywhere.
- **What stands for what.** The original's typefaces are commercial and cannot be passed
  on; these are open, under the SIL Open Font License, and chosen by eye:

  | the descriptor names | drawn in | same widths |
  | --- | --- | --- |
  | Arial | Liberation Sans | yes: it was drawn to them |
  | Tahoma | Liberation Sans | no |
  | Courier | Liberation Mono | no |
  | Trebuchet MS | Fira Sans | no |
  | Franklin Gothic Demi Cond | Barlow Condensed SemiBold | no |
  | Garamond, Adobe Garamond Pro | EB Garamond | no |
  | Impact | Anton | no |
  | anything else | Liberation Sans, with a line in the log | no |

  "Anything else" is six display faces in the install: Eurostile, KyrillaSansSerif,
  Another Typewriter, Olde English, SV Basic Manual and urania_czech.
- **The size is whatever makes the text as wide as the original's glyphs made it.** The
  descriptor gives how far the pen moves for each character; the letters and digits it
  has are measured in both, and the typeface is drawn at the size where the two agree.
  For a typeface of the same widths that is the original's size, and text fits the boxes
  the files give it. The descriptor's own `size` is not used: two copies of one font in the wild give
  12 and 15 for identical glyphs.
- **A typeface of other widths is sized by the height of a line instead, and narrowed to
  the original's width** where it comes out wider, to no less than 0.7 of itself. Its
  letters are a different shape, so matching their width alone would make it the wrong
  height. That is every shipped typeface but Liberation Sans, and any a mod supplies.
- **The baseline and the line's height are the descriptor's**, so text stands where it
  stood.
- **The colour is the declaration's times the colour the glyphs were painted in.** Some
  glyph textures are white and take their colour from the `bitmapfont` entry; some
  carry it - the top bar's button font is dark grey in its texture and has no entry.
  The texture is read for its average colour and for nothing else.
- **Measured:** the top bar at 1920 by 1080 and at 2560 by 1440, every figure where the
  glyph textures put it and sharp at both, in Liberation Sans, Fira Sans and Barlow
  Condensed, with no font of the machine's used.
- The adjustments between particular pairs of letters that a descriptor lists are not
  read; the typeface has its own.
- **A text box set to the text it already shows does nothing.** `confirmed` So a screen
  may set every box every frame.
- **`format` is `left`, `centre` or `right`**, and `center` is written 51 times and works.
  `measured` *Ours:* the text is set against that side of the box, inside `borderSize`.
  How the original uses `borderSize`, and where it breaks a line, are not read.

## 7. Ours

- **Read every start, not cached.** Reading and checking the 58 files and the 1,502
  sprites they name takes about 70 ms, so there is nothing to cache. Textures are decoded
  when a window that shows them is made.
- **The declarations and the figures are in the libraries, with no engine in them.**
  `OpenHOI3.Content` reads the files; `OpenHOI3.Interface` works out where an element goes
  and what a screen's text says; the client only draws. That is what puts a wrong format
  or a wrong colour in reach of a test.
- **An icon or a button is as many units as its texture has pixels.** That is what the
  original's art needs, and it is the one thing that must change before a sharper texture
  can be dropped in: a file with twice the pixels would draw twice the size. Frames are
  already fractions of the texture; what is missing is something that says the size a
  texture was drawn for, and that is not decided (`readme.md`, *The interface*).
- **Kinds that are not drawn are still read** - a 3D button's - so a window's
  children are all there in their order and their names are known. A window counts how
  many it could not draw.
- **An edit box is shown as the box of text it holds**, found by its name like any text
  box, with the `size` it declares for room; typing into it is not built. Its text starts
  **further in than a text box's**: one less than a second border across, and one more
  than a second border down. *Measured* on the same screenshot, from the one edit box
  drawn so far - the unit's name, whose box has a border of 8 by 4 and its text 15 across
  and 9 down from its corner. The routine that draws an edit box was not read, so that
  one case is all the rule there is.
- **An icon's `rotation` turns it about the middle of its lower left pixel, and a positive
  quarter stands it on its end, running upwards.** So an icon `w` long and `h` high at
  `(x, y)`, turned a quarter, covers `x - h + 1` to `x + 1` across and `y + h - w` to
  `y + h` down. *Measured*, 2026-10-09, on a screenshot the maintainer gave of the
  original's unit panel beside this one's: the bars of 24 by 8 fall on the original's to
  the pixel in the header and in a brigade's row. Two guesses came first and he saw both
  wrong - about the upper left corner, which stood the bars 8 too high, and about the
  middle of the near end, which stood them 7 too far right; the second was "confirmed" by
  the bars then standing in the exact middle of a place in the row's picture, which the
  original shares between them and the mark of a reserve. Not read, and known for a
  quarter turn only. No screen built before the unit panel gives a rotation other than
  nought.
- **A flag turns about its own middle instead**, a pixel and a half to the right of it. The
  battle window's two shields are 66 by 51 and turned a negative quarter, and stand 51 wide
  and 66 high: one at `(x, y)` covers `x + 9` to `x + 60` across and `y - 8` to `y + 58`
  down, with what was its left edge at the top. *Measured*, 2026-10-09, on the
  maintainer's screenshot of that window; the bar's rule would have stood them 55 further
  left, over the picture between them. Not read, and known for those two only.
- **A line too wide for a text box that says `fixedsize = yes` is cut, and ends in three
  dots.** *Measured* on the same screenshot, where a division's name ends "CCN ..." in the
  middle of a word. *Ours:* as many letters are kept as leave the dots room in the box's
  `maxWidth`; the original's name ends ten pixels short of that, so it measures the room
  some other way, which was not read. A line with a colour in it is left alone.
- **A window whose file says `moveable = 1` is dragged with the left button held anywhere
  on it but on a button, a scrollbar or a list** - a picture or a text lying over a button
  counts as the button - and a window that is the screen is never dragged. The maintainer's
  account of the original's event window, 2026-10-12, and **no part of the window may leave
  the screen** - his too, the same day. That the rest of the original's moveable windows drag
  the same way was not read. *Ours*: the key is what decides, so the orders windows and the
  dialogs drag too; the window's extent is the `size` its file declares, so a background
  picture larger than that may still reach past the edge; and a window larger than the
  screen keeps its upper left corner on it.
- **A window can be made many times over.** A line of a list, an entry of the technology
  tree and a slider are each a top-level window type the code makes one of for each
  thing it has to show, and puts where it wants it.
- **A caption is one line in the middle of its button**, in the button's font. Captions
  and text boxes both draw their own glyphs: a text box because its colour changes in
  the middle of a line, and both because a typeface of other widths is narrowed as it is
  drawn.
- **The typefaces are in `fonts/`, with their licences**, and `fonts/readme.md` says where
  each came from. Which stands for which is one table in the client.
- **A button's shortcut is read** for function keys, `ESCAPE`, `ENTER` and single letters
  and digits. The other words the files use are not yet keys.
- **A checkbox is a button that a click ticks or clears**, showing the second frame of
  its sprite while ticked and the first while not. That the frames are those was not
  read; a look at the original would settle it.
- **A button says whether the mouse is held down on it**, which is what lets a screen
  repeat a step while it is (`builders.md`, section 6), and can be given another sprite
  by name.
- **A button may answer the right mouse button too**, going down and coming up on it - and
  takes it only where a screen asks, so a right click elsewhere still goes on to what lies
  under it. The original's observer of a button has nine events and of a checkbox six; which
  is which was not read, and the leader window's two that are taken for the right button
  were inferred from where they sit - the maintainer confirmed both on 2026-10-11
  (`leaders.md`, section 12).

## What is not established, in one place

1. **The table of colours a font with no `colorcodes` falls back to**: who fills it, and
   with what.
2. **How a text box lays its text out**, beyond section 6: where a line breaks, how a
   `fixedsize` box measures a line it cuts (section 7), what the two kinds of text box
   differ in, the five orientations
   other than the upper left and right, and why the pictures sit half a pixel the other
   way from the text.
3. **Where the position is measured to** - that it is the upper left corner is a fit on
   the files - and how `rotation` and `scale` turn and size an icon. A quarter turn is applied
   as section 7 says - one way for a bar and another for a flag - and **where an edit box
   puts its text** likewise: all measured on screenshots of the original, none read. Nor is
   why a bar's picture is drawn upside down (section 5).
4. **The keys each kind takes, other than a window's.**
5. **Which of two buttons or icons of one name is found.**
6. **What frame 0 draws**, and whether an icon's frames are counted from nought.
7. **Which buttons `buttonstate.fx` is for.** The shader is read (section 5); whether a
   sprite that names no effect file changes under the mouse was not looked for.
8. **Where a `maskedShieldType` puts its flag** when the mask is not the overlay's size:
   in the overlay's middle here, which fits the one case there is (section 5).
9. **What `transparencecheck` tests**, and against which texture.
10. **Tooltips** are read and built (2026-10-11): `tooltips.md` is the spec, and says what of
    them is open. `pdx_tooltip` and the three tooltip keys of a button are still read past -
    no file of the original's or of BlackICE's gives any of them a value.
11. **How a scrollbar and a box of overlapping elements lay themselves out.** Both are
    drawn by the plain reading of their declarations (section 2). The list box was too,
    until it was read. Of the list box there is left: where its scrollbar stands, how far
    a notch of the wheel moves it, and how a list that goes by its lines' heights scrolls.
12. **Which frame a ticked checkbox shows.** The second here (section 7), unread.
13. **Which element takes a click.** Nothing of how the original hands the mouse to an
    element was read; two things are known from the maintainer's eye. A list box does
    not keep a click from what it lies over (section 2), and **an icon does not keep
    one from a button under it** - a click on the counter of a line of a list picks the
    line (2026-10-09). Here a button, a scrollbar and any icon not `allwaystransparent`
    take a click, the uppermost first, and a window, a text box and a list box do not;
    an icon hands what the mouse does to it on to the uppermost button of its own
    window drawn under it, and keeps it from the map either way. That it is only a
    button of the same window, and that an icon still keeps a click from an icon, a
    scrollbar or a button of another window beneath it, is *ours*.
