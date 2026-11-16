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
| `editBoxType` | 21 | text the player types | shown; typed in where a screen makes it editable (section 8) |
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

**A button on a text sprite with no font draws nothing at all, and is never under the mouse.** `confirmed` for the
drawing, `inferred` for the mouse (read 2026-11-16, `reversing/findings/FINDINGS-guistatic.md`, section 6). A text
sprite's picture is the texture its text is rendered into. A button whose text or font is empty makes its picture from
the sprite alone, and passes its `buttonFont` as the font to render it with; an empty one renders nothing, so the object
has no texture, draws nothing, and its shown size stays nought, which is what the mouse is tested against. Setting the
button's text afterwards renders only with a font, and the button's is none. In the base game and Their Finest Hour no
button is so (309 on text sprites, every one with a font); **BlackICE's `faction_symbol_big_button` is the one**, its
`GFX_sort_axis` a `textSpriteType` and its `buttonFont = ""` - "effectively disables it without a trace", its file says -
so its three faction sort buttons on the diplomacy screen, which it moves over the country list's columns, are not
there in the original, and a click there reaches the row. *Ours:* `CButtonCaption.DrawsPicture`, by the type's font;
what the original does where code gives such a button a sprite of another kind or a font later was not read.

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

**A `format` in quotes is not taken, and the one before it stands.** *Measured*, not read: the
reorganise row's `subunit_strength` gives `format = left` and then `format = "right"` - the only
two quoted formats in the base game - and on the maintainer's screenshot of the original's window
its men stand to the left, just after the box's corner, where the second would have put them past
the row's end (`reorganise.md`, section 2). The original's reading of the key was not opened; a
format word is taken to be matched as a word, which a quoted string is not.

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
  - **A line wider than the box is drawn whole, past its edge** - and past its window's: nothing in the original's
    interface clips one object to another (read 2026-11-15, `reversing/findings/FINDINGS-listbox.md`, section 4).
    Its draw list can scissor an object, but the rectangle it would cut to is only ever zeroed and no object is
    ever flagged for it; a list box hides and shows whole lines and makes no picture of its own; and an
    `instantTextBoxType` draws every glyph it has laid out with no test against any rectangle (section 6). The one
    cut there is is a `textBoxType`'s own picture (section 6), which is the box's and not its list's. So the
    technology panel's effect lines, laid out to 330 less an `O`, stand up to about 11 units past their 310-wide
    list (`technologyscreen.md`, section 7). `inferred` as a whole: every direct store to the flag is accounted
    for, and one through a computed pointer cannot be ruled out by a scan.
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
- **A window draws its background, then the children its file declares in the order it declares them, all kinds
  together - child windows among them - and then whatever code attaches to it, in the order attached, each over
  everything before it.** `confirmed` (read 2026-11-16, `reversing/findings/FINDINGS-guistatic.md`, section 6). What
  draws is a tree of drawn objects, each window's under its own node, walked a parent before its children and children
  first to last; attaching an object puts it last among its parent's children, and attaching it again moves it to the
  end. The window's constructor attaches its background and then each declared child, in declared order; a window or
  element code attaches later (`AttachChild`, `AttachSubWindow` - the diplomacy screen's faction sort windows and faction
  boxes) goes after all of them. The original's two lists of children - declared elements and attached ones - are what
  it finds and shows children by, not the draw order. A list box's rows and then its scrollbar are drawn under the
  box's own node, so where the box was declared, the scrollbar over the rows; bringing a child to the front, as the
  diplomacy triangle does with the chosen country's marker, attaches it again. A window code makes by name with no
  parent is a window of its own over every window made before it. `click_to_front`, which no file of the base game, Their
  Finest Hour or BlackICE sets, only gives the background button a handler. **Nothing is cut at a window's edge**
  (`reversing/findings/FINDINGS-listbox.md`, section 4), so BlackICE's diplomacy picture, 1280 wide at -85 in a window
  of 1120, is drawn whole. *Ours:* Godot draws a control's children in order, so a window built from its file and then
  given children by a screen is already the original's. Until 2026-11-16 this said the original kept one list of
  children; that gave the same order for what a file declares and was wrong as a reason.
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
- **A file found is read by what it holds, not by its name.** `likely`: the original
  loads its textures through D3DX, which goes by a file's content, and draws every picture
  of BlackICE's below - kinds Their Finest Hour has none of but two small odd-sized DXT3s,
  so they were not met before BlackICE was played (2026-11-16). BlackICE names 11 TGAs `.dds` - the SS small counters
  and one leader counter - and three JPEGs `.tga`. So `CTextureSniffer`
  (`lib/OpenHOI3.Content/Textures`) tells a DDS, a PNG, a JPEG and a BMP by the mark each
  begins with, and a TGA, which has none, by a header whose every field a TGA can have;
  anything else is reported and drawn as nothing. **Which decoder reads it is then decided
  from its header, before any is asked**, so that Godot's own is never handed a file it
  would refuse or misread and has nothing to warn of (`CTextureManager`):
  - *Godot's own*, as it always was, for a DDS in DXT1, DXT3 or DXT5 of whole 4 by 4
    blocks or of 24 or 32 bits a pixel, a TGA, a PNG, a JPEG and a BMP - with the three
    things done to a DDS before or after it that the map needed: a DXT1 with see-through
    texels decoded here (`mapdraw.md`, section 6), a DDS whose chain of smaller copies is
    short read for its full size alone, and a compressed picture whose size is not a power
    of two set to its nearest colours of five, six and five bits (`CRecompression`; ibid.).
    Every texture of Their Finest Hour's but two goes this way, and comes out as before.
  - *Ours*, `OpenHOI3.Content.Textures`, the image handed to Godot whole and its smaller
    copies made again where the file had a chain:
    - **an uncompressed DDS of 8 or 16 bits a pixel** (`CMaskedPixels`), by the masks its
      pixel format gives and never a guess at the layout - BlackICE's 995 counters and
      icons are A1R5G5B5 and its 16 cut-unit counters A4R4G4B4, and 565, X1R5G5B5 and
      X4R4G4B4 are read the same way. A channel of fewer than eight bits is widened by
      repeating its bits from the top, as Direct3D widens one; grey comes from the red
      mask, a format of alpha alone is black, and alpha comes from its mask only where the
      format says it has alpha, so an X format is solid. Godot read the 1555 files short,
      "Reading less data than requested", and the 4444 ones itself;
    - **an 8-bit palette** - BlackICE's `intel_tab_ministers` and `intel_tab_production`:
      256 entries after the header, red, green, blue and a fourth byte taken for alpha, as
      Direct3D 9 takes a palette entry's flags; the one entry of each of the two with a
      nought there is white and clear;
    - **a DXT1 to DXT5 picture whose width or height is not a whole number of blocks**
      (`CDxtBlocks`) - 32 of BlackICE's: 23 DXT1s, every one with see-through texels and
      so decoded here already, and 9 DXT3s, `selected_brigade_red1` (291 by 27) and
      `cgm_diplo_bg` (431 by 250) among them, two of them Their Finest Hour's own
      `combat_divider` (2 by 128) and `tech_level_circle` (1 by 1). The header's linear
      size is right for the real size; Godot works it out otherwise, refused five of the
      nine and warned over the other four. Decoded block by block, the texels past the edge left out, rather than the
      header widened to the next whole block and the picture cropped back: that would
      still hand Godot a file outside the format's rule and a crop of a compressed image
      unpacks it anyway, where a decoder of our own is a few lines beside the see-through
      DXT1's, takes nothing from Godot and is tested as plain .NET. A colour is widened
      and the thirds worked as for the see-through DXT1; DXT3's and DXT5's colour blocks
      always have four colours. One whose size is not a power of two is then set to its
      nearest colours as Godot's are - the same reasoning, D3DX compressing it afresh;
    - **a colour-mapped TGA whose map is not of 24 bits** (`CTgaDecoder`) - BlackICE's
      portrait `L600780`, whose map is of 32, which Godot's loader refuses.
  `--texturecheck=folder` reads every `.dds`, `.tga` and `.png` under a folder this way and
  prints each that fails, with the reason, each that Godot logs anything over, and the
  counts; `--textureout=folder` beside it writes each picture read as a PNG to look at. On
  2026-11-16 BlackICE's 45,846 were read, where 22 had failed and 999 had Godot warn, and
  the install's 22,089 and Their Finest Hour's 374 as before, the two odd DXT3s with no
  warning now. *Inferred*, not measured: that our thirds give the GPU's colours to the
  unit in the two Their Finest Hour DXT3s, which Godot left compressed for the GPU - 27 of
  `combat_divider`'s 256 texels are thirds.
- **A frame is the texture's width divided by `noOfFrames`.** `confirmed` as the
  declaration; so a sharper texture needs no change to any file. *Ours:* frames are worked
  in fractions of the texture throughout.
- **A button's frame number is counted from one.** `likely` The speed indicator has six
  frames and is set to 1 through 6; the bar behind the date has two and is set to 1 or 2;
  a technology folder's button has two and is set to 2 when chosen and 1 when not. What
  0 does is not known; *ours* draws the nearest frame that exists.
- **An icon's may be counted from nought.** `likely` The bar that shows a theory or a
  practical has 21 frames, the first empty, and is set from 0 to 20
  (`technologyscreen.md`, section 5); the Strategic Effects window's strip has 17 and is
  set from a modifier's `icon`, which the files give from 1 to 16 (`strategiceffects.md`,
  section 3). *Ours:* those two are drawn so; other icons' frames count from one.
- **A `progressbartype` is a bar that fills from the left**, drawn since 2026-11-07
  through the effect its sprite names - `progress.fx` - translated for the canvas
  (`reversing/findings/FINDINGS-progressfx.md`), `confirmed`: `ColorProgress` where both
  its pictures load, the first over the part filled and the second over the rest, and
  `tec0` where either does not, its `color` and `colortwo` instead; the split where u
  reaches `CurrentState`, the share in hundredths over 100. An icon whose sprite is one
  takes the bar's `size` and is set in hundredths, capped at 100. A colour is read as the
  original reads one: each of r, g and b over one divided by 255 on its own, the alpha
  one unless a fourth number is written, and then taken as written - so `org_bar`'s
  `{ 0 0 0 0 }` is clear. `horizontal = no` turns u up the bar, filling it from its foot.
  **Both pictures are drawn upside down**, and the reading says why: the original's quad
  has v nought along its foot (`likely` - the caller that makes its matrix was not read).
  *Measured* first, 2026-10-09, on the maintainer's screenshots of a battle: the plate
  under a unit of the battle window (`GFX_combat_unit_bg`) has its light band over its
  dark one, where the picture has the dark over the light; and the unit panel's two
  standing bars are bright on the left, which a quarter turn of their pictures as they
  are would put on the right. The effect drawn on the quad gives those screenshots pixel
  for pixel as the measured flip did. *Ours:* the share is not scaled by the part of its
  texture the second picture fills - a texture here is the picture alone, where the
  original's is padded to a power of two - and a screen's party colour on an
  organisation bar is set as the effect's first colour. The original's `ScrollTech` is a
  `scrollingSprite`'s, never a bar's.
- **A sprite can be chosen by a name the code makes up** - `icon_` and a category,
  `GFX_` and a strategic resource. `confirmed` Where there is no sprite of that name
  the icon keeps the one its file gave it, and nothing is said.
- **A picture code gives an icon from a file - a country's skin, a portrait, a terrain or technology picture - is
  drawn in its sprite's box, not its own.** `confirmed` (`reversing/findings/FINDINGS-guistatic.md`, section 6; the
  picture sprite, `PictureSprite_Render`). The original makes a picture sprite over the icon's sprite with the file as
  its texture, and draws it with the sprite's quad: the sprite's own file size, a frame of it, times the scale, and the
  sprite's texture coordinates - its file's size over the next power of two - laid on the picture, which the texture
  handler pads to its own power of two with nothing in the padding. A picture the size of its sprite's file fills the
  box exactly, which every country skin and top bar of the base game is (1024 by 680 and 1024 by 80, the generic ones
  too). One of another size is stretched or cut: BlackICE's skin sprite, `country_bg_generic`, is 1280 by 736, the base
  game's German skin 1024 by 680, so the German skin is drawn 1280 across from its first 640 columns, 680 rows down, and
  the 56 rows below are the padding - the whole width of BlackICE's diplomacy and politics screens behind their right
  column, where the picture at its own size stopped at 939. *Ours:* `CPictureSprite` and `CGuiIcon.ShowPicture`, used
  for the country screens', the ledger's and the top bar's skins; the portraits, the terrain, tactic, ship and
  technology pictures go through the same object in the original and still through `CGuiIcon.SetTexture` here, which
  takes the picture's own size - right wherever a picture is its sprite's size, not checked where it is not.
- **Hover, press and disabling do not change the frame; a shader does it.** `confirmed`,
  from `gfx/FX/buttonstate.fx`, which is plain text in the install and names four
  techniques. `up` draws the picture as it is. `over` adds a tenth to red, green and
  blue. `down` takes a tenth off. **`disable` draws it in grey**: each pixel's red, green
  and blue become their sum weighted 0.212671, 0.715160 and 0.072169. Transparency is
  untouched by all four. **Since 2026-11-06 the install's own file is drawn**, translated
  for the canvas (`shaders.md`, section 3.10), so a mod's `buttonstate.fx` is the one
  that counts - it was written again by hand for this engine until then - and a
  scrollbar's slider and bar are drawn with it too - which is why a locked slider is grey, as
  the maintainer said it should be on 2026-10-08. **A sprite is drawn through the effect
  file it names, and one that names none through `buttonstate.fx`** - the original's
  sprite type starts with that file before its declaration is read
  (`reversing/findings/FINDINGS-buttonstatefx.md`), `confirmed` - so a sprite with no
  effect file changes under the mouse as one naming it does. Since 2026-11-07 each
  element's material is its own sprite's file's: the base game's one other is
  `interpolated_buttonstate.fx`, the same four techniques with a linear sampler, on
  `GFX_empty_position`, the box a portrait is drawn into. The technique is the element's
  state's by name - a button's normal, pressed, hovered and disabled `up`, `down`, `over`
  and `disable`, an icon's state one more than its own - and `Color`, white unless a
  screen tints the sprite, multiplies every technique. **Open since that reading**: the
  original's draw takes a sheet's frame from the state - `up` the first, `down` the
  second, `over` the third, `disable` the fourth, each modulo the frames - wherever no
  frame has been set, and whether code sets one on the 88 multi-frame buttons whose file
  gives none - the map mode buttons among them - was not read; if nothing does, the
  sentence above is wrong for them. **The technology screen's folder tabs are settled**:
  the screen sets each tab's frame outright on every pass, 2 for the chosen folder and 1
  for the rest, and a frame set so overrides the state's, so a tab keeps its picture under
  the mouse - which is how BlackICE's avionics tab switches when chosen (the maintainer's
  pointer, 2026-11-07; `reversing/findings/FINDINGS-buttonstatefx.md`, *Not
  established*; `technologyscreen.md`, section 3). The maintainer knows of no button
  that changes its picture on hover. *Ours:* an icon is drawn plainly until a screen
  gives it a look, which `up` with a white `Color` comes to; the effect's `Point`
  sampler is the element's own filter, as for every drawn texture (`shaders.md`, section
  3.10); and the original's half-pixel shift of the quad, Direct3D 9's, is left out.
  **An icon can be given a look too**: the
  Strategic Effects window draws a row's background and strip in `up` or `disable` by
  whether the country has the modifier (`strategiceffects.md`, section 3). The routine
  sets the states 1 and 4, which are `up` and `disable` - `confirmed` since the reading
  of 2026-11-07, the interface choosing a technique by the state's name.
- **A `maskedShieldType` draws a country's flag** between two textures: one over it, and a
  mask. **The flag is as see-through at each point as the mask is there** - `confirmed`,
  from `gfx/FX/maskedflag.fx`, which is plain text in the install: the flag's colour with
  the mask's alpha. **Since 2026-11-07 a shield is drawn as the original draws it**
  (`reversing/findings/FINDINGS-maskedflag.md`), `confirmed`: the flag through the effect
  the sprite's `effectFile` names - `maskedflag.fx`, and `maskedflag_greydown.fx` for
  `GFX_minishield_greydown` - translated for the canvas, on a quad the mask's size in the
  middle of the overlay's box, its corners as the original's (an odd width or height puts
  the fourth half a pixel off), the flag's coordinates 0 to 1 and the mask's its second
  set; then the overlay over it, plainly. **A look changes the flag alone**: `tec0` up,
  `over`, `down` and `disable` - the overlay is the same in every look. The thin shield's
  mask and the top bar flag's are solid and their overlay's size, so there it comes to the
  flag under its frame. **The small round shield's is a disc**, 40 by 26 under an overlay of
  32 by 32: this said until 2026-10-09 that every mask shipped was blank and applied none,
  and the battle row of the unit panel showed a square flag behind a round frame. That the
  mask sits in the overlay's middle was *measured* on the maintainer's screenshot of that
  row and is now read. *Ours:* the original's flag is a cell of an atlas of every country's
  flag, `FlagCoords` its column and row; here it is the country's own picture with
  `FlagCoords` (1, 1, 0, 0), as the original draws a shield that has a picture of its own;
  and the mask's second set runs 0 to 1, a texture here being the picture alone where the
  original's is padded to a power of two. Each shield is a `CShieldView`, the flag its own
  material's, `CShields` making what a sprite's shields share once.
- **`allwaystransparent = yes` lets clicks through.** `inferred` from the name. *Ours:* any
  other icon takes the click, so the map under the bar does not.
- **`transparencecheck = yes` lets a click through a button wherever the pixel of the frame
  it shows has no alpha** (`reversing/findings/FINDINGS-transparencecheck.md`). The sprite's
  test is read whole: see-through where the alpha is nought, everywhere for
  `allwaystransparent`, nowhere without the key, the frame held to one to `noOfFrames`, and
  the point in the texture's own pixels from the frame's corner - `confirmed`. **A DXT1 or
  DXT5 texture is refused as it loads** - "Texture ... have forbidden compression, have you
  tried DXT3?" - and the sprite tested nowhere; DXT3 and uncompressed textures are tested.
  *Ours:* a compressed texture is unpacked before its alpha is read - until 2026-11-05 it was
  read compressed, every pixel an error of the engine's, and the leader window, whose rows'
  `select_leader_banner` is a DXT3 of 19,008 pixels, hung as it opened.
  That a click is what asks it is `inferred`: who calls the test was not found. The art bears
  it out - the top bar's `speed_indicator` is drawn only in columns 4 to 13 of each 32-wide
  frame and lies over the left ten pixels of `button_speedup`, which was pressed only on its
  right part until this was built. *Ours:* a button answers by its sprite's pixel and its
  shown frame, scaled from its size on screen to the texture's; an icon does not, and hands
  a click on to a button under it only where that button would take it. Whether a button
  whose sprite is `allwaystransparent` takes a click at all is not read, and here it does.
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

- **A text is held as its Windows-1252 bytes and drawn as Windows-1252 shows them**: the 27 characters of 0x80 to 0x9F -
  the curly quotes and apostrophe, the dashes, the euro, the ellipsis - are drawn and measured as themselves, where a
  typeface would find only controls (`CWindows1252.ToDisplay`, in the font's drawing alone). Until 2026-11-06 they were
  drawn as nothing - the start's description in the lobby lost its quotation marks and "Foch's" its apostrophe.
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
  the border added once more whichever way it is set. **A box measured from the centre**, set centred, starts at the border, plus half the box, less half the
  **widest** line, less half the line (2026-11-08, `reversing/findings/FINDINGS-textdraw.md`), so a single line ends at the middle of the box; set left or right it starts at the border. The other four
  orientations have rules of their own that were not all worked through; *ours* treats them as the upper left.
  **That is an `instantTextBoxType`'s rule, and the caption's of a button that names no sprite** (below). A
  `textBoxType` is placed by another routine of the font, which draws it into a picture of its own (read 2026-11-14,
  `reversing/findings/FINDINGS-textdraw.md`, section 2.2.1; `CTextBoxPlacement`) - and so are an edit box's line
  (section 8) and the caption of a button on a text sprite (below):
  - **A box fits where it says `fixedsize = yes`, or gives both a `maxWidth` and a `maxHeight`.** Then its picture is
    the box's size - 320 across where it gives no width - and

    | set | the line starts, from the box's left edge |
    | --- | --- |
    | left | at the border |
    | centre | at half the box's width less half the line's, each half cut to a whole number. **The border plays no part** |
    | right | one short of the border on the right: the box's width, less the border, less one, less the line |

  - **A box that does not fit** - no height and not of fixed size, 18 of the 341 in the base game and Their Finest
    Hour, the game options' `MustRestartTextBox` among them - is drawn into a picture as wide as its widest line and
    the border on both sides, and **every line starts at the border whatever its format**: a centred one is set left.
  - **The orientation plays no part in the text**: it places the box. So a box measured from the upper right does not
    add its border twice as an `instantTextBoxType` does - the production screen's IC figure and its title.
  - The first line's top is the border's height down, the next a line's height under it - **except in a box that fits
    and has a picture behind its text**, its `textureFile`, where the lines start half of what the box's height leaves
    them down, cut to a whole number towards nought, with no border in it: the console's `console_text`, 16 high in
    `garamond_16`, whose line is 16, starts at its top, where its border would put it 1 down.
  - **The picture behind the text is drawn into the same picture first, in nine parts** (read 2026-11-14,
    `reversing/findings/FINDINGS-textdraw.md`, section 2.2.2; `CTextBoxPicture`): cut by the box's `borderSize`, its
    four corners the border's size in the file and on the screen, its edges stretched along their length and its middle
    both ways - **stretched, not repeated**. It covers what the box shows from its upper left corner, **less two pixels
    across where the border across is not nought, and less two down where the border down is not**. So the tooltip's
    `tiles_dialog` ends two short of the box's right and bottom edges, and an edit box's picture two short of its own
    (section 8). Four `textBoxType`s of the base game and Their Finest Hour have a picture, the same four in BlackICE:
    `console_text` and `ChatMemberText`, whose `transparency.dds` is clear, and `ToolTip` and `FPS_Counter`, on
    `tiles_dialog.dds`.
  - **Only part of the picture is shown, and the text outside it is cut off**: in a box that fits, its width less the
    border on the right and its height less the border at the bottom - or where it gives no height, its lines and the
    border above and below; in a box that does not, all of it. So a `textBoxType`'s lines past its height are cut off,
    and a line too wide for its box loses its ends.
  - A box of fixed size that gives its width or height as nought, with a picture behind its text, takes that
    dimension from the picture, less the border once.

  *Ours:* the original's picture is drawn on screen one unit narrower and lower than the texels it shows, each way -
  a squeeze of a pixel over the box - which is not followed, nor is it known whether its pictures do the same. The
  original samples the picture behind the text nearest, a texel to a pixel; the canvas here is scaled, and it is
  sampled as the interface's other pictures are. A part that comes out of no size, the box narrower than its border
  twice, is left out.
- **A button's caption is laid out one of two ways** (read 2026-11-14, `reversing/findings/FINDINGS-textdraw.md`,
  section 1; `CButtonCaption`, `CGuiCaption`):
  - **On a button that names a sprite**, as a `textBoxType`'s text in the sprite's picture: of fixed size, its box the
    picture less the button's `borderSize` once, so the text is first cut to the lines the picture holds and ends
    " ..." where it holds fewer than it needs; then broken as a `textBoxType` is, **each line centred in the picture
    with no border** - whatever the button's `format` says - and **the lines centred up and down**, half of what the
    picture's height leaves them, cut to a whole number; what falls outside the picture less the border is cut off. A
    caption too wide for its button therefore no longer runs past it, as it did here before the reading. A plain `spriteType` draws no caption at all. A sheet of more than one frame is laid out at a quarter
    of its width, uncut (`docs/enhanced.md`, *Interface*); none has one.
  - **On a button that names none**, as an `instantTextBoxType` at the button's place: its `size` the box, 200 where it
    gives none, **no border**, its `format` and its orientation - the line's top at the button's. Two buttons of the
    base game are such, `bg_button` and `messagelogbutton`, both with blank words.

  No button of the base game or of BlackICE gives a `borderSize`, though the original's grammar takes one; it is read
  here for the caption.
- **Every glyph is then drawn one unit right and one up of that.** The original adds
  half a pixel to both coordinates of every corner of a glyph, in a space whose second
  axis points up (`confirmed`), and beside its pictures that comes out as a whole pixel
  each way (`measured`, on a screenshot of the running game the maintainer sent: the
  difficulty's `1` and the year beside it are each one right and one up of the
  arithmetic). *Ours:* all text is drawn so. With it the figure's stem is in the same
  two columns as the original's and its top and bottom on the same rows; what differs
  is the letter, which here has a foot the original's has not.
- **Where a line breaks** (read 2026-11-14, `reversing/findings/FINDINGS-textdraw.md`, sections
  1 and 2.1 to 2.4). **Every text box breaks its text at its `maxWidth`, whatever its height**,
  each line a line's height under the last and placed as the first is - set centre, each centred
  by its own width. **The two kinds of box are broken by two routines of the font, near alike**:
  - **An `instantTextBoxType`** - and the caption of a button with no sprite - is drawn straight to the screen
    (`CFontLineBreak`). Its width is 200 where its file gives none (the object that draws it sets
    that; no file of the base game has one), and its height plays no part, so its lines run on
    down past it - **and nothing is cut across either**: a line wider than the box, as a last word or a line laid
    out elsewhere can be, is drawn to its last letter, past the box and whatever holds it (read 2026-11-15,
    `reversing/findings/FINDINGS-listbox.md`, section 4; section 2 above). A word is taken with the space after it: at each space, where the line, that
    word and its space and twice the border are **wider** than the box, the line breaks before
    the word, keeping its own trailing space - measured with it, so a centred line stands half a
    space left; a word over 50 that runs past the edge, once something has been laid out, is
    broken where it reaches it; the last word is tested without a space, and not at all where it
    alone is wider than the box; a newline ends a line; past 55 lines the rest is dropped.
  - **A `textBoxType`** - and the caption of a button on a text sprite - is drawn into a picture of its own (`CTextBoxLineBreak`), 320 wide where
    its file gives no width. The text is read a character at a time, and **before each one - a
    space among them - a line that has reached the box**, twice the border in, breaks before the
    word being gathered, the word so far beginning the next line: a word goes down at the
    character the line reaches the box, with no floor of 50 and no test of anything laid out. At a
    space, a line that the word and its space bring **to** the box (not only past it) breaks
    before the word - but never while nothing has been laid out. At the end the last word goes
    down where the line has reached the box, however wide the word. A newline ends a line; past
    55 lines the rest is dropped. A colour escape takes no room; a flag, a counter and a strength
    bar are as wide as their pictures and count for the line and not the word.
  - **A box that says `fixedsize = yes` is cut first** (`CFixedSizeCut`), and then broken: lines
    are counted a character at a time to the box's width less twice the border, and where they
    are more than its height less twice the border **across** holds, the text is cut before the
    character that started the line too many, **its last four characters made a space and three
    dots** - "Magdeburg" in the province view's list of towns is "Magd ...", where it was
    "Magdeb..." before the reading. An `instantTextBoxType` is cut
    whatever its height, a `textBoxType` only where it has one.
  - **A tooltip, a row of the message log, a line of the lobby's chat, a line of the diplomacy
    screen's status list, a row of the technology panel's effects and an edit box's text are laid
    out by the original's text layout** before their text reaches a box (`CTextLayoutLineBreak`;
    `reversing/findings/FINDINGS-editbox.md`, section 6; `reversing/findings/FINDINGS-textdraw.md`,
    section 2.3): a line is cut where it and one `O` are wider than the room - the tooltip's box
    less three borders, the log's button less 10, **the chat row's `messagelogtext` less 10**, 740,
    the status list's and the effects' text box's `maxWidth`, an edit box's `size` less twice its
    border - at the last space before the first character at which it reaches the room less the
    `O`, the space dropped, or, where the first word and an `O` do not fit, after that character,
    the word split. So a line standing whole may be the room less an `O` wide, and a line broken
    off is narrower. What that gives is never broken again by the box. A row of the chat, like a
    row of the log, is drawn alone, so a colour does not run on into the next row (2026-11-14; until
    then the chat was broken at its spaces to the row's whole width and a colour carried over).

  A first word too wide for the box, a space after it, leaves the first line empty in both
  kinds of box; in a `textBoxType` so does a first word that fills the box, and a word wider than
  the box goes on there a character to a line, the original's count of the word never set back
  after a break inside it - all kept (`docs/enhanced.md`, *Interface*). **A colour runs on over a
  line break and a newline** until the next escape, in a box and in a tooltip, the font keeping
  it; a row of the log or of a list does not take the last row's colour, each being drawn alone.
  *Ours:* the original's oddity at the end of an `instantTextBoxType`'s text - a last word wider
  than the box makes the whole line's width that word's for its placing - and the one-short width
  a `textBoxType` keeps for a line broken inside a word are not followed: a line is placed by its
  own measured width. **A `textBoxType`'s lines are cut off past its height less its border** and
  placed by a rule of their own, above (read and built 2026-11-14). **An edit box** shows one line
  of its layout, the caret's - the last where a screen set its text, which leaves the caret at its
  end - placed where section 8 measured it; **a button's caption** is laid out as the item above
  says; **the lobby's chat** as the message log is. All three since 2026-11-14. `¤`, which the
  original's `textBoxType` counts 16 wide, is a letter here.
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
  on; these are free to ship with the game - each one's licence is in `fonts/readme.md` -
  and chosen by eye:

  | the descriptor names | drawn in | same widths |
  | --- | --- | --- |
  | Arial | Liberation Sans | yes: it was drawn to them |
  | Tahoma | Liberation Sans | no |
  | Courier, Another Typewriter | Liberation Mono | no |
  | Trebuchet MS | Fira Sans | no |
  | Franklin Gothic Demi Cond | Barlow Condensed SemiBold | no |
  | Garamond, Adobe Garamond Pro | EB Garamond | no |
  | Impact | Anton | no |
  | Olde English (`axis_country`) | Breitkopf Fraktur | no |
  | KyrillaSansSerif (`comintern_country`) | Code Red October | no |
  | anything else | Liberation Sans, with a line in the log | no |

  "Anything else" is three display faces in the install: Eurostile, SV Basic Manual and
  urania_czech.
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
- **A font whose descriptor gives every character the same advance is drawn a character
  to a cell of that advance**, each letter in the middle of its own and **narrowed to the
  width of the original's glyph of it** where the typeface draws it wider - an M of
  Liberation Sans ran over its neighbours, the maintainer's screenshot of 2026-11-04,
  where `timefont`'s M is 10 wide like its cell. Two in the base game
  are: `Courier` and `timefont`, the date widget's, whose 143 characters all advance 10.
  *Measured* on the maintainer's screenshots of the land orders window: each character of
  the date starts exactly 10 pixels after the last, so its figures stand in the boxes of
  `time_overlay` behind them - drawn in a typeface of its own widths they ran across the
  boxes (`landorders.md`, section 2).
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
  How the original uses `borderSize` and where it breaks a line are read since: the placing
  above, and the breaking a few items up.

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
  box, with the `size` it declares for room; typing into one is section 8. **Where its line
  stands is section 8 since 2026-11-14** (`CEditLinePlacement`): the border in twice across,
  and down the border twice, or with a picture behind it the border and half of what the
  box's height leaves the line - read, and set beside two screenshots of the original
  glyph row for glyph row. ~~Its text starts further in than a text box's: one less than a
  second border across, and one more than a second border down, *measured* on the same
  screenshot from the unit's name, 15 across and 9 down from its corner~~ - the 9 holds
  (section 8); the 15 was one short, the original's `5` of "5th Air wing" standing 17 in
  where 15 put a letter of no side bearing 16 in.
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
  left, over the picture between them. Not read, and known for those two only. **A button that draws a country's shield
  turns the same way** since 2026-11-06 - the lobby's shortcut shields, `lobby.gui`'s `shield` a negative quarter, set
  beside the maintainer's screenshot of the original's lobby (`frontend.md`, section 3) - and with them the lobby's
  `player_shield`, the province view's `owner_flag` and the politics screen's two selection buttons, whose files turn
  them too. A button of any other picture is not turned.
- **A line too wide for a text box that says `fixedsize = yes` is cut, and ends in three
  dots.** *Measured* on the same screenshot, where a division's name ends "CCN ..." in the
  middle of a word. ~~*Ours:* as many letters are kept as leave the dots room in the box's
  `maxWidth`; the original's name ends ten pixels short of that, so it measures the room
  some other way, which was not read. A line with a colour in it is left alone.~~ **Read on
  2026-11-14** (section 6, `CFixedSizeCut`): the text is cut before the character that brings
  a line to the box, and its last four characters before the cut become a space and three dots
  - the space before the dots on the screenshot is that, and the character it gives up for the
  space likely the ten pixels short (not measured again). A colour in the text is counted as nothing and kept.
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
- **A caption is laid out as section 6 says**, in the button's font - ~~one line in the middle of its button~~ until
  2026-11-14, which a scrollbar's end buttons still are, how the original draws theirs not read. Captions and text
  boxes both draw their own glyphs: a text box because its colour changes in the middle of a line, and both because a
  typeface of other widths is narrowed as it is drawn.
- **The typefaces are in `fonts/`, with their licences**, and `fonts/readme.md` says where
  each came from. Which stands for which is one table in the client.
- **A button's shortcut is read** for function keys, `ESCAPE`, `ENTER` and single letters
  and digits. The other words the files use are not yet keys. *Ours*, by the maintainer's word of 2026-11-12: **a
  held key presses its button again and again**, at the keyboard's own repeat, all but Escape - which would close a
  window and then open the in-game menu and close it again - and **the number pad's Enter is Enter**. The clock's speed
  keys repeat too; Space, Escape, H, the console's key and the debug menu's do not. **The arrow keys go no further
  than the game's own nodes** (`Main._Input`): Godot answers an arrow, as its `ui_` actions, by searching the whole tree
  for a control to focus when none has it - 26 ms an event with the tree at some 21,000 nodes - and a held arrow's
  repeats took the map from 90 frames a second to 26 (2026-11-12). Nothing here uses Godot's focus; the map scrolls by
  asking whether a key is down.
- **A checkbox is a button that a click ticks or clears**, showing the second frame of
  its sprite while ticked and the first while not - **and its fourth, ticked or not, while
  it is switched off**, where its sheet has four (`reversing/findings/FINDINGS-landorders.md`,
  section C; `confirmed`, read 2026-11-04 for the land orders window's greyed rows, whose
  `GFX_checkbox_default` is an empty box, a ticked one and two grey filled ones). A third
  state shows the third frame; what it is was not read, and nothing here shows it.
- **A button says whether the mouse is held down on it**, which is what lets a screen
  repeat a step while it is (`builders.md`, section 6), and can be given another sprite
  by name.
- **A button may answer the right mouse button too**, going down and coming up on it - and
  takes it only where a screen asks, so a right click elsewhere still goes on to what lies
  under it. The original's observer of a button has nine events and of a checkbox six; which
  is which was not read, and the leader window's two that are taken for the right button
  were inferred from where they sit - the maintainer confirmed both on 2026-10-11
  (`leaders.md`, section 12).
- **An element greys everything it holds** (`CGuiObject.Greying`, 2026-11-06): greyed, enabled, or left to what holds
  it, the nearest element up the tree that says either deciding. A greyed button draws as a disabled one and cannot be
  pressed, a checkbox showing its grey frame; a greyed scrollbar - a slider among them - is drawn grey, bar, end buttons
  and knob, and cannot be moved or locked. **The original's counterpart greys nothing**: it is whether an element takes
  the mouse, set on an element and handed down to every element under it as it is set - a button told no answers no
  click and looks as before (`reversing/findings/FINDINGS-frontend.md`, C.4; `FINDINGS-guistatic.md`). A screen run by the
  AI stops taking the mouse whole and its arrows then take it again (`frontend.md`, section 4.4); here it is greyed as
  well, by the maintainer's choice (`docs/compatibility.md`, entry 19). Here the flag is asked up the tree instead, so a
  row added later to a greyed list is greyed with it, and a button its screen enables each frame stays greyed under a
  greyed window - which the original's would not, at the next frame. A button's own `Disabled` and its greying are
  apart: either keeps it from being pressed.

## 8. Typing into an edit box

Read out of the original on 2026-11-05 for the save dialog (`reversing/findings/FINDINGS-editbox.md`), and built
(`CEditText` in `Interface/Text`, `CGuiTextBox.MakeEditable`). [exe] unless marked.

- **The keyboard** goes to an edit box when it is clicked with the left button, or when a screen gives it - the
  save dialog gives `game_name` it as it opens. **It stays there until another edit box takes it**: hiding the box,
  Enter, Escape and a click on a button take nothing away. A hidden box takes no keys. A click does not move the
  caret. **The box is offered every key before anything else** - a button's shortcut, the map - and what it takes goes
  no further, so a letter typed into it pauses nothing and selects no group.
- **What it takes**: every character as its one byte - Windows-1252 - inserted at the caret, the caret after it; and
  **Backspace, Delete, Left, Right, Home and End**, no other key. Left and Right are taken only with neither Ctrl nor
  Shift held, Delete without Shift. **Enter is taken and does nothing** - a box has no line break and no listener -
  and Escape goes past it, to `cancel`'s shortcut in the save dialog. With Ctrl held no character reaches it; there is
  no selection, no clipboard, no Up or Down.
- **What it refuses**, which goes on to whatever else takes keys: Tab; and where its font's `effect` is on - **the
  default; a font says `effect = no` to turn it off** [mod: `interface/core.gfx`] - the seven characters `"` `§` `¤`
  `£` `@` `{` `}`. `game_name`'s `Arial12_bold` sets nothing, so the seven are refused there.
- **At most 250 characters** typed, whatever the box's size: a character is taken and not inserted once the text
  holds 250. Text a screen sets is never cut. No `.gui` key moves the cap; there is no `maxCharacters`.
- **Backspace** erases the character before the caret - **and with the caret at the very start, the one after
  it**. **Delete** the one after it. **Home** goes to the start of the caret's line, **End** to the line's length **as
  the last edit left it**, which after the caret has moved to another line is that other line's.
- **A typed `'` also moves the caret right**, and a `%` typed without Shift left: the original's key test takes
  their codes for the arrows' (on a US layout `'` always, `%` never).
- **The box shows only the line the caret is on**: its text is laid out by the original's text layout
  (`CTextLayoutLineBreak`, section 6) to its `size` across less twice its `borderSize` - a line cut where it and one
  `O` more are wider, at the last space before the first character at which it reaches the room less the `O`, the
  space belonging to neither line, else after that character (read 2026-11-14, `reversing/findings/FINDINGS-textdraw.md`,
  section 2.3; until then the last space that fits, else the most characters, *likely*) - so typing past the box's width
  jumps it to a new line. There is no scrolling sideways. **A text a screen sets leaves the caret at its end**, so a
  box shows that text's last line (`CTextLayout::SetText`, the insert putting the caret after what it inserted).
- **The line is drawn by the same object as a `textBoxType`'s text** (`reversing/findings/FINDINGS-textdraw.md`,
  section 2.2.2): handed the box's whole `size`, its border, format left and not of fixed size, so it fits, and put
  the border in from the box's corner. A line of the layout is narrower than that object's room, so it is never broken
  again.
- **So the line stands as a `textBoxType`'s that fits** (section 6; `CEditLinePlacement`): its pen **the border in
  twice** across, set left whatever the box's `format`; and its top **the border down twice** - or, where the box's
  `textureFile` gives a picture behind it, **the border and half of what the box's height leaves the line**, cut
  towards nought, the line's height being the font descriptor's `lineHeight` (the font's loader takes it positionally
  from the `common` line, `reversing/findings/FINDINGS-textdraw.md`, section 2.2.1 as amended 2026-11-14). [exe] for the
  rule, [mod] for the sizes, border, font and picture. Each glyph is then drawn as section 6 draws every glyph, a unit
  right - and up and down as *measured* below.
  *Measured* on two screenshots of the original, glyph row for glyph row (2026-11-14): **the save dialog's `game_name`**
  (260 by 32, border 8 by 6, `small_tiles_dialog`, `Arial12_bold` of line 14 and capitals 3 to 10 down it), a name typed
  - its capitals stand 17 to 24 below the box's top, its `_` 26, its first letter's ink 17 in and every later one where
  its pen puts it, 6 + (32 - 14) / 2 = 15 and a unit up; and **the unit panel's `unitname`** (235 by 24, border 8 by 4,
  no picture, the same font) - its figures 11 to 18 below the box's top and its first letter 17 in, 4 + 4 = 8 and **not**
  a unit up. So with a picture the line's glyphs are a unit higher than without one, which the reading's arithmetic does
  not give; why was not found (`reversing/findings/FINDINGS-textdraw.md`, *What is not established*). Here a line with no
  picture is put a unit lower to cancel the unit up every glyph is drawn with. Set so, both lie on the original's row
  for row; across, each letter is within a unit of the original's, the typeface here being another (section 6).
- **All of that is the bitmap font's. A box whose font is a `font = { }` entry** [mod: `interface/*.gfx`] - the
  console's `console_edit`, in `FPS_Font` - is handed to that font's own routine, which draws with Direct3D's text
  drawing (read 2026-11-15, `reversing/findings/FINDINGS-textdraw.md`, section 2.5): into a rectangle from the border,
  **set left and never centred up and down** - a box asks for centring only where it is `fixedsize`, and an edit box
  never is - so **the line's cell stands the border in twice each way**, whatever the box's height and whether a picture
  is behind it (`CEditLinePlacement.TrueTypeTop`). Its glyphs have no unit right and up: that is the bitmap font's half
  pixel. [exe], `likely`: whether Direct3D's own drawing would centre without a single-line flag is Direct3D's, and no
  screenshot of the original's console has been set beside ours. *Ours:* such a font is drawn as the shipped typeface
  that stands for its `fontname`, at the size whose cell - top of the tallest letter to foot of the lowest - is the
  entry's `height`, as Windows takes a positive height; a line is that cell, and the colour the entry's
  (`CGuiFonts.MakeTrueType`). The original asks the `font = { }` entries before the bitmap fonts for the loading
  screen; which it asks first for an interface element was not read - no name of the base game's is both. Before this
  reading, the same day, the console's line was centred by the bitmap font's rule and drawn in a stand-in of line 16 for
  want of a descriptor: its cell stood 22 down, and its letters three units lower and a size larger than now (measured on
  screenshots of ours, the window at 1920 by 1080).
- **The caret** is the `cursor` sprite [mod: `interface/core.gfx`, `gfx/interface/textcursor`], past the box's border
  by the width of its line before it and 3, and a line's height down for each line above its own - **and a unit higher
  where a picture is behind the line**, as the line is: `game_name`'s bar, rows 5 to 20 of its picture, stands 10 to
  25 below the box's top on the screenshot, where the border's 6 puts it 11 to 26 (*measured*; no caret was seen in a
  box without a picture). **It is on for half a second and off for half a second**, and a key taken
  hides it until it next comes on - up to a second.
- **A box's `textureFile` is drawn behind its text** [mod: the `.gui`], in the nine parts of section 6 - its corners
  kept to the box's `borderSize` and the rest **stretched** between them - **from the border to 2 short of the box's
  right and bottom edges**, where the border that way is not nought. *Measured* first on the maintainer's screenshot
  of the save dialog (2026-11-05): `game_name`, 260 by 32 with a border of 8 by 6, shows `small_tiles_dialog`'s rim 246
  by 20 at 10 and 8 in - the picture has 2 clear all round - and set beside it ours lies on it to the pixel (the text
  and the caret were said to as well, and were two and one pixels off - the bullets above). **Read on 2026-11-14** (`reversing/findings/FINDINGS-textdraw.md`, section 2.2.2), and the
  reading gives exactly that: the object that draws the line stands the border in and shows the box less the border,
  and the picture is that less two each way. Until then it was repeated between the corners, which on these even panels
  looks the same, and an edit box with no border down - the lobby's in a game, the metaserver's - was 2 short at the
  bottom too. Every edit box of the base game's with a picture is drawn so; the unit panel's name has none.

*Ours*:

- **Only a box a screen makes editable takes typing** - the save dialog's, the brigade card's, the console's, the go-to
  box's, the battle plans' and, since 2026-11-12, the unit panel's name (`unitpanel.md`, section 3). The original's every
  edit box does. The original's layout also backspaces after every insert while the text wraps past what the box holds -
  its height, or its most lines where they are set (`reversing/findings/FINDINGS-unitrename.md`, section 5); here only a
  box given a most (`CEditText.MaxLines`) is held so, the unit panel's name to one line.
- **A character Windows-1252 has no byte for is not taken** and goes on; the original's window would be handed the
  code page's own stand-in. Text set by code puts the caret at its end, where the original's was not read.
- ~~**The text stands where section 7 measured it**, 15 across and 9 down in the unit panel's box~~ - until
  2026-11-14, when the maintainer's screenshot of the save dialog with a name typed showed `game_name`'s line two
  pixels lower than that and its caret one higher, and the reading's placing (above) was taken with the one unit
  measured. The caret's bar on that screenshot stands a unit right of ours, the line before it being a unit wider in
  the original's font than in the typeface here.

Not established: how a button matches its shortcut, so whether the original lets an Escape typed into the box insert
its character before `cancel` takes it, and whether a box with the keyboard keeps Enter from a button whose shortcut it
is; where the caret goes across a space a line was cut at.

## What is not established, in one place

1. **The table of colours a font with no `colorcodes` falls back to**: who fills it, and
   with what.
2. **How a text box lays its text out**, beyond section 6 (where a line breaks, the cut of a
   `fixedsize` box, how the two kinds of text box differ in breaking, and where a `textBoxType`
   places its lines and cuts them off are read since 2026-11-14, and so are the line an edit box
   shows, the box a button's caption is given and the nine parts of a `textBoxType`'s picture): the
   five orientations other than the upper left and right, why the pictures sit half a pixel the other
   way from the text, which container crops a `textBoxType`'s picture, and why an edit box's line with
   a picture behind it stands a unit higher than the reading's arithmetic and one without does not
   (section 8) - which may mean a `textBoxType` with no picture is drawn a unit too high here, since
   both lines go through the one routine; no screenshot has shown one.
3. **Where the position is measured to** - that it is the upper left corner is a fit on
   the files - and how `rotation` and `scale` turn and size an icon. A quarter turn is applied
   as section 7 says - one way for a bar and another for a flag - all measured on screenshots of
   the original, none read. Nor is why a bar's picture is drawn upside down (section 5).
4. **The keys each kind takes, other than a window's.**
5. **Which of two buttons or icons of one name is found.**
6. **What frame 0 draws**, and whether an icon's frames are counted from nought.
7. **Whether code sets the frame of a multi-frame button that names none** - 88 in the
   base game, the map mode buttons among them - where the original otherwise takes the
   frame from the button's state (section 5; `reversing/findings/FINDINGS-buttonstatefx.md`,
   *Not established*). Which sprites `buttonstate.fx` is for is read: every plain one.
8. **Which look each element kind gives a shield**, and so when `over`, `down` and
   `disable` show (`reversing/findings/FINDINGS-maskedflag.md`, *Not established*): here a
   button's own, as any button's, and an icon's where its screen sets one.
9. **What `transparencecheck` tests**, and against which texture.
10. **Tooltips** are read and built (2026-10-11): `tooltips.md` is the spec, and says what of
    them is open. `pdx_tooltip` and the three tooltip keys of a button are still read past -
    no file of the original's or of BlackICE's gives any of them a value.
11. **How a scrollbar and a box of overlapping elements lay themselves out.** Both are
    drawn by the plain reading of their declarations (section 2). The list box was too,
    until it was read. Of the list box there is left: where its scrollbar stands, how far
    a notch of the wheel moves it, and how a list that goes by its lines' heights scrolls.
12. ~~**Which frame a ticked checkbox shows.** The second here (section 7), unread.~~ Read
    2026-11-04 (section 7): the second, and the fourth while switched off.
13. **Which element takes a click.** How the original hands the mouse to an element was
    not read, beyond a sprite's own test of a pixel (section 5), whose caller was not
    found; two things are known from the maintainer's eye. A list box does
    not keep a click from what it lies over (section 2), and **an icon does not keep
    one from a button under it** - a click on the counter of a line of a list picks the
    line (2026-10-09). Here a button, a scrollbar and any icon not `allwaystransparent`
    take a click, the uppermost first, and a window, a text box and a list box do not;
    an icon hands what the mouse does to it on to the uppermost button of its own
    window drawn under it, and keeps it from the map either way. That it is only a
    button of the same window, and that an icon still keeps a click from an icon, a
    scrollbar or a button of another window beneath it, is *ours*.
