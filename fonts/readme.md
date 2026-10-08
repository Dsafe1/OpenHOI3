# The typefaces that ship with the game

Every letter the game draws comes from a file in this folder, or from a typeface a mod
puts in its own `gfx/fonts`. Nothing is taken from the machine the game runs on, so text
is the same on every one. `docs/mechanics/interface.md`, section 6, is how they are used.

These are not the original's. Its fonts are textures of glyphs made from commercial
typefaces - Arial, Trebuchet MS, Franklin Gothic, Garamond, Impact - none of which may be
passed on. Each file here is an open typeface that stands for one of those, chosen for
being as near to it as an open one gets, and all are under the SIL Open Font License 1.1,
which allows them to be bundled and redistributed with software so long as the licence
goes with them. The licence texts are beside the files and must stay there.

| file | typeface | stands for | licence text |
| --- | --- | --- | --- |
| `LiberationSans-Regular.ttf`, `-Bold`, `-Italic`, `-BoldItalic` | Liberation Sans 2.1.5 | **Arial**, with the same widths letter for letter; Tahoma; and any typeface with no stand-in of its own | `OFL-Liberation.txt` |
| `LiberationMono-Regular.ttf`, `-Bold` | Liberation Mono 2.1.5 | Courier | `OFL-Liberation.txt` |
| `FiraSans-Regular.ttf`, `-Bold` | Fira Sans 4.203 | Trebuchet MS | `OFL-FiraSans.txt` |
| `BarlowCondensed-SemiBold.ttf` | Barlow Condensed 1.408 | Franklin Gothic Demi Cond | `OFL-BarlowCondensed.txt` |
| `EBGaramond-Variable.ttf` | EB Garamond 1.003, every weight in one file | Garamond, Adobe Garamond Pro | `OFL-EBGaramond.txt` |
| `Anton-Regular.ttf` | Anton 2.116 | Impact | `OFL-Anton.txt` |

Where they came from, on 2026-10-08, unmodified:

- Liberation: the `liberation-fonts-ttf-2.1.5` release of
  `github.com/liberationfonts/liberation-fonts`.
- The rest: the `ofl/` folder of `github.com/google/fonts`. `EBGaramond-Variable.ttf` is
  that repository's `EBGaramond[wght].ttf` under a name without brackets; the file itself
  is unchanged.

Which typeface stands for which is the table in `src/Gfx/CShippedTypefaces.cs`. To change
one, put the new file here with its licence, change that table, and change the table above.
A typeface with the same widths as the one it stands for is worth more than a handsome one:
text set in it lands exactly where the original's did. Only Liberation Sans is that; the
others are sized by the height of a line and narrowed where they come out wider.

The `.import` files beside the typefaces are the Godot editor's and are kept in the
repository. `allow_system_fallback` is off in each: a character a typeface lacks stays
missing and is not borrowed from a font installed on the machine.
