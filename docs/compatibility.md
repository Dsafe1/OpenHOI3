# Where this departs from the original on purpose

The goal is to run the original's content and its mods as they are written. This is the
list of the places where that is **deliberately not so**: a file that is valid in the
original and means something there, and that here means something else, or nothing,
because somebody decided it should. A mod's author should be able to read this page and
know what of theirs will not carry over, and why.

Started 2026-10-08, when the second such decision in a day was
made in passing and there was nowhere it belonged.

**What goes here.** A decision, with when it was made, what the original does, what
this does instead, what a mod loses by it, and what would undo it. One entry a decision.

**What does not.**

- *Things not built yet.* A screen that does not exist is not a decision. Those are in
  `docs/worklist.md`, *Loose ends*.
- *Things nobody has read.* Where a spec says **Ours** because the original's behaviour
  is not known, that is a guess waiting for a reading, not a departure. Each spec in
  `docs/mechanics/` ends with what is not established.
- *The technology.* Fixed-point numbers, commands, our own engine: none of that is in
  the content's language, so none of it is the content's business.

When a decision is reversed its entry stays, struck through with the date, so that the
next person to wonder finds that it was wondered before.

---

## 1. The campaign alone: no scenarios, no custom games ~~, no bookmarks~~

**Decided** 2026-10-07, with the plan, as one start. **Amended 2026-11-15**: "put the bookmark support
on the loose end. Scenarios should still be considered out of scope." So the bookmarks are no longer part of this
decision: they are planned work, in the worklist's *Loose ends* - until they are built the lobby offers the defines'
`start_date` alone (entry 17), which is a thing not built and not a departure. The scenarios, and the custom games,
which the amendment did not touch, stay out. **Bookmarks are built since 2026-11-15** (`docs/mechanics/bookmarks.md`):
the lobby offers every historical bookmark and the Quick Start panel its four starts.

| | |
| --- | --- |
| the original | offers ~~eight bookmarked dates,~~ nine self-contained scenarios, a custom game assembled from settings, and the campaign - from any of eight bookmarked dates |
| here | the campaign, ~~one start: the campaign's first date~~ ~~its first date until bookmarks are built~~ from any of the bookmarked dates and the Quick Start panel's four |
| not read | everything under `scenarios/` |
| a mod loses | its own scenarios and its custom game settings. Its history files are read as before |
| why | a scenario has its own dates, map extent and victory conditions, apart from the campaign - a second simulation to keep beside the first; a custom game is a start assembled from settings, a second set of starting conditions to balance |
| to undo | read `scenarios/` and build each scenario's map extent and victory conditions, or the custom game's settings screen |

`docs/worklist.md`, under *Loose ends*, has the reasoning at length; the front end is `docs/mechanics/frontend.md`, the
bookmarks `docs/mechanics/bookmarks.md`.

## 2. The interface is scaled as a whole

**Decided** 2026-10-07.

| | |
| --- | --- |
| the original | draws an interface file's pixel as one pixel of the screen, so everything is smaller the denser the screen |
| here | an interface file's pixel is one unit of a canvas 1920 by 1080, and the canvas is scaled to the screen |
| a mod loses | nothing of its layout: positions, sizes and anchors mean what they did. What differs is that on a screen larger than the canvas there is no more room than before, only larger things |
| why | a dozen pixels of text on a 4K screen is unreadable, and that is what the files would give |
| to undo | a setting for the canvas's scale, down to one unit a pixel. None exists yet |

## 3. A ~~font's~~ small font's glyph texture is not drawn

**Decided** 2026-10-08, on seeing the top bar scaled: the old font system is
replaced with a better one.
**Narrowed** 2026-11-18, on seeing a test of distance fields made from the
glyph textures and the sizes of every font of the base game and BlackICE: "large fonts (at
least 38) get upscaled, and the other ones use a fallback". So a font whose capital H stands
38 pixels or more - the map's faction and province names, and BlackICE's display faces - is
drawn from its own glyphs again, as distance fields; only the smaller ones are not.

| | |
| --- | --- |
| the original | draws text from a texture of glyphs, `gfx/fonts/<name>.tga`, cut up by the descriptor `<name>.fnt` beside it |
| here | ~~the texture is not drawn.~~ **a small font's** texture is not drawn. The descriptor is still read - for the typeface it says the glyphs were made from, how wide they came out and how high a line is - and that typeface is drawn as outlines, at the size that reproduces those widths. **A large font's** glyphs are cut out of its texture by the descriptor and drawn as distance fields, sharp at any size, in the descriptor's own layout |
| a mod loses | **for a small font, glyphs it painted itself.** A font whose texture is hand-drawn, or made from a typeface this project does not ship, comes out in a stand-in. A font's colour baked into its texture is kept, as one colour: the average of the texture - a large one's too |
| a mod can | put a `.ttf` or `.otf` of the font's name beside the descriptor. The original ignores it; this draws it, ahead of a large font's own glyphs |
| why | glyphs a dozen pixels high cannot be enlarged into anything readable (entry 2); glyphs of 38 and more can, and a display face has no stand-in |
| to undo | drawing the textures again at one unit to one pixel. Not planned: `CLAUDE.md` says not to bring a small font's back. The line is `CDistanceFieldAtlas.LeastCapHeight` |

`docs/mechanics/interface.md`, section 6.

## 4. No typeface is taken from the machine

**Decided** 2026-10-08: the game must not depend on what is installed on the
machine it runs on.

| | |
| --- | --- |
| the original | has no such question: its glyphs are in its textures |
| here | every typeface drawn is a file in `fonts/` or in a mod's own `gfx/fonts`. No installed font is looked up, and a character a typeface lacks is left out rather than borrowed |
| a mod loses | nothing it had. But a small font whose descriptor names a typeface this project has no stand-in for is drawn in Liberation Sans, and says so in the log - a large one is drawn from its own glyphs since 2026-11-18 (entry 3); and the Axis names' Fraktur, whose licence forbids passing the file on, is drawn only where whoever builds the game has downloaded it (`fonts/readme.md`) - which no font of the base game or BlackICE asks for any more, their `old_english` being large |
| why | the same text on every machine; and the original's typefaces are commercial and cannot be passed on, so open ones stand for them |
| to undo | nothing to undo; a mod that wants a face supplies it (entry 3) |

`fonts/readme.md` has which typeface stands for which.

## 5. The colours of coloured text are fixed

**Decided** 2026-10-08, twice: first of the red, green and yellow as
they looked ("Good guess. They can stay that way."), and again the same day once it was
known that the original's are in its files ("For now it can stay the way it is."). **For
now** is the decision's own word: this one is expected to be looked at again.

| | |
| --- | --- |
| the original | takes an escape letter's colour from the font's own **`color_override`** block, else from **one table every font's `colorcodes` fills** - the first value given for a letter winning in each, the `.gfx` files in their listing's order - else changes nothing (corrected 2026-11-19, `reversing/findings/FINDINGS-fontterrain.md`, section 1; this row said until then that `colorcodes` was each font's own). So in Their Finest Hour `chatfonts.gfx`, listed before `core.gfx`, would give the shared table its green (0 159 3) and a `P`, and BlackICE's edits to `core.gfx`'s `colorcodes` would change nothing but where `chatfonts.gfx` has no letter - inferred, the listing's order not read |
| here | seven letters have a colour fixed in the code, the same for every font: `R`, `G`, `Y`, `B`, `W`, `b`, `g`. Neither block is read |
| the difference today | the base game's red is the same to a point, and its white, black and grey the same; its green differs (0 159 3 shared and 0 255 0 on the `_black` fonts, against ours, about 26 204 26), its yellow deeper (255 189 0 against 255 217 0) and its blue pure (0 0 255 against about 64 128 255); it also has a dark red, `r`, and a `P`, which are not among our seven and so change nothing |
| a mod loses | any colour it changed in a `color_override` - BlackICE's five fonts' - any letter it added, and a shared colour where its file is parsed first |
| why | the colours were chosen by eye before anyone knew where the original kept them, and they look right |
| to undo | read `colorcodes` into one shared table and `color_override` into each font, the first value per letter winning, the `.gfx` files in their listing's order - `reversing/findings/FINDINGS-fontterrain.md`, section 1 |

`docs/mechanics/interface.md`, section 6.

## 6. ~~The player's country starts on its history's laws~~

**Reversed 2026-11-02** ("Ok, then lets delete the entry"): the player's country starts as the
original's does, its laws and technology brought forward with every other country's. The reading of that day found that
part of what moves the player's laws before the first screen is **the engine's own**, not the AI's: as a new game is
entered the original moves every country's laws to the latest each group allows and buys every country a head start in
technology (`docs/mechanics/politics.md`, section 13; `research.md`, section 9; `reversing/findings/FINDINGS-starttech.md`).
The Lua's look at the laws now runs for the player's country too (`politics.md`, section 12). What follows is kept as it
was decided, for the record.

**Decided** 2026-10-08, the original having been checked in play: a fresh
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
| why | the files say what the country starts with, and nothing a player did changed it. It was taken for a bug in the original rather than a rule |
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

**The AI's politics was built on 2026-10-14** (`docs/mechanics/politics.md`, section 12), and keeps to this entry:
a new game's `Laws(country)` and the politics minister after it are for the countries nobody plays, so the player's
ministers are left alone too, which was not raised for a decision. The question left open above is answered as the
original does it: the countries the AI runs have their units made first and then the laws changed, the change cutting
each brigade to its new ceiling. As Germany, the original's AI countries' brigades now start as the save has them for
3435 of 3534, where the history's laws gave 3041.

`docs/mechanics/units.md`, section 8; `reversing/findings/FINDINGS-unitstart.md`, section 9.

## ~~7. A counter always carries its unit's name~~

**Ended 2026-11-12**: the settings are built, so by its own terms this decision is over, and it was put back
among the worklist's *Loose ends* as work to do - the counter to follow `show_unit_names` as the original's does.
**Undone 2026-11-14**: it does - on, the unit's name; off, its leader's, or nothing - so nothing of this entry stands
(`docs/mechanics/mapunits.md`, section 2). The table below is as it was when the entry ended.

**Decided** 2026-10-09, on learning that the original writes the unit's name
on a counter only with a setting on: it can stay that
way until the settings are implemented. **Until then** is the decision's own word: this one ends when the settings are built.

| | |
| --- | --- |
| the original | reads `show_unit_names` from the player's `settings.txt`. On, a near counter carries its unit's name, cut to eight letters; off - which is what it is until the file says otherwise - the name of the unit's leader, and nothing for a unit with none |
| here | the unit's name, always. `settings.txt` is not read. Units have had leaders since 2026-10-11 (`docs/mechanics/leaders.md`); the counter still carries the unit's name, as decided |
| the difference today | a player who had the setting off sees unit names where the original showed leaders' |
| a mod loses | nothing: it is the player's setting and not a mod's file |
| why | the reference games are played with it on, and without leaders, when this was decided, the other way would have left every counter bare |
| to undo | read the setting, and with it off write the leader's name. The rule is `reversing/findings/FINDINGS-counters.md`, section 4 |

`docs/mechanics/mapunits.md`, section 2.

## 8. Opening a battle's window does not move the map

**Decided** 2026-10-09, on trying the window: "Please dont center on the
province when opening the battle screen." It was not known at the time that the original's
row does; that was raised for a decision afterwards.

| | |
| --- | --- |
| the original | a click on a battle's row in a unit's panel selects the battle and puts the province fought over in the middle of the map. What a click on the battle's marker does was not read |
| here | the row and the marker both bring the window up, and the map stays where it is |
| the difference today | a player who used the row to find a battle far away has to go there himself |
| a mod loses | nothing: no file says it |
| why | the jump was found unwanted; a battle opened by its marker is on screen already |
| to undo | one line, where the session opens the window - and it could be the row alone |

`docs/mechanics/combatview.md`, section 1; `reversing/findings/FINDINGS-combatview.md`.

## 9. A released puppet is in its master's supply network at once

**Decided** 2026-10-09, the original's delay having been watched in play that
day and read as an accident of ordering and not a rule, a reading agreed to.

| | |
| --- | --- |
| the original | groups its areas for supply when land changes hands, which on a release is before the new country is anybody's puppet; so for some days each released country draws on its own capital, and only when something else has the groups made again does it join its master's network |
| here | the network is made whole whenever it is asked for, so a puppet is in its master's group, on the group's depot, from the hour it is one |
| the difference today | a country released or made a puppet during a game - by the politics screen's puppet or liberation, an event's `release` or `release_vassal`, or a rebels' independence - is on its master's depot the hour it is one, where the original's moves some days later: Manchukuo and a released North Korea go to Pusan the same day |
| a mod loses | nothing of its files. An event chain timed around those days of a puppet standing on its own supplies would find them missing |
| why | the delay is what is left over from the order two things happen in, it is not written anywhere a mod could read or set, and keeping it would mean keeping the network up to date piece by piece as the original does only to be late in the same places |
| to undo | make the groups again only when a province changes hands and on whatever else the original does it on - which was not read (`docs/mechanics/supply.md`, *Not established*) |

`docs/mechanics/supply.md`, section 9; `reversing/findings/FINDINGS-supplynetwork.md`,
section 10.

## 10. A requirement tooltip is indented as BiceLib corrects it

**Decided** 2026-10-12, when the technology screen's tooltips were taken up:
BlackICE's BiceLib.dll already has a hook that fixes the indentation of those tooltips, and its
fixed logic is the one to use.

**Extended** the same day to every block of conditions written as words - a decision's `allow` on
its info icon (`docs/mechanics/scripttext.md`, section 3) - since the DLL corrects the one renderer
all of them go through. Not raised for a decision separately; `CConditionText` is where it is done,
and where a country scope's conditions now have their asterisks. Effects have no correction and keep
the original's indent.

**And one step past the DLL's**, the same day: a block inside a `not`, below the top, is given no
asterisk of its own, the `not`'s standing for it. The corrected sums give it one beside the `not`'s,
and on seeing "(*)(*)Germany do NOT control Paris" in a decision's tooltip: "Something
is wrong with the asterisk for the negation. It is rendered twice." Whether the DLL draws two as
well was not looked at.

| | |
| --- | --- |
| the original | writes an `allow` block's conditions with three spaces a level, but gets the depth wrong inside an "all of" and a "one of": a block's header indents a line its parent placed, its conditions sit level with the header, a block inside a block starts again at one level, a block inside a scope has no asterisk, a country scope's conditions none either, and a "none of" indents its condition into the middle of its parent's line |
| here | the same lines, words and order, with the corrected arithmetic the mod's DLL patches in: no header indents itself, each condition of a block one level deeper than the block, a block in a block at its real depth, a block's asterisk drawn, and a "none of" on its parent's line |
| the difference today | only the spaces before a line and an asterisk on a block inside a scope or a country scope. No base game technology nests deeper than three levels; a decision's `allow` and, since 2026-11-14, a technology's take every condition, country scopes among them, so a mod's deeper blocks show it most |
| a mod loses | nothing of its files; a mod that worked round the original's indentation in its texts would see its own spaces added to correct ones |
| why | the original's is a bug that makes a nested block unreadable, and the mod already plays with it mended |
| to undo | put the three sums in `CConditionText` back to the original's (`reversing/findings/FINDINGS-techtext.md`, section 2 has them). Since 2026-11-14 that one class draws a technology's `allow` as well as a decision's, `CRequirementText` having been folded into it |

`docs/mechanics/technologyscreen.md`, section 9; `reversing/findings/FINDINGS-techtext.md`,
sections 2 and 4.

## 11. The AI's choice of an event's option is drawn on the game's own generator

**Decided** 2026-10-12, on the question of how the AI should draw it: the game's own
generator, over the original's wall clock and over the event scope's state of chance.

| | |
| --- | --- |
| the original | weighs each option by its `ai_chance`, cuts each share of a hundred to a whole number, and takes the first option a roll of 0 to 99 does not pass - the roll from a **fresh generator seeded with the time of day**, so the choice is reproducible from neither a save nor a seed |
| here | the same weights, the same shares, the same test and the same fall to the first option - the roll from **the game's own generator**, seeded once from the game's seed |
| the difference today | which option an AI country takes, run against run: here a game played the same way chooses the same; in the original it differs every time |
| a mod loses | nothing; no file can tell the two apart |
| why | a game here is replayable from its seed and its commands, and the tests depend on it; a choice by the clock would make every AI event a different game |
| to undo | seed a fresh MT19937 from the clock for each choice, as `reversing/findings/FINDINGS-eventfire.md` section 1.2 has it |

`docs/mechanics/events.md`; `reversing/findings/FINDINGS-eventfire.md`, section 1.2.

## 12. The map always shows counters: `alwaysCounters=no` is not supported

**Decided** 2026-10-15, on its being shown that the weather map's pictures follow the setting: "'no' means to use
sprites, but no one uses them and they are also not easily moddable. So they are more or less a dead feature."

| | |
| --- | --- |
| the original | `mapRenderingOptions`' `alwaysCounters` in the player's `settings.txt`: `yes` draws counters at every zoom and the weather map mode's plain `_simple` icons; `no` draws the units as figures close in and the weather as clouds with rain and snow falling under them |
| here | always as `yes`: counters, and the `_simple` weather icons; a `settings.txt` saying `no` is reported and read as `yes` |
| the difference today | the units' figures and the full weather art of `interface/mapitems.gfx` - `weather_rain`, `weather_snow` and the rest without `_simple` - are never drawn; nor is `selection_projection`, the mark the original lays under a selected unit only without counters (`docs/mechanics/movement.md`, section 7, read 2026-11-14) |
| a mod loses | what it gives those types and its unit figures; its `_simple` types and its counters are drawn as before |
| why | nobody plays with them, and the figures are not easily modded: a dead feature |
| to undo | read `alwaysCounters` and draw the figures and the full weather pictures - the layer that drew the clouds, the falling rain and snow and the lightning was built on 2026-10-15 and taken out the same day (`docs/mechanics/weather.md`, section 6) |

`docs/mechanics/weather.md`, section 6; `docs/mechanics/settings.md`.

## 13. The player's settings are the original's `settings.txt`

**Decided** 2026-10-15: for now the existing settings.txt is read - the one in the
Documents folder, not the one next to the exe.

| | |
| --- | --- |
| the original | writes and reads `settings.txt` in `Documents\Paradox Interactive\Hearts of Iron III`, and ships one beside the executable |
| here | reads the one in the Documents folder ~~, and never writes it~~; the one beside the executable is not read |
| the difference today | ~~a setting changed here is not kept; nothing here changes one yet~~ none: the game options window writes it since 2026-11-04 (`mechanics/gameoptions.md`), and a session writes it as it ends |
| amended | **2026-11-04**, on the question whether the game options window's Apply should write that same file as the original does: "Write the original's file". So it is written there, in the original's format, and a change made here is the original's next time too - as the message settings' `messagetypes_custom.txt` already is (`mechanics/ingamemenu.md`, section 5) |
| a mod loses | nothing; a mod has no `settings.txt` of its own |
| with a mod | **2026-11-16**, the mod layer (`mechanics/mods.md`): the file is the mod's player folder's, `Documents\...\Hearts of Iron III\<user_dir>\settings.txt`, as the original writes it. Where that folder has none, the original reads `settings.txt` over its search path - the mod's folder, then `tfh/`, then the install's root, which ships one - and never the plain Documents folder's (`reversing/findings/FINDINGS-modlayer.md`); this reads the player's folder alone and takes the defaults, by the decision above, given again for a mod on 2026-11-16: "User folder only". Without a mod the original falls through the same way when the Documents folder has none; this does not |
| why | the player's settings are where the original keeps them, and one file serves both |
| to undo | settings of our own, in our own file |

`docs/mechanics/settings.md`.

## 14. The right-click menu and the five side menus are not built

**Decided** 2026-11-04, on the leftovers wave's reading: "Yeah dont build them."

| | |
| --- | --- |
| the original | makes `interface/rightclickmenu.gui`'s menu and `sidebar.gui`'s provinces, army, naval, air and combat menus, and shows none of them to a player: the menu's in-game rows are never filled, and the side menus' top-bar handlers are never attached to a button. Its one live piece is a developer's: Shift and the right button over an element in the main menu, `Reload: <file>` and `Close` (*inferred* to show) |
| here | neither file is read; `sidebar.gui`'s deploy menu is, as before |
| the difference today | none a player sees; a developer has no Shift-and-right-click reload of one `.gui` file in the main menu |
| a mod loses | nothing reachable: a `topbar.gui` that restores the `button_view*` buttons gets buttons that do nothing in the original too |
| why | nothing in the original reaches them; building them would mean inventing a way in |
| to undo | build them from `reversing/findings/FINDINGS-leftovermenus.md`, which has every row, click and figure |

## 15. A loaded save goes on with its own generator

**Decided** 2026-11-04, on the question whether a load should restore the save's `seed` and `count`: "Yes".

| | |
| --- | --- |
| the original | writes its generator's `seed` and the number of draws, `count`, into every save, and **throws both away on a load** - `CGameState::LoadKey` takes neither - so a loaded game draws on whatever the generator holds (`reversing/findings/FINDINGS-savegame.md`, section A.3) |
| here | a load seeds the game's generator with the save's `seed` and draws `count` times, so a loaded game goes on with the draws the saved game would have made. **The AI's own generator** (2026-11-12, `mechanics/landcombat.md`, section 8) is not in a save, as in the original, and a load seeds it from the save's `seed` at its first draw - decided that day, to keep the save's keys the original's; writing it is in `enhanced.md` |
| the difference today | the rolls after a load: here a save loaded twice plays the same; in the original it need not |
| a mod loses | nothing; no file can tell the two apart |
| why | a game here is replayable from its seed and its commands (entry 11), and a load should not end that |
| to undo | leave the generator as it stands and take neither key, as the original does |

## 16. An autosave holds the turn of the day, whatever the speed

**Decided** 2026-11-05, on its being shown that the reference autosaves hold the first hour of the day: on a slow speed it
lands on 0, and that is to be fixed in OpenHOI3.

| | |
| --- | --- |
| the original | decides on an autosave as the day begins and writes it a frame later (`reversing/findings/FINDINGS-autosave.md`), so what it holds hangs on the speed: at a slow speed the turn of the day, `1941.7.1.0`; at a fast one an hour later, `1941.7.1.1`, as every reference autosave |
| here | the autosave is written as the day is decided on, the hour that turned it run whole: always the turn of the day |
| the difference today | an autosave made at speed holds the hour before the original's would |
| a mod loses | nothing; no file can tell the two apart |
| why | a save should not hang on how fast the game was running when it was made |
| to undo | keep the decision and write it on the frame after the next, after that frame's hours (`CSession.DecideAutosave`) |

## 17. The front end greys the scenarios and the custom games ~~, and offers one bookmark~~

**Decided** 2026-11-06, choosing a "Pared-down lobby" - the original's main menu and its lobby "as far
as one start needs" - which follows from entry 1. Read in `reversing/findings/FINDINGS-frontend.md`; the screens are
`docs/mechanics/frontend.md`. **Amended 2026-11-15** with entry 1: the other bookmarks and the Quick Start panel, whose
four buttons start bookmarks, are no longer part of this decision but planned work (the worklist's *Loose ends*); until
they are built the lobby offers the defines' `start_date` alone and `toggle_quick_normal` stays greyed. The Scenarios and
Custom Games tabs stay greyed as decided. **Built 2026-11-15** (`docs/mechanics/bookmarks.md`): the lobby offers every
historical bookmark, `simple` picks the first panel and the Quick Start panel's four buttons start their games, so what is
left of this entry is the two tabs.

| | |
| --- | --- |
| the original | opens on the Quick Start panel where `settings.txt` says `simple=yes` - four buttons starting Germany in 1939, the United States in 1936, Japan in 1941 and the Soviet Union in 1941 - and on the main menu where `simple=no`; its lobby offers every bookmark of `common/bookmarks.txt`, the scenarios and the custom games |
| here | ~~the main menu always comes first and `simple` is not read for it; `toggle_quick_normal` is greyed; the lobby offers only the bookmark of the defines' `start_date`, and~~ its Scenarios and Custom Games tabs are greyed - each ~~of the three~~ with a tooltip of ours saying why (decided on 2026-11-06: "add them but grey them out with a tooltip that explains it") |
| the difference today | ~~a player whose `settings.txt` says `simple=yes` sees the main menu where the original shows Quick Start; a player cannot start at a later bookmark or~~ a player cannot open a scenario or custom game |
| a mod loses | its scenarios and custom games; ~~its other bookmarks and the Quick Start panel's buttons - until bookmarks are built, those too~~ |
| why | entry 1: the campaign is the one way into a game. ~~The bookmarks wait only on being built~~ |
| to undo | ~~for the bookmarks, build them (the worklist's *Loose ends*) and take the greying off `toggle_quick_normal`;~~ for the two tabs, entry 1 first |

## 18. Arcade cannot be chosen

**Decided** 2026-11-06 in building the lobby's options, and not raised for a decision: Arcade's supply is not built, so
offering it would offer the normal mode under another name. Read in `reversing/findings/FINDINGS-frontend.md`, D.2; the
panel is `docs/mechanics/frontend.md`, section 4.

| | |
| --- | --- |
| the original | the lobby's Game Mode row offers Normal and Arcade; Arcade draws every unit's supplies and fuel from its capital, with no network, no supply convoys and the Supply map mode greyed |
| here | the row's arrows are greyed and the mode stays Normal; a save or a `gameplaysettings` saying Arcade is read and kept, and plays as Normal |
| the difference today | Arcade is not offered, and a game in Arcade plays with the network |
| a mod loses | nothing of its files; a player loses the mode |
| why | it is not built |
| to undo | build Arcade's supply (D.2's sixteen places) and take the greying out of `CLobbyOptions` |

## 19. A screen the AI runs is greyed

**Decided** 2026-11-06, on seeing the screens greyed under Ai Control: the original greys out
nothing when switching to AI control, but greying it out is better and is kept - with the
sliders greyed too, and the intelligence screen's Production and Ministers buttons left usable. A list's own scrollbar is
neither greyed nor stopped (2026-11-19), as in the original, whose list never hands the setting to its bar
(`interface.md`, section 2). Read in
`reversing/findings/FINDINGS-frontend.md`, C.4; the screens are `docs/mechanics/frontend.md`, section 4.4.

| | |
| --- | --- |
| the original | under the AI each screen's controls - the same ones as here - stop taking the mouse: a click on them passes through, and **they look exactly as before** |
| here | the same controls are greyed - drawn as a disabled button is, the sliders' bars and knobs as well - and take no click; and **the production screen's sliders are greyed while its slider automation is above Manual Control**, the script setting them each day, where the original leaves them to be moved (decided on 2026-11-06: "make the slider ai mode of the production screen grey out the slider"); and **handing production to the AI puts the slider automation back to Manual Control**, where the original keeps the level - decided the same day: otherwise "it looks confusing if the player has the sliders on prioritize production, but the bigger AI actually prioritizes upgrades". A save made after it writes `automate_sliders=0` |
| the difference today | the player sees what the AI holds; the original shows nothing and ignores the click |
| a mod loses | nothing: no file says how a screen under the AI looks |
| why | a preference: it shows which controls are the AI's |
| to undo | have `CGuiObject.Greying` keep the element's look and only refuse the mouse |

## 20. An edited river bitmap is traced from its own sources

**Decided** 2026-11-06, on learning that the rivers' sources are kept in `map.bin`, which the original
tests by date against nine map files and not `rivers.bmp`: "switch to the map.bin way, but also fix the fault". Read in
`reversing/findings/FINDINGS-mapdraw.md`, C.6; the rivers are `docs/mechanics/mapdraw.md`, section 4.

| | |
| --- | --- |
| the original | `map.bin` holds the rivers' sources - every index-0 pixel of `rivers.bmp` - and is built again only when one of positions, provinces.bmp, terrain.bmp, definition.csv, adjacencies, terrain.txt, continent, region or coastlines is missing or newer than it; **an edited `rivers.bmp` alone is traced from the old sources** over the new pixels, so rivers break off or are lost until something else rebuilds the file |
| here | the sources are read from `map.bin` as the original reads them, and the file is tested against `rivers.bmp` too: where the bitmap - or any of the nine - is missing or newer, or there is no `map.bin`, the sources are those its builder would write, taken from the bitmap, and the log says so (`CMapBin`) |
| the difference today | only after `rivers.bmp` is edited with no other map file: the original draws the old sources' rivers, this the new |
| a mod loses | nothing: a mod's rivers are what its bitmap says |
| why | as decided: the original's test is a fault |
| to undo | take `rivers` out of `CMapBin`'s list of sources |

## 21. The fog of war turned off is the player's alone

**Decided** 2026-11-08, on the debug menu's `debug fow` switch being built as the original's: "Could you make
it not affect the AI?" Read in `reversing/findings/FINDINGS-espionage.md` (the daily rebuild) and
`reversing/findings/FINDINGS-navaldetection.md` (the sea battle's roll); the rule is `docs/mechanics/intelligence.md`,
section 3.

| | |
| --- | --- |
| the original | its console's `debug fow` off fills every country's knowledge of every province with 9 at each day's rebuild - the AI's too - and any fleet finds an enemy's without the roll |
| here | the countries played alone know every province at 9, given at the end of the rebuild, and only their fleets skip the roll; every other country knows what it would with the fog of war on |
| the difference today | with the switch off the AI plays as it would with it on: it does not see through the fog because the player turned it off |
| a mod loses | nothing: no file sets it |
| why | as decided: a debugging aid for the player should not change how the AI plays |
| to undo | fill every country's bytes with 9 at the start of `CProvinceIntel.Rebuild` where `FogOfWar` is off, and drop the played test in `CNavalSpotting.ShouldStart` |

## 22. The map cache is the Documents folder's alone

**Decided** 2026-11-08, on learning that the game was drawing from the install's shipped cache: "Please dont use that mapcache. Only use the one from documents". The rule is `docs/mechanics/mapcache.md`, section 11.

| | |
| --- | --- |
| the original | looks in the player's folder and then the install, so Their Finest Hour's shipped `tfh/map/cache` serves until something newer is built |
| here | the game asks `Documents\Paradox Interactive\Hearts of Iron III\map\cache` and nothing else, and builds the cache there where it is not the map's |
| the difference today | the first start after that folder is emptied takes about 2.3 seconds longer and writes about 220 MB to it; what it draws is the same, every file being the shipped one byte for byte |
| a mod loses | nothing: a cache is derived data |
| with a mod | **2026-11-16**: the folder is the mod's player folder's `map\cache`, the original's write folder with a mod, and nothing else - where the original would also find a cache in the mod's folder or the install (*likely*, `reversing/findings/FINDINGS-modlayer.md`); decided on 2026-11-16: "User folder only" |
| why | as decided |
| to undo | have `CMapCache.OpenOrBuild` take `Open`'s answer first, and pass `askInstall: true` |

## 23. A lent unit's leader cannot be changed

**Decided** 2026-11-09, the original having been seen in play letting the host do it.

| | |
| --- | --- |
| the original | the panel's `leader_button` on a lent unit is first disabled and then enabled again by a later test in the same routine, so the host may open the leader window, which lists the host's own leaders |
| here | the button and the portrait are greyed for a lent unit, and the command is refused |
| the difference today | the host cannot give a unit lent to it one of its own leaders |
| a mod loses | nothing; this is the engine's rule |
| why | a choice: a lent unit is the lender's |
| to undo | drop the `Lender` tests in `CUnitPanelText.MayChangeLeader`, `CUnitHeaderEntry` and `CAssignLeaderCommand` |

`docs/mechanics/expeditions.md`.

## 24. A loaded unit's first attack estimate counts its whole modifier

**Decided** 2026-11-11, ours, not raised for a decision: uninitialised memory cannot be reproduced.

| | |
| --- | --- |
| the original | the AI's attack odds keep the part of each unit's modifier that hangs on the unit alone - combined arms, experience, the leader, dissent, supplies, the difficulty, and for a defender its dig-in, ground and encirclement - in a cache that nothing initialises, so a loaded unit's first estimate counts that part or nought by what memory held, differently from run to run |
| here | the part is always counted, as the original counts it after the unit's first hour |
| the difference today | a loaded game's first AI estimates: tried the other way - nought for every loaded unit - entering the August 1938 save gave 110 of 110 land orders and the July 1941 save 16 the original did not give, where this gives 107 of 110 and 1 |
| a mod loses | nothing; this is the engine's arithmetic |
| why | counting the part matches the most units, and the original's own answer is not reproducible |
| to undo | there is no faithful undo; a run's nought units are not knowable |

`reversing/findings/FINDINGS-liveread.md`, section 46; `docs/mechanics/moving.md`, section 11.4.

## 25. BiceLib's Lua functions are not provided

**Decided** 2026-11-11: an accepted break. **Confirmed 2026-11-16**, after a first attempt at giving the
functions as `OpenHOI3.BiceLib` was taken back off main: compatibility patches for a DLL made for another binary are not
for the normal engine, which is finished first, with BlackICE testing the original's own modding API; BiceLib's patches and
mechanics, and an API for mod-defined native interface elements, belong to the enhanced engine (`docs/enhanced.md`, *Asked
for*; the attempt was discarded, and the enhanced engine starts afresh). The one fix BlackICE cannot play without is
made for every game: entry 29. BlackICE's `bicelib_lua.lua` passes over the DLL where the global `OpenHOI3` is set.

| | |
| --- | --- |
| the original | with BlackICE, `script/bicelib_lua.lua` calls functions that BiceLib - the mod's DLL, injected into the original's process - registers with the game's Lua, for its overlay, autosaves, map modes and commands |
| here | nothing provides them, so a call to one fails where the mod is played |
| the difference today | none in Their Finest Hour alone; with BlackICE, every BiceLib feature is missing |
| a mod loses | every BiceLib feature, until the interface below exists |
| why | BiceLib's features are to come back as a native interface of this engine's own that the mod can call, built from the fact base's prose, never from BiceLib's code or offsets |
| to undo | build that interface; nothing here is to be removed |

## 26. A click with a docked fleet or a based air unit selected goes where it is aimed

**Decided** 2026-11-13, the original's having been tried in the running game: in this case an
exception is made and the fixed Fleet -> Fleet is implemented right away.

| | |
| --- | --- |
| the original | while exactly one unit is selected that has no counter on the map - a fleet lying in port, an air unit at its own base - its click test offers no building picture and no counter but those of the same owner's units in that unit's own province, and otherwise answers nothing; so clicking a port with fleets in it goes *fleet, nothing changes, fleet* - every second click is lost |
| here | the click goes to what is under it as it does with anything else selected - a port's or an airfield's picture to the next of the player's units there, a counter to its unit - so a port goes *fleet, fleet* |
| the difference today | no click is lost going round a port's or an airfield's units |
| a mod loses | nothing; no file says anything about it |
| why | as decided: the original's lost click is a fault |
| to undo | in `CMapView.Click`, while the one unit selected has no counter, ask no building picture and take a counter only of the same owner's units in that unit's province, falling to nothing otherwise |

`reversing/findings/FINDINGS-seaair.md`, section 6.

## 27. A bookmark chosen makes the world afresh at its date

**Decided** 2026-11-15: proposed by the session building bookmarks and accepted ("Ok") before the build.
Read in `reversing/findings/FINDINGS-bookmarks.md`, sections 1.3, 2.1 and 4; the spec is `docs/mechanics/bookmarks.md`,
section 2.

| | |
| --- | --- |
| the original | keeps one world in the lobby and moves it: a later bookmark replays the history from the clock to its date; an earlier one **rewinds to the defines' `start_date`** - each entry's undo - and replays forwards. An undo is not always exact: a dated `manpower` undone writes the officers into the manpower, a `revolt_risk` comes back cut to whole points, a land leader's `rank` undone takes him off his unit - so a later bookmark chosen and then an earlier one may leave a world unlike the earlier one chosen at once |
| here | every bookmark chosen makes the world afresh at its date, from countries read anew: the same world whatever was chosen before |
| the difference | only after a later bookmark and then an earlier one, and only where an undo is inexact - the manpower is set again by every new game, and the lobby has no units for a leader to leave |
| a mod loses | nothing a file says |
| why | one way to make a world at a date - the one a first choice takes - and no undo to keep for every kind of history entry |
| to undo | keep the lobby's world and move it, giving every history entry an undo as the original's, the rewind with them |

## 28. The player's folder is not a layer of game files

**Decided** 2026-11-16, on its being shown the original's search path with the player's folder in front and its
`music` folder of `.mp3`s: "Keep entry 28". The switch is `CModRules.UserFolderIsLayer`.

| | |
| --- | --- |
| the original | reads every file through a search path whose front is the player's folder - `Documents\Paradox Interactive\Hearts of Iron III` without a mod, `...\<user_dir>` with one - so a file put there stands in for the mod's and the install's (`reversing/findings/FINDINGS-modlayer.md`) |
| here | the player's folder is asked by name only for what the game writes there - the settings, the saves, the message settings, the map cache, the logs, the battle plans; a game file is looked for in the mods' folders, `tfh/` and the install (`mechanics/mods.md`, section 2) |
| the difference today | the base game's player folder holds a `music` folder of `.mp3`s, which the original's search path puts in front of the install's music folder and this does not look in; nothing else in it is a game file. The file checksum walks the original's whole search path, the player's folder first, and is the original's figure |
| a mod loses | nothing it ships: a mod's own files are in its folder. A player's own file dropped into the player's folder to override one is not read |
| why | so that the base game reads what it read before the mod layer, and a stray file in the Documents folder does not change a game |
| to undo | set `CModRules.UserFolderIsLayer`, which puts the player's folder in front of the layers with a mod; without one it would need the Documents folder added too |

## 29. A province's most infrastructure is its `infra` building's

**Decided** 2026-11-16, setting BiceLib aside for the normal engine: BlackICE runs without its DLL
but for this one fix, which the normal engine keeps for every game - "Keep the fix, record it".

| | |
| --- | --- |
| the original | stores the figure of whichever infrastructure building it writes last - a plain store, a level built writing a tenth of its level whatever the building's figure - so a mod adding a second one puts provinces out of every supply area once a game is loaded: BlackICE's `extra_infra` left 1,603 provinces in none, and the original's AI crashed on them (`reversing/findings/FINDINGS-extrainfra.md`, sections 2 and 6) |
| here | the first building of the infrastructure kind, `infra`: what was built of it, damaged or not (`CSupplyFigures.MostInfrastructure`; `mechanics/supply.md`, section 2) - what BlackICE's DLL makes it with `fixMaxInfrastructure` |
| the difference today | none in Their Finest Hour, which has one infrastructure building; BlackICE's areas as with its DLL |
| a mod loses | a second infrastructure building's figure standing in for the first's, which no mod is known to want |
| why | so that BlackICE plays without its DLL, the normal engine giving none of BiceLib (`docs/enhanced.md`, *Asked for*) |
| to undo | store the last building written's figure in `CSupplyFigures.MostInfrastructure` |

## 30. The install's Lua runs on LuaJIT, and `pairs` walks a table in LuaJIT's order

**Decided** 2026-11-19, on a measurement of both interpreters: "I think the 'pairs' issue is not that big of a deal.
Since the pairs order is unspecified, no code should rely on it" - and the original's Lua kept "with a switch".

| | |
| --- | --- |
| the original | runs its scripts on Lua 5.1.4, its `lua5.1.dll`, whose tables hand `pairs` and `next` their entries in an order that follows from 5.1.4's hashing and layout |
| here | LuaJIT's interpreter, set to answer the C interface and the scripts as 5.1.4 does - integers 32 bits wide and rounded to even, `math.mod` and `string.gfind` - but laying out its tables its own way (`native/luajit/readme.md`; `mechanics/lua.md`, section 1) |
| the difference today | a script that keeps the first of several equal choices may make another: within a new game's first days - the fourth in the base game, the second in BlackICE - the ministers post the same commands in another order and the production minister picks other brigades among equals, so a game plays otherwise from the same seed. Every check against the start save comes out the same under both. A fraction given to `string.sub`, `string.byte` and a few others is cut where 5.1.4 rounds it, which no script was found to do |
| a mod loses | nothing a script may rely on - Lua leaves the order unspecified - but a mod's AI may choose otherwise among equals than it does in the original |
| why | about a fifth of the AI's Lua time: about 256 ms of a BlackICE day to 199, measured back to back on one build (`docs/performance.md`, section 2) |
| to undo | `OPENHOI3_LUA=5.1.4`, the client's `--lua=5.1.4` or the savegame tool's `lua=5.1.4` runs the original's Lua 5.1.4, still built beside it (`CLuaInterpreter`); making it the default is `CLuaInterpreter.Chosen`'s fallback |

## Stricter, with the same meaning

Not departures - a file means what it meant - but a mod's author will see them, so they
are listed.

- **A mod named that is not there is said**, and a `.mod` file's key the original does not read - the old `extend` and
  `replace` among them - is reported. The original plays without the mod, and drops the key, in silence; so does this
  (`docs/mechanics/mods.md`, sections 1 and 3; since 2026-11-16).
- **A key nothing recognises is reported.** The original drops it without a word; this
  reads the rest of the file the same way and says which key it did not know. So far in
  the interface files (`docs/mechanics/interface.md`, section 2), in unit types, historical
  models and orders of battle (`docs/mechanics/units.md`), in events and decisions
  (`docs/mechanics/script.md`), and wherever a loader knows the whole of what a block may hold.
- **A modifier block's keys are the original's closed list** - its 107 engine keys, the 32 mission efficiencies under
  each order's own key and a `suseptibility_<tag>` for each faction, 142 in Their Finest Hour - and a key outside it is
  reported and dropped wherever a modifier is read: laws, ministers, policies, static, event and triggered modifiers,
  strategic resources, governments, factions and buildings. The original logs it, or for a building drops it in
  silence (`docs/mechanics/modifiers.md`, section 2; since 2026-11-14). The base game, Their Finest Hour and BlackICE
  use none.
- **A theory or a practical named in a technology's `allow`** is reported and left out, as the original leaves it out
  silently: it is no condition there (`docs/mechanics/research.md`, section 5; since 2026-11-14). Neither Their Finest
  Hour nor BlackICE writes one.
- **An order of battle's `leader` naming no declared leader** is reported and passed over, where the original passes
  over it in silence (`docs/mechanics/leaders.md`, section 5; since 2026-11-14).
- **A brigade of a type nobody declares is left out**, and said to be. The original keeps
  it with no type, which nothing can then use. It is still given its number, so the units
  after it are numbered as the original numbers them.
- **A texture named with the wrong extension is found**, as it is in the original, and a
  path with a backslash that should have been doubled still names the file it names
  there. Neither is reported.
