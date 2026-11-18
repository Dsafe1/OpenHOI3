# The typefaces that ship with the game

Every letter the game draws comes from a file in this folder, from a typeface a mod
puts in its own `gfx/fonts`, or - for a large font, whose capital H stands 38 pixels or
more, since 2026-11-18 - from that font's own glyph texture in the player's install or mod,
made into distance fields as it loads and never copied here. Nothing is taken from the
machine the game runs on, so text is the same on every one. `docs/mechanics/interface.md`,
section 6, is how they are used. So Breitkopf Fraktur (Olde English) and Liberation Mono's
place as Another Typewriter stand for nothing in the base game or BlackICE today: the fonts
made from those faces there, `old_english` and `kart_font`, are large.

These are not the original's. Its fonts are textures of glyphs made from commercial
typefaces - Arial, Trebuchet MS, Franklin Gothic, Garamond, Impact, Olde English - none of
which may be passed on. Each file here is a free typeface that stands for one of those,
chosen for being as near to it, or as fitting, as a free one gets. Most are under the SIL
Open Font License 1.1, which allows them to be bundled and redistributed with software so
long as the licence goes with them; two are free typefaces under their own terms, set out
below. The licence texts are beside the files and must stay there.

| file | typeface | stands for | licence text |
| --- | --- | --- | --- |
| `LiberationSans-Regular.ttf`, `-Bold`, `-Italic`, `-BoldItalic` | Liberation Sans 2.1.5 | **Arial**, with the same widths letter for letter; Tahoma; and any typeface with no stand-in of its own | `OFL-Liberation.txt` |
| `LiberationMono-Regular.ttf`, `-Bold` | Liberation Mono 2.1.5 | Courier; Another Typewriter (`kart_font`, the province names on the map), nearly a monospace | `OFL-Liberation.txt` |
| `FiraSans-Regular.ttf`, `-Bold` | Fira Sans 4.203 | Trebuchet MS | `OFL-FiraSans.txt` |
| `BarlowCondensed-SemiBold.ttf` | Barlow Condensed 1.408 | Franklin Gothic Demi Cond | `OFL-BarlowCondensed.txt` |
| `EBGaramond-Variable.ttf` | EB Garamond 1.003, every weight in one file | Garamond, Adobe Garamond Pro | `OFL-EBGaramond.txt` |
| `Anton-Regular.ttf` | Anton 2.116 | Impact | `OFL-Anton.txt` |
| `BreitkopfFraktur.ttf` - **downloaded, not in the repository**: below | Breitkopf Fraktur 2.10, by Dieter Steffmann (Typographer Mediengestaltung) | Olde English (`old_english`, `axis_country`: the Axis names on the map) | `FFC-BreitkopfFraktur.txt` |

## Breitkopf Fraktur: download it yourself

Its licence forbids publishing the file, so the repository does not hold it (`.gitignore`
names it) - only its licence and its `.import`. To have the Axis names in Fraktur:

1. download `https://www.1001fonts.com/download/breitkopf-fraktur.zip` - the font's page is
   `https://www.1001fonts.com/breitkopf-fraktur-font.html`;
2. take `BreitkopfFraktur.ttf` out of the zip and put it in this folder under that name,
   unchanged (72,552 bytes, version 2.10);
3. open the project in the editor once, or run its import, so that Godot imports it.

Without it the game runs as before and draws the Axis names in Liberation Sans, saying so
once in the log; `tools/export.ps1` warns that its export lacks it and leaves its licence
out. Which typeface stands for which, and that this one is fetched, is the table in
`src/Gfx/CShippedTypefaces.cs`.
| `CodeRedOctober.ttf` | Code Red October 1.0, by Heaven Castro Studios, after ParaType's PT Mono | KyrillaSansSerif (`Kyril`, `comintern_country`: the Comintern names on the map) | `CodeRedOctober-info.txt`, `OFL-PTMono.txt` |

Where they came from, unmodified:

- Liberation, on 2026-10-08: the `liberation-fonts-ttf-2.1.5` release of
  `github.com/liberationfonts/liberation-fonts`.
- Fira Sans, Barlow Condensed, EB Garamond and Anton, on 2026-10-08: the `ofl/` folder of
  `github.com/google/fonts`. `EBGaramond-Variable.ttf` is that repository's
  `EBGaramond[wght].ttf` under a name without brackets; the file itself is unchanged.
- Breitkopf Fraktur, on 2026-11-12: `www.1001fonts.com/download/breitkopf-fraktur.zip`, the
  font and the licence beside it as they are in the zip, the licence under a new name.
- Code Red October, on 2026-11-12: `www.fontspace.com/code-red-october-font-f41581`, whose
  zip holds the font as `CodeRedOctober-owwd0.ttf` and `info.txt`; both are here under
  new names, unchanged. `OFL-PTMono.txt` is `ofl/ptmono/OFL.txt` of `github.com/google/fonts`.

Chosen by the maintainer on 2026-11-12. What each of the two non-OFL licences allows:

- **Breitkopf Fraktur** is under the *1001Fonts Free For Commercial Use License*: free for
  personal and commercial use, not to be modified, and "not to be sold or published without
  written permission" of its author. The licence's current text on
  `www.1001fonts.com/licenses/ffc.html` adds that it "may be embedded into an application
  ... as long as the application does not distribute the given typeface, such as offering it
  as a download" - the zip's older copy, which is the one kept here, has no such clause. So it
  ships packed inside the exported game, which never offers it as a file of its own. **The
  repository does not hold it** since 2026-11-12, by the maintainer's choice: a public
  repository would publish it, so each builder downloads it (above). It was committed before
  that, so the repository's history still holds it, until that history is rewritten or a
  public repository starts without it. The author's own page says he gives his fonts away "for commercial
  purposes without any restrictions".
- **Code Red October** is labelled *Freeware* by its author and comes with no other terms. Its
  file says it is "Based on ParaType's PT Mono", which is under the SIL Open Font License 1.1,
  and that licence binds whatever is made from it: the derived font may be redistributed and
  bundled as freely as PT Mono, under the same licence, which is why PT Mono's goes with it.

Which typeface stands for which is the table in `src/Gfx/CShippedTypefaces.cs`. To change
one, put the new file here with its licence, change that table, and change the table above.
A typeface with the same widths as the one it stands for is worth more than a handsome one:
text set in it lands exactly where the original's did. Only Liberation Sans is that; the
others are sized by the height of a line and narrowed where they come out wider.

The `.import` files beside the typefaces are the Godot editor's and are kept in the
repository. `allow_system_fallback` is off in each: a character a typeface lacks stays
missing and is not borrowed from a font installed on the machine.
