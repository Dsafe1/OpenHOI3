# Where this departs from the original on purpose

The goal is to run the original's content and its mods as they are written. This is the
list of the places where that is **deliberately not so**: a file that is valid in the
original and means something there, and that here means something else, or nothing,
because somebody decided it should. A mod's author should be able to read this page and
know what of theirs will not carry over, and why.

Started 2026-10-08, at the maintainer's word, when the second such decision in a day was
made in passing and there was nowhere it belonged.

**What goes here.** A decision, with who made it and when, what the original does, what
this does instead, what a mod loses by it, and what would undo it. One entry a decision.

**What does not.**

- *Things not built yet.* A screen that does not exist is not a decision. Those are in
  `readme.md`, *Loose ends*.
- *Things nobody has read.* Where a spec says **Ours** because the original's behaviour
  is not known, that is a guess waiting for a reading, not a departure. Each spec in
  `docs/mechanics/` ends with what is not established.
- *The technology.* Fixed-point numbers, commands, our own engine: none of that is in
  the content's language, so none of it is the content's business.

When a decision is reversed its entry stays, struck through with the date, so that the
next person to wonder finds that it was wondered before.

---

## 1. One start

**Decided** 2026-10-07, with the plan.

| | |
| --- | --- |
| the original | offers eight bookmarked dates, nine self-contained scenarios, a custom game assembled from settings, and the campaign |
| here | one start: the campaign's first date |
| not read | `common/bookmarks.txt` and everything under `scenarios/`. The front end's and the lobby's windows are read with the rest of the interface files and nothing shows them |
| a mod loses | its own bookmarks and scenarios. Its history files are read as before, and it is their state on the start date that is played |
| why | one set of starting conditions to balance, one way into the simulation, one start state to build |
| to undo | nothing in `lib/` assumes a single date, but a lobby to choose in would have to exist |

`readme.md`, under *Loose ends*, has the reasoning at length.

## 2. The interface is scaled as a whole

**Decided** 2026-10-07.

| | |
| --- | --- |
| the original | draws an interface file's pixel as one pixel of the screen, so everything is smaller the denser the screen |
| here | an interface file's pixel is one unit of a canvas 1920 by 1080, and the canvas is scaled to the screen |
| a mod loses | nothing of its layout: positions, sizes and anchors mean what they did. What differs is that on a screen larger than the canvas there is no more room than before, only larger things |
| why | a dozen pixels of text on a 4K screen is unreadable, and that is what the files would give |
| to undo | a setting for the canvas's scale, down to one unit a pixel. None exists yet |

## 3. A font's glyph texture is not drawn

**Decided** 2026-10-08 by the maintainer, on seeing the top bar scaled: "I think we should
just make the decision that the old font system is replaced with a better one."

| | |
| --- | --- |
| the original | draws text from a texture of glyphs, `gfx/fonts/<name>.tga`, cut up by the descriptor `<name>.fnt` beside it |
| here | the texture is not drawn. The descriptor is still read - for the typeface it says the glyphs were made from, how wide they came out and how high a line is - and that typeface is drawn as outlines, at the size that reproduces those widths |
| a mod loses | **glyphs it painted itself.** A font whose texture is hand-drawn, or made from a typeface this project does not ship, comes out in a stand-in. A font's colour baked into its texture is kept, as one colour: the average of the texture |
| a mod can | put a `.ttf` or `.otf` of the font's name beside the descriptor. The original ignores it; this draws it |
| why | glyphs a dozen pixels high cannot be enlarged into anything readable (entry 2) |
| to undo | drawing the textures again at one unit to one pixel. Not planned: `CLAUDE.md` says not to bring them back |

`docs/mechanics/interface.md`, section 6.

## 4. No typeface is taken from the machine

**Decided** 2026-10-08 by the maintainer: "I dont want a software that depends on the local
pc installs."

| | |
| --- | --- |
| the original | has no such question: its glyphs are in its textures |
| here | every typeface drawn is a file in `fonts/` or in a mod's own `gfx/fonts`. No installed font is looked up, and a character a typeface lacks is left out rather than borrowed |
| a mod loses | nothing it had. But a descriptor that names a typeface this project has no stand-in for - six display faces in the base game - is drawn in Liberation Sans, and says so in the log |
| why | the same text on every machine; and the original's typefaces are commercial and cannot be passed on, so open ones stand for them |
| to undo | nothing to undo; a mod that wants a face supplies it (entry 3) |

`fonts/readme.md` has which typeface stands for which.

## 5. The colours of coloured text are fixed

**Decided** 2026-10-08 by the maintainer, twice: first of the red, green and yellow as
they looked ("Good guess. They can stay that way."), and again the same day once it was
known that the original's are in its files ("For now it can stay the way it is."). **For
now** is the maintainer's word: this one is expected to be looked at again.

| | |
| --- | --- |
| the original | takes the colour of each escape letter from a `colorcodes` block in the font's own `bitmapfont` declaration, font by font, and from a table of its own for a font with none |
| here | seven letters have a colour fixed in the code, the same for every font: `R`, `G`, `Y`, `B`, `W`, `b`, `g`. The block is not read |
| the difference today | the base game's red is the same to a point, and its white, black and grey the same; its green is brighter (0 255 0 against ours, about 26 204 26), its yellow deeper (255 189 0 against 255 217 0) and its blue pure (0 0 255 against about 64 128 255); it also has a dark red, `r`, which is not one of our seven and so draws in the font's own colour |
| a mod loses | any colour it changed, any letter it added, and any font it gave colours of its own |
| why | the colours were chosen by eye before anyone knew where the original kept them, and they look right |
| to undo | read `colorcodes` into the font as the original does, and keep ours as the table for a font with none. Small: the reading is `reversing/findings/FINDINGS-textdraw.md`, section 4 |

`docs/mechanics/interface.md`, section 6.

## 6. The player's country starts on its history's laws

**Decided** 2026-10-08 by the maintainer, having checked the original himself: a fresh
1936 game as Germany shows `two_year_draft` on the first screen with nothing touched,
where Germany's history says `volunteer_army`. "So this looks like a bug, because the game
AI should not have touched it." Asked whether this project should leave the player's
country alone: "Yes it should leave the players history laws."

| | |
| --- | --- |
| the original | by the first screen something has moved the laws of every country, the player's included: Germany is two laws up in conscription and two in the economy, which is the shape of the AI script's own rule, "the next law up, if it is allowed". Its units are then made under the changed conscription law |
| here | the country the player plays starts on the laws its history files give it, and its units are made under those. An AI, once there is one, chooses laws only for the countries it runs |
| the difference today | as Germany: `volunteer_army` and `basic_mobilisation` where the original shows `two_year_draft` and `war_economy`; reserves at a quarter of their strength, not half; reserves nobody gave an experience at 3.75, not 7.5; 469 manpower, not 882 |
| a mod loses | nothing of its files: a history file's laws now mean what they say for the player. A mod balanced around the player getting the AI's opening choices for nothing - which is what the original gives - will find the player's start weaker than it was tuned for |
| why | the files say what the country starts with, and nothing a player did changed it. The maintainer takes it for a bug in the original rather than a rule |
| to undo | let the AI's opening pass run for the player's country as well, before the units are made |

Two things this does not settle. **Ministers**: the original's AI script changes those
before the first hour too, and the question was asked about laws. And **the countries the
AI does run**: whether their units are made before or after its opening choices is for
when the AI is built - in the original it is after, for conscription.

Nothing has to be built for this today: there is no AI, so every country starts on its
history's laws. The entry is here so that the AI, when it comes, is not given the
player's country on the first day.

**What moves them was read on 2026-10-12** (`reversing/findings/FINDINGS-luahost.md`, section 3):
entering a new game, the original calls the install's Lua `Laws(country)` for every country -
not guarded by who plays it - just after asking `AI_GenerateNonHistoricalRelation`, and
`ai_politics_minister.lua`'s `Laws` posts a change of law for each group it wants moved. So the
"something" is a script call, and this entry is that one call not made for the player's country.
`docs/mechanics/lua.md`, section 5, has it; for the countries the AI runs it waits for the AI.

`docs/mechanics/units.md`, section 8; `reversing/findings/FINDINGS-unitstart.md`, section 9.

## 7. A counter always carries its unit's name

**Decided** 2026-10-09 by the maintainer, told that the original writes the unit's name
on a counter only with a setting on: "It can stay that way until we implement the
settings." **Until then** is his word: this one ends when the settings are built.

| | |
| --- | --- |
| the original | reads `show_unit_names` from the player's `settings.txt`. On, a near counter carries its unit's name, cut to eight letters; off - which is what it is until the file says otherwise - the name of the unit's leader, and nothing for a unit with none |
| here | the unit's name, always. `settings.txt` is not read. Units have had leaders since 2026-10-11 (`docs/mechanics/leaders.md`); the counter still carries the unit's name, as decided |
| the difference today | a player who had the setting off sees unit names where the original showed leaders' |
| a mod loses | nothing: it is the player's setting and not a mod's file |
| why | the maintainer plays with it on, and without leaders, when this was decided, the other way would have left every counter bare |
| to undo | read the setting, and with it off write the leader's name. The rule is `reversing/findings/FINDINGS-counters.md`, section 4 |

`docs/mechanics/mapunits.md`, section 2.

## 8. Opening a battle's window does not move the map

**Decided** 2026-10-09 by the maintainer, on trying the window: "Please dont center on the
province when opening the battle screen." He was not told at the time that the original's
row does; that was put to him afterwards.

| | |
| --- | --- |
| the original | a click on a battle's row in a unit's panel selects the battle and puts the province fought over in the middle of the map. What a click on the battle's marker does was not read |
| here | the row and the marker both bring the window up, and the map stays where it is |
| the difference today | a player who used the row to find a battle far away has to go there himself |
| a mod loses | nothing: no file says it |
| why | the maintainer found the jump unwanted; a battle opened by its marker is on screen already |
| to undo | one line, where the session opens the window - and it could be the row alone |

`docs/mechanics/combatview.md`, section 1; `reversing/findings/FINDINGS-combatview.md`.

## 9. A released puppet is in its master's supply network at once

**Decided** 2026-10-09 by the maintainer, who had watched the original's delay himself that
day and was told it looks like an accident of ordering and not a rule: "I agree".

| | |
| --- | --- |
| the original | groups its areas for supply when land changes hands, which on a release is before the new country is anybody's puppet; so for some days each released country draws on its own capital, and only when something else has the groups made again does it join its master's network |
| here | the network is made whole whenever it is asked for, so a puppet is in its master's group, on the group's depot, from the hour it is one |
| the difference today | none that can be reached: nothing releases a country or makes a puppet during a game yet. When something does, the depot moves at once where the original's moves some days later - Manchukuo and a released North Korea go to Pusan the same day |
| a mod loses | nothing of its files. An event chain timed around those days of a puppet standing on its own supplies would find them missing |
| why | the delay is what is left over from the order two things happen in, it is not written anywhere a mod could read or set, and keeping it would mean keeping the network up to date piece by piece as the original does only to be late in the same places |
| to undo | make the groups again only when a province changes hands and on whatever else the original does it on - which was not read (`docs/mechanics/supply.md`, *Not established*) |

`docs/mechanics/supply.md`, section 9; `reversing/findings/FINDINGS-supplynetwork.md`,
section 10.

## 10. A requirement tooltip is indented as BiceLib corrects it

**Decided** 2026-10-12 by the maintainer, when the technology screen's tooltips were taken up:
"In the Bicelib.dll we already have a hook which fixes the indentation for those types of
tooltips. Please use the fixed logic."

**Extended** the same day to every block of conditions written as words - a decision's `allow` on
its info icon (`docs/mechanics/scripttext.md`, section 3) - since the DLL corrects the one renderer
all of them go through. Not put to the maintainer separately; `CConditionText` is where it is done,
and where a country scope's conditions now have their asterisks. Effects have no correction and keep
the original's indent.

**And one step past the DLL's**, the same day: a block inside a `not`, below the top, is given no
asterisk of its own, the `not`'s standing for it. The corrected sums give it one beside the `not`'s,
and the maintainer, seeing "(*)(*)Germany do NOT control Paris" in a decision's tooltip: "Something
is wrong with the asterisk for the negation. It is rendered twice." Whether the DLL draws two as
well was not looked at.

| | |
| --- | --- |
| the original | writes an `allow` block's conditions with three spaces a level, but gets the depth wrong inside an "all of" and a "one of": a block's header indents a line its parent placed, its conditions sit level with the header, a block inside a block starts again at one level, a block inside a scope has no asterisk, a country scope's conditions none either, and a "none of" indents its condition into the middle of its parent's line |
| here | the same lines, words and order, with the corrected arithmetic the mod's DLL patches in: no header indents itself, each condition of a block one level deeper than the block, a block in a block at its real depth, a block's asterisk drawn, and a "none of" on its parent's line |
| the difference today | only the spaces before a line and an asterisk on a block inside a scope. No base game technology nests deeper than three levels, and nothing here has a country scope yet |
| a mod loses | nothing of its files; a mod that worked round the original's indentation in its texts would see its own spaces added to correct ones |
| why | the original's is a bug that makes a nested block unreadable, and the mod already plays with it mended |
| to undo | the three sums in `CRequirementText` back to the original's - `reversing/findings/FINDINGS-techtext.md`, section 2 has them |

`docs/mechanics/technologyscreen.md`, section 9; `reversing/findings/FINDINGS-techtext.md`,
sections 2 and 4.

## 11. The AI's choice of an event's option is drawn on the game's own generator

**Decided** 2026-10-12 by the maintainer, asked how the AI should draw it: the game's own
generator, over the original's wall clock and over the event scope's state of chance.

| | |
| --- | --- |
| the original | weighs each option by its `ai_chance`, cuts each share of a hundred to a whole number, and takes the first option a roll of 0 to 99 does not pass - the roll from a **fresh generator seeded with the time of day**, so the choice is reproducible from neither a save nor a seed |
| here | the same weights, the same shares, the same test and the same fall to the first option - the roll from **the game's own generator**, which an hour seeds from the game's seed |
| the difference today | which option an AI country takes, run against run: here a game played the same way chooses the same; in the original it differs every time |
| a mod loses | nothing; no file can tell the two apart |
| why | a game here is replayable from its seed and its commands, and the tests depend on it; a choice by the clock would make every AI event a different game |
| to undo | seed a fresh MT19937 from the clock for each choice, as `reversing/findings/FINDINGS-eventfire.md` section 1.2 has it |

`docs/mechanics/events.md`; `reversing/findings/FINDINGS-eventfire.md`, section 1.2.

---

## Stricter, with the same meaning

Not departures - a file means what it meant - but a mod's author will see them, so they
are listed.

- **A key nothing recognises is reported.** The original drops it without a word; this
  reads the rest of the file the same way and says which key it did not know. So far in
  the interface files (`docs/mechanics/interface.md`, section 2), in unit types, historical
  models and orders of battle (`docs/mechanics/units.md`), and wherever a loader knows the
  whole of what a block may hold.
- **A brigade of a type nobody declares is left out**, and said to be. The original keeps
  it with no type, which nothing can then use. It is still given its number, so the units
  after it are numbered as the original numbers them.
- **A texture named with the wrong extension is found**, as it is in the original, and a
  path with a backslash that should have been doubled still names the file it names
  there. Neither is reported.
