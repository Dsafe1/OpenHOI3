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
| `checkboxType` | 99 | a button that stays down | as a button |
| `scrollbarType` | 34 | four buttons and a range | yes |
| `shieldtype` | 22 | a country's flag in a frame, in a dialog | no |
| `OverlappingElementsBoxType` | 21 | a row of things that may overlap | yes |
| `editBoxType` | 21 | text the player types | no |
| `eu3dialogtype` | 15 | a window that may also hold shields | yes |

Three more child keywords are read by the original and used by no file of its own or of
BlackICE's: `3dButtonType`, `multiSpriteButtonType`, `threeDguiType`. `confirmed`

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

*Ours, and stricter than the original:* a top-level key that is not `guiTypes`, a keyword
that is no kind of element, a key a kind does not take, and an orientation or a format
that is not a known word are each reported, once for each kind of complaint with a count.
The 58 files give none.

**Three kinds hold what the code puts in them**, and the files say only how. None of the
three was read out of the original; each is drawn here by the plain reading of its
declaration, and each is *ours* until one is.

- **A scrollbar** has a `size`, a range - `minValue`, `maxValue`, `startValue`,
  `stepSize` - a direction (`horizontal`) and four buttons declared inside it, which its
  `slider`, `track`, `leftbutton` and `rightbutton` keys name. *Ours:* a button at each
  end, the track stretched between them - a cornered tile keeps its edges - and the
  slider on the track where the value puts it. The slider is dragged, a click on the
  track brings it to the mouse, and an end button moves it ten steps. `likely` the
  original holds the four numbers in thousandths, so a step is never less than one.
  **A bar that runs up and down has its `leftbutton`, and its least value, at the
  bottom.** `measured`: the list boxes' scrollbar gives its `leftbutton` the picture
  of an arrow pointing down and its `rightbutton` one pointing up, and in the original
  the arrow at the top points up (the maintainer, 2026-10-08, on seeing them the other
  way round here). So the file's sprites are not swapped, as this project's readme said
  for a long time; the bar is read from the bottom. A list is at its top when its bar
  is at its most.
- **A list box** has a `size`, a `spacing` and the name of a top-level scrollbar. *Ours:*
  the windows put in it are stacked from the top with the spacing between them and cut
  off at the box's edge; when they do not all fit the scrollbar appears along the right
  edge, as long as the box is high, and the wheel moves the list too.
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
- **The child a window names as its `background` fills the window.** `inferred` from the
  files and from running the original's main menu in an earlier iteration of this project:
  it is always a button, usually one named `Background`, and a cornered-tile sprite repeats
  where anything else stretches.

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
  mask. `inferred` from the declarations. *Ours:* the flag is scaled to the overlay's size
  and the overlay blended on top; the mask is not applied. Both masks the base game ships
  are solid white, so nothing is lost; a mod's that cut a shape would be ignored.
- **`allwaystransparent = yes` lets clicks through.** `inferred` from the name. *Ours:* any
  other icon takes the click, so the map under the bar does not; the original tests the
  pixel (`transparencecheck`), and this does not.

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
- **Kinds that are not drawn are still read** - edit boxes and a dialog's shields - so a
  window's children are all there in their order and their names are known. A window
  counts how many it could not draw.
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

## What is not established, in one place

1. **The table of colours a font with no `colorcodes` falls back to**: who fills it, and
   with what.
2. **How a text box lays its text out**, beyond section 6: where a line breaks, what
   `fixedsize` changes, what the two kinds of text box differ in, the five orientations
   other than the upper left and right, and why the pictures sit half a pixel the other
   way from the text.
3. **Where the position is measured to** - that it is the upper left corner is a fit on
   the files - and how `rotation` and `scale` turn and size an icon.
4. **The keys each kind takes, other than a window's.**
5. **Which of two buttons or icons of one name is found.**
6. **What frame 0 draws**, and whether an icon's frames are counted from nought.
7. **Which buttons `buttonstate.fx` is for.** The shader is read (section 5); whether a
   sprite that names no effect file changes under the mouse was not looked for.
8. **What a mask in a `maskedShieldType` is for**, given that both shipped are blank.
9. **What `transparencecheck` tests**, and against which texture.
10. **Tooltips.** `pdx_tooltip` and the three tooltip keys of a button are read past.
11. **How a scrollbar, a list box and a box of overlapping elements lay themselves
    out.** All three are drawn by the plain reading of their declarations (section 2).
12. **What a checkbox does when pressed.** It is drawn as a button and stays as it is.
