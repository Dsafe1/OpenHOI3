# Built-in elements

An enhanced game's own: interface elements the engine paints, so that a page can be laid out without a texture or a
sprite entry behind each of its parts. Nothing here is the original's.

Decided 2026-11-24. The user's proposal: "a 'library' of built-in native GUI elements. So we can build up pages
without having to create textures or sprites for each element we create ... a new keyword. Something like:
`built_in_texture = { x = 10 y = 10 r = 0 g = 255 b = 0 }`." Agreed with two things added, "Sounds good": a `style`
beside the colour, so that what is built looks like the game, and the styles in one file a mod may change, so that the
look is not written out on every page.

`CBuiltInSprite`, `CBuiltInStyle`, `CBuiltInStyles`, `CBuiltInPainter` in `lib/OpenHOI3.Content/Gui`; the reader in
`CGuiTypes`; the picture made in `src/Gfx/CTextureManager`.

## 1. In a line

An icon or a button says `built_in = { style = panel size = { x = 300 y = 120 } }` where it would name a
`spriteType`, and the engine paints a rectangle of that size in that style.

## 2. The key

    iconType = {
        name = "stats_panel"
        position = { x = 10 y = 10 }
        built_in = { style = panel size = { x = 300 y = 120 } }
    }
    guiButtonType = {
        name = "apply"
        position = { x = 20 y = 140 }
        built_in = { style = button size = { x = 90 y = 24 } color = { r = 0 g = 255 b = 0 } }
        buttonText = "APPLY"
        buttonFont = "Arial12_bold_black"
    }

`iconType` and `guiButtonType` (and `checkboxType`) take it. Since a window's `backGround`, a list's `background` and
a scrollbar's `slider`, `track` and two end buttons are icons and buttons named by the window, all of those may be
built in too.

| key | |
| --- | --- |
| `style` | one of section 3's names, whatever its case. `flat` where none is given. A name nothing has is reported, with the names there are, and painted `flat`. |
| `size` | `{ x = .. y = .. }`, the picture's pixels - **needed**, since an icon and a button take their size from their texture and here there is none. Each from 1 to 4096; anything else is reported and held within them. |
| `color` | `{ r = .. g = .. b = .. }`, each 0 to 255, with `a = ..` where it is not solid; or the numbers bare, `{ 0 255 0 }`. Optional. `colour` is taken too. |

Any other key in the block is reported. **A `built_in` stands before a `spriteType` beside it.**

**Write it only in `interface/openhoi/`**, which is read in an enhanced game alone and which the original does not
open (`gameoptions.md`, section 5): to the original the key is an unknown one in a file it never reads.

**A built-in button takes its words.** The original draws a button's `buttonText` only on a text sprite, a plain
picture having none (`interface.md`, section 2); a built-in picture is plain and has no text sprite to be, so its
`buttonText` is drawn on it in its `buttonFont`, centred as on a text sprite. **Its looks are a button's**: under the
mouse, pressed and greyed come from the install's `buttonstate.fx`, as for any button (`shaders.md`).

## 3. The styles

The engine's, in the colours of the original's panels:

| style | what it is for | fill | border | bevel | gradient |
| --- | --- | --- | --- | --- | --- |
| `flat` | a mark, a swatch, a bar | white | none | - | - |
| `panel` | what a window or a group stands on | 200 186 142 | 1, 40 34 20 | 0.18 | 0.06 |
| `button` | something pressed | 188 166 104 | 1, 40 34 20 | 0.35 | 0.18 |
| `row` | a line of a list | 218 203 154 | 1, 132 118 80 | - | 0.10 |
| `header` | the dark ribbon over a group | 72 74 44 | 1, 16 16 10 | 0.20 | 0.12 |
| `divider` | a thin line | 40 34 20 | none | - | - |
| `frame` | a border around what lies under it | hollow | 2, 40 34 20 | - | - |
| `field` | a sunken box for a value | 232 222 188 | 1, 40 34 20 | -0.25 | - |

**A mod changes them in `interface/openhoi/builtin_styles.txt`** (`CBuiltInStyles.FilePath`), a block a style:

    panel = { fill = { r = 60 g = 70 b = 50 } border_width = 2 }
    steel = { fill = { r = 120 g = 128 b = 136 } border = { r = 20 g = 20 b = 24 } border_width = 1 bevel = 0.3 }

A block named as one of the engine's changes the keys it gives and keeps the rest; any other name is a new style,
begun as `flat`. The keys: `fill` and `border` (colours as above), `border_width` (pixels, nought for none), `bevel`
(-1 to 1; below nought it is sunken), `gradient` (0 to 1), `hollow` (yes for nothing inside the border). Another key
is reported. So a mod rethemes every built-in element in one place.

## 4. The picture

`CBuiltInPainter.Paint`, a pixel at a time:

- the **border** is `border_width` pixels on every side, never more than half the shorter side - so an element
  thinner than twice its border is all border;
- inside it a **hollow** style paints nothing;
- else the **fill**, lighter by half the `gradient` at the top row and darker by half at the last, evenly between;
- and the **bevel**: the first row and column inside the border are lighter by `bevel`, the last ones darker by it.

**An element's own `color` is the fill in place of the style's**, and the border is that colour at 0.45 of its
brightness - a coloured button keeps a border that suits it. On a hollow style the colour is the border itself.

A picture is painted once for each style, size and colour and kept, as a texture read from a file is.

## 5. How it reaches the screen

The reader turns the block into a sprite *name* that says all of it -
`openhoi_builtin:<style>:<width>:<height>:<rrggbbaa or ->` - and puts it where the element's sprite name goes
(`CBuiltInSprite.Name`). Asked for a sprite of such a name, the sprite table answers a plain sprite whose texture is
the name, and the texture manager paints it. So everything that draws a sprite draws a built-in picture with no code
of its own: frames, rotation, the button shader, a window dragged by it.

*Not built:* a built-in picture that stretches with its element - one made larger by code, as a window resized, is
scaled as any picture is, its border with it. A page gives each element its size.

## 6. Looking at a layout

`--window=<name>` puts up a window the interface files declare, as it is, with nothing behind its elements - for
looking at a layout while it is written. BlackICE's `interface/openhoi/builtin_sample.gui` declares
`openhoi_builtin_sample`, every style on one window.

## 7. An edit box's caret

**Every `editBoxType` declared in a file of `interface/openhoi/` has a caret the engine draws**: a line one pixel
wide, a pixel after what stands before it and as tall as the text's line less a pixel at each end. No key asks for
it - it was `built_in_caret = yes` for an hour on 2026-11-24, and the user's question, "In what situation would you
not set it?", had one answer: in the original's own windows, which are not in that folder. Everywhere else the
caret is the game's
`cursor` picture (`interface.md`, section 8), which is drawn for the original's boxes - 32 high, their text four
below their border - and in a low box stands some four pixels under its text and as many to its right. Asked for
by the user on 2026-11-24, of a 20 pixel field: "The text cursor is misaligned to the text box."

With a `field` 20 high under it, an edit box of the same size put **one pixel above the field's corner** with
`borderSize = { x = 4 y = 2 }` has its line in the field's middle.
