# Units on the map: counters, and putting a finished unit there

What the original shows of a unit on its map and how a finished one gets there, written
as rules this project can implement. No addresses and no offsets. Each rule says how sure
it is, in the fact base's vocabulary - `confirmed`, `likely`, `inferred` - or **measured**,
meaning counted here against the files of a Their Finest Hour install on 2026-10-09.

Sources: `reversing/findings/FINDINGS-counters.md`,
`reversing/findings/FINDINGS-deploymenu.md`, for the command a finished unit is placed
by `reversing/findings/FINDINGS-buildqueue.md`, and for the buildings on the map
`reversing/findings/FINDINGS-seaair.md`. It was reviewed in play on 2026-10-09,
with a screenshot of one of the original's counters, which section 2 is measured
against; the last section says what a further look would settle.

Where this project deliberately does something else, or had nothing to go by, the
paragraph is marked **Ours**.

## 1. Where a province's units stand

A province has one point that stands for it - `map.md`, section 4: the centre of its
bounding box unless `positions.txt` gives it a `unit` point, or for a sea zone a
`text_position`. That file is read now (2026-10-09), by a reader of its own, since it is
six megabytes of which those two keys are a small part.

**A unit stands at its province's `unit` point.** Settled on 2026-10-09: "The unit
position is part of the positions.txt." The file has that one point for a province and no
other for units (measured: 2754 `unit` blocks, and beside them only the text's and one
`city`).

The original keeps **five points to a province**, one for each stack of section 3, and
stands a counter at its stack's: the first for a land unit or a fleet with no path, the
second for one with a path, the last for an air unit, and the two between for the sides of
a battle. `confirmed` (`reversing/findings/FINDINGS-movement.md`, section 9). With one
point in the file the other four are worked out from it - read on 2026-10-10, after a
screenshot showed the original's bombers lying over the stack they bomb: `confirmed`

| stack | where it stands, from the province's point |
| --- | --- |
| standing | on it |
| on the move | 2.5 east and 1.0 along the map's y |
| the two sides of a battle | on it; **at sea only**, the attackers 2.4 west and 2.4 south of it, the defenders 2.4 east and 2.4 north |
| **air units** | **on it**, as the standing stack - and drawn after it, so over it |

On the original's screenshot the bombers' counter lies a quarter of a counter south and east
of the defenders' top counter: that top counter is the stack's point moved 0.3 west and north
for each of the four counters under it, and the bombers' is on the point itself.
**Here until 2026-10-10 the stack of aircraft stood a counter's width east and north of the
standing one** - a guess, and review in play showed it was wrong. **The battle stacks are built
since 2026-11-14** (section 3): until then a unit in a battle stood in the standing or the
moving stack, an attacker with a path among the moving, and a sea battle's two fleets in one
stack. The stacks are drawn in the original's order of the five - standing, moving, attacking,
defending, air - so on land a battle's sides lie over the standing stack on the same point.
**The stack of units on the move is drawn 2.5 map units east and 1.0 north of the standing
one**, the reading's figure, and over it - by the decision of 2026-11-14: "Use the
originals, that is the correct one." Until then it was drawn 2.6 east and 1.5 north, as
measured below, the measure kept until the two had been chosen between. On a
screenshot of the original given on 2026-10-09, a division setting off lies
3.7 map units east and 0.5 north of the top counter of the stack it leaves; that counter
is itself 0.3 west and north for each of the four under it. The count of the stack is
read off its edges, so the figure is good to a few tenths. Before the screenshot it was a
guess, first of a whole counter's width - "Its way too big now" - and then
of three tenths of one.

**The default point was a pixel out until 2026-10-09** for a province whose bounding box
is an even number of pixels across or high: the centre is rounded up (`map.md`, section
4).

**One map unit is one pixel of the province bitmap.** `inferred`: nothing read says so,
but the offsets between the counters of a stack and a counter's own height only come out
at a sensible size that way, and the original subtracts pixel positions to aim a unit's
figure.

## 2. What a counter is made of

A counter is declared, not built in: `interface/mapitems.gfx` has an `objectTypes` block
with two `CounterType`s, **`unit_counter`** for a far view and **`unit_counter_close`**
for a near one. Each unit has one of both.

| key | |
| --- | --- |
| `textureFile` | the plate |
| `textureFile2` | the mask: red where the owner's colour goes; blue and green for a selected counter's ring |
| `textureFile3` | a strip of `noOfFrames` size marks, `X` to `XXXXXX` |
| `scale` | half a counter's height, in map units |
| `level_scale` | how much a counter grows for each step of size |
| `font`, `size` | the font of the two figures, and their height |
| `shieldtype` | that the counter carries its owner's flag, and the flag's width and height in `scale`s |
| `offset`, `offset2`, `offset3` | where the flag, the two figures and the name go |
| `color1`, `color2` | two of the colours a selected counter's ring may be |
| `textureFile4`, `effectFile`, `effect`, `delayAfterAttackColor` | read, and not used here |

`confirmed` for which key is which texture and for the colours; `likely` for the lengths.
A key that is none of these is reported, where the original drops it.

**The picture is put together by a shader that ships as plain text**,
`gfx/FX/counter.fx`, so what it does is not a reading of the executable:

    the plate's colour times the owner's colour, where the mask is red
    the unit type's picture over that, as solid as the picture is
    the size mark over that, as solid as the mark is
    the whole as solid as the plate

and for a selected counter the mask's green, in the selection's colour and pulsing, laid
over through the mask's blue; and where the counter has a mark, its owner's colour in the
corner beyond u + v = 1.6 is the mark's, with a black edge between 1.57 and 1.6.

**Since 2026-11-07 a counter is drawn through `counter.fx` itself**, translated for the
canvas, as the original hands it its parameters (`reversing/findings/FINDINGS-counters.md`,
section 10.4): `confirmed`

| parameter | value |
| --- | --- |
| `BackgroundTex`, `MaskTex`, `SizeTex` | the type's three pictures |
| `CounterTex` | the unit type's picture, below |
| `SizeFrame` | the counter's size, 0 to 5 |
| `SizeOffset` | the share of its texture the size strip fills, over its frames |
| `CountryColor` | the owner's colour |
| `vOtherColor` | the mark's colour, below; alpha nought where there is none |
| `SelectionColor`, `Selected` | white on a selected unit's counter, the type's `color2` on one under it, and 1; else 0 |
| `SelectionIntensity` | 0.5 + 0.5 \|sin 2t\|, t the clock's seconds |

**The three marks** colour a counter's corner, on the player's own units alone, the first
that holds: `confirmed`

| mark | colour | where |
| --- | --- | --- |
| short | red (1, 0, 0) | given under a twentieth of its supplies or of its fuel - a constant of the executable's, which no define moves |
| upgrading | orange (1, 0.5, 0) | a brigade of it being changed into another type |
| attack delay | yellow (1, 1, 0) | an attack delay left, in no battle and not falling back |

The orange one is a unit's line of brigade upgrades (`upgrades.md`), which shows after red and before yellow.
`delayAfterAttackColor` is read by nothing in the original: the yellow is its own.
*Ours:* the original's texture is a picture padded to a power of two, so its strip of 384 is
0.75 of its texture and `SizeOffset` an eighth; a texture here is the picture alone and
`SizeOffset` a sixth - the same frame either way. The effect's vertex shader and the lift a
tilted camera gives a counter are not used: the canvas places the counter where the 2D view
has it.

- **The unit type's picture** is the sprite `GFX_small_counter_<key>` on the far counter
  and `GFX_map_counter_<key>` on the near one, `<key>` being **the type of the unit's first
  brigade, ship or wing**. `confirmed` A unit's sub units are kept in order of their types' `priority`, highest
  first and equals as they came, however the file lists them (`units.md`, section 3, *The order of a unit's sub
  units*) - so the picture is of a brigade of the highest priority: BlackICE's infantry (83) and not the division
  headquarters (1) its files list first. That was missing here until 2026-11-18, when the counter showed the
  headquarters. `confirmed`
- **The size mark** is the frame of the strip for the unit's rank: `confirmed`

  | unit | mark |
  | --- | --- |
  | a theatre | `XXXXXX` |
  | an army group | `XXXXX` |
  | an army | `XXXX` |
  | a corps | `XXX` |
  | a division of more than one brigade | `XX` |
  | a division of one brigade | `X` |
  | a fleet | `XX` |
  | an air unit | `X` |

- **A counter's own scale** is `(size - 1) * level_scale * counter_scale + 1`, `size` counting
  from 0 for `X`: with the far counter's `level_scale` of 0.2 and `counter_scale` at its 1, a
  lone brigade is drawn at 0.8, a division at 1 and a theatre at 1.8; at 2, a theatre at 2.6;
  at 0, every far counter a division's size. The near counter gives no `level_scale`, so every
  near counter is the same size. `confirmed` (`reversing/findings/FINDINGS-counters.md`,
  section 10.3). `counter_scale` is the player's settings.txt [mod: settings], 0 to 2 by the
  game options' slider - "Counter hierarchy scale" - and an account of the original's, 2026-11-07,
  is the same: each level up the chain is bigger. It is applied as it is read and from Apply
  on, the original's every counter being drawn and hit-tested through it.
- **A counter is `2 * scale * its own scale` high** and as wide as its plate's picture
  makes that. `likely`: it is how the original tests a click against one.
- **`offset`, `offset2` and `offset3` are in tenths of `scale`**, times the counter's own
  scale, and so are the sizes of the two texts. Their three numbers are east, up off the
  map, and north. `likely` for the tenths; north is **measured** (below).
- **The flag** is on the near counter only, which is the one with a `shieldtype`: its
  middle at `offset`, `shieldtype`'s two numbers wide and high in `scale`s.
- **The two figures** start at `offset2`, at the size `size`.
- **A name** starts at `offset3` on the near counter, at the size 3.5, by the player's
  `show_unit_names` [mod: the player's settings.txt, the game options' box]: **off - a new
  install's default - the unit's leader's, and nothing for a unit with none; on, the unit's
  own**; cut to its first eight letters either way, and `???` where the player knows the unit
  under 5 whatever the setting (section 3). **The eight count a colour escape's two characters**, and
  the name is drawn through the counter's font, which obeys the escape as every text does
  (`interface.md`, section 6): BlackICE's `§Y7th Cavalry Brigade (Mechanized)§W` is a yellow `7th Ca`.
  Until 2026-11-18 the counter drew its name plain, the section sign and the letter with it. `confirmed` for the rule
  (`reversing/findings/FINDINGS-counters.md`, section 4; `CUnit_RefreshCounterName`), `likely`
  for the size. **It is written again the moment it changes**: a rename and a change of leader
  write that counter's (`reversing/findings/FINDINGS-unitrename.md`, section 4;
  `reversing/findings/FINDINGS-leaderassign.md`, section 9), and Apply with the box changed every
  counter's the next frame (`reversing/findings/FINDINGS-settingswindow.md`). Built 2026-11-14
  (`CCounterText.Name`, `CUnitCounters.ShowUnitNames`): read at the start and taken from Apply at
  once, and a change of leader is looked for each frame while the setting is off. `--unitnames=no`
  or `=yes` gives the counters the other setting for a run, the file left alone.
- **Only the top counter of a stack carries a flag, figures and a name.** `confirmed`

**Measured on a screenshot of the original's near counter**, given on
2026-10-09 - an armoured division, `2 - 2`, named `1. Panze`. Its unit picture gives the
scale, 31 pixels to a map unit, and against that:

| | read | measured |
| --- | --- | --- |
| the flag's middle | 0.625 east, 1.25 south | 0.66 east, 1.29 south |
| the flag, inside its frame | 1.125 by 0.75 | 1.15 by 0.76 |
| the figures' left end | 1.75 west | 1.75 west |
| the foot of the figures' digits | 1.6 south | 1.59 south |
| the name's first letter | 1.45 east, 1.375 north | 1.56 east, 1.35 north |

So north is the way it was taken to be, and three things the reading did not give:

- **A text is anchored by the start of the line its letters stand on.** The figures'
  left end and the foot of their digits are on `offset2`.
- **The name is written turned a quarter**, running south down the counter's right edge
  from `offset3`, the tops of its letters to the east.
- **A line of text is nine tenths of the size its type gives.** The font is 18 units to
  the line with digits of 11; the digits come out 0.615 map units high, where a line of
  1.125 would make them 0.69, and the name's capitals 0.48 where 0.53. That is a line of
  20 for a size of 18 - what the original divides by was not read, and one font is all
  there is to go by.
- **The flag has a dark frame**, about a fortieth of `scale` wide.

~~*Ours:* **the name is the unit's own**, as with
`show_unit_names` on - which is how the reference game is set, and there are no
leaders to name and no settings to choose by. Decided on 2026-10-09: it can stay
that way until the settings are implemented (`docs/compatibility.md`, entry 7).~~ The setting
is followed since 2026-11-14, as above. **Checked with `--click` that day**, near Berlin: with
the reference settings' `show_unit_names=yes` the counters say `12. Infa`, `23. Infa` and `Ob.kdo.`;
with `--unitnames=no` `Böhme`, `Busch`, `Guderian` and `von Böhm` - the selected division's
`Busch` the unit panel's leader, the positive control. A unit type
with no counter sprite of its
own is shown by the `unknown` one of the same family.

### The pointer of a unit on the move

Noted on 2026-10-09, with a screenshot: "In the original a moving unit also has a
small green pointer at all times." It is the `counter_arrow` `billboardType` of
`interface/mapitems.gfx`, which every counter has one of:
`reversing/findings/FINDINGS-counters.md`, section 9.

- **Every unit with a path shows it**, selected or not. `confirmed`
- Its picture is a strip of `noOfFrames` frames; the first, green, is the one a marching
  unit shows. `confirmed`. The second is red and its use was not read.
- It is drawn in a square `2 * scale` across - 2 map units in the base game - **whatever
  the counter's own scale**: the original hands the pointer that scale through a setter
  its draw never reads, so the pointer is always drawn at one. `confirmed`, corrected on
  2026-11-07 (`reversing/findings/FINDINGS-billboardfx.md`, section 2) - this said "times
  the counter's own scale", which made a far counter's pointer up to 1.8 times as large.
- **It stands `offset` from the middle of the counter, in the direction the unit is
  going, and points that way** - 4.5 map units in the base game, not scaled either. The
  direction is from the unit point of the province the unit is in to that of
  the next province of its path, the short way round the map. `confirmed`, and the
  screenshot agrees to the degree: the pointer lies on the line it points along. It is
  about a seventh further from the counter there than 4.5, which is not explained.

*Ours:* the pointers of a stack are drawn with their counters, so one under the top
counter's can be hidden by it.

## 3. Which units have a counter, and how they stack

**A fleet in port and an air unit at its own base have no counter.** The original shows
those by the province's port and airfield: section 9. Nor has an air group aboard a carrier,
nor a land unit aboard a fleet in port; **a land unit aboard a fleet at sea has its own**, among
the moving counters while the fleet has a path (`transport.md`, section 12), and one aboard an air unit among the
air units' (`transport.md`, section 13.6). Every other unit
that is somewhere has one. `confirmed`

A province sorts its counters into **five stacks**, by what the unit is doing: `confirmed`
for the order of the tests.

| stack | |
| --- | --- |
| air | an air unit, or a land unit carried by one |
| attacking, defending | a unit in a combat, by its side |
| moving | a unit with provinces left to move through, or a land unit aboard a fleet that has |
| standing | every other |

**Which battle stack** (`reversing/findings/FINDINGS-mapstacks.md`, section 1, `confirmed`): the
original asks one battle - the first of the province's kind, a land battle on land and a sea battle
at sea, fought in the province the unit stands in, or where there is none the unit's own first
battle - whether the unit's owner is among its attackers still in the fight. Yes, the attackers'
stack; no, the defenders'. So an attacker on land, standing in its own province where there is
usually no battle, goes by the battle it is in; and **an attacker standing in a province that is
itself attacked stands with the defenders**. Built 2026-11-14 (`CMapCounters.StackOf`,
`IsAttackerHere`), and seen on a reference save of 2 November 1937: a Japanese fleet south-west
of the Western Ryukyu Sea's point and the Chinese fleet it attacks north-east, where both had stood
in one stack.

**A stack shows its top counter, or its top six** each drawn 0.3 map units west and 0.3
north of the one under it. `confirmed`. Which it is depends on how well the player knows
the units: **how well the player knows a unit** is 9 for its own units and its friends' - a
country of the same real faction, or one allied with it - and otherwise what it knows of the
province the unit stands in (`intelligence.md`) [exe]. A stack goes by its top counter's: **under
2 it is not drawn and cannot be clicked, under 3 it shows one counter**, not fanned. Each counter
goes by its own unit's: under 3 the unknown picture (`GFX_map_counter_unknown`, the small one far
[mod: interface/counters.gfx]), under 5 the name `???`, under 7 the figures `?` (section 4).
`confirmed` (`reversing/findings/FINDINGS-counters.md`, sections 5 and 6). *Ours:* the original
takes the level of the first counter of its stack's list, here the top one; and it copies a unit's
level as the unit enters a province, where here it is worked out as the counters are made again -
when units move, and each day after the day's intelligence. Built since 2026-11-07: until then there
was no fog of war, and every stack showed up to six.

**Within a stack the higher rank is on top.** Confirmed on 2026-10-09: "Correct." - and
read since, 2026-11-14 (`reversing/findings/FINDINGS-mapstacks.md`, sections 2 and 3,
`confirmed`). A province keeps its units with a counter in one list, sorted as each comes in:
**the player's own first**; then an air unit before a land unit it carries; then in a land
province everything else before a fleet, and at sea a fleet first; then **the higher rank
first** - a theatre, an army group, an army, a corps, a division (a lone brigade among them),
a fleet, an air unit. Of two it does not part, the one that came into the province first
stands first. **The first of the list is on top of its stack**, and the rest under it in the
list's order - **until a unit of the province is selected: then its place starts the list**,
so it is on top, the units after it under it and those before it at the bottom. The click that
steps through a stack takes the unit after the selected one in the same list, which is then on
top in its turn. Built so since 2026-11-14 (`CProvinceUnitOrder`, `CMapCounters.TopDown`), and
seen in a new game: Berlin's stack with the theatre's command on top, and Heeresgruppe A on top
after the second click. *Ours:* this game keeps no record of when a unit came into a province,
so of two the list does not part the one made first stands first; the original's list is one
for the province, and here each stack is ordered by it; and with several selected the cursor is
the place of the last selected in the province, as the original sets it at each selection.
Until 2026-11-14 the player's own units were not put first, of two of a rank the one made later
was on top, and a selected unit was brought to the top with the others left as they were.

**Only the counter on top of a stack takes a click.** `confirmed`: the original asks a
near counter whether the mouse is on it only where it is the top one of a stack. An
account of the original, 2026-10-09, of what follows from it: "You click the top unit only, if you hit
the lower ones the click goes through to the province. Then if you keep clicking the unit
it cycles through the units in the province." So a click on the top counter selects its
unit, and each further click the next unit of the stack: the next in the province's list
(above), round to the first again - read since (`selection.md`, section 2.1). *Ours:* the
original steps through the province's units of the selected one's owner, here the stack's. **The stack is stepped through only
from the one selected unit's own counter**; with several selected a click takes the top unit,
only the player's own units are selected at all, and Shift adds the next one instead
(`selection.md`, section 2.1, read 2026-10-16).

## 4. The two figures

A counter's two figures are its unit's attack and defence, written `1 - 2`. Each is a
sum over the unit's brigades, divided, and held to 9: `confirmed`

| | attack | defence |
| --- | --- | --- |
| land | `(soft_attack + hard_attack) / 6` | `(defensiveness + toughness) / 16` |
| naval | `sea_attack / 25` | `sea_defence / 25` |
| air | `air_attack / 10` | `air_defence / 10` |

- The sum is over the brigades that still count towards their unit (`units.md`), and **a
  brigade's `toughness` goes into it twice** - a slip of the original's own, in how it
  adds a brigade's figures into its unit's.
- **The division is of whole thousandths, cutting; the result is then rounded to a whole
  number, a half going to the even one.**
- A figure the player does not know - an intel level under 7 - is written `?` (section 3).

They are for the eye. Nothing fights by them.

## 5. The alert

The original has sixteen alerts, each an icon in a row under the top bar. The one for
units waiting to be placed is `alert_deployable_units`, the second of the sixteen.

- An alert that comes up gets an `alerticon_window` of `interface/alerts.gui`, whose
  `alerticon` shows **the frame one more than the alert's number**, frames counting from
  one and alerts from nought - the second frame for this one. `likely`
- The first alert up stands at the `positionType` `alerticon_startposition`, and each
  later one `alerticon_offset` further along: alerts stand in the order they came up.
  `likely`
- **A click on this alert's icon opens the deploy menu**, closing any other side menu
  first. `confirmed`

All sixteen are built: `alerts.md`.

## 6. The deploy menu

The window `sidebar_deploy` of `interface/sidebar.gui`: a title, the list
`deployment_queue`, and `close`. `confirmed` throughout, but where marked.

**The menu, the order of battle browser and the unit panel stand side by side**: the menu at the screen's left, 256 wide by
its file, the browser at its right edge and the panel at the browser's - each pushed aside by those before it, and back to
the edge when the menu closes. The original's screenshots show the three up at once (given 2026-11-13); until
then the panel gave way to the menu here, which left the browser shut and the drag below impossible. *Ours:* the
arithmetic, from the screenshots and not read.

**Its list is the country's units waiting to be placed**, in the order they were finished,
each a `deployment_entry` - and since 2026-10-15 the buildings waiting after them, a line each with the building's name
and no pictures, placed by a click where the map shows green (`buildinglines.md`, section 6):

| element | |
| --- | --- |
| `name` | the unit's name |
| `counter_type` | the sprite `GFX_counter_<type of its first brigade>`, where there is one |
| `counter_size` | the frame of the mark for its rank (section 2), counted from one |
| `select` | chooses it |

The title is the text of `DEPLOY_UNITS`. **When the list is empty the menu closes itself.**

**Choosing a unit** makes it the one being placed and turns the list into **the units in
the field it may join**, under the title `DEPLOY_SEL_TARGET`. Each is a
`deployment_target_entry`: the same, for that unit, with `location` the name of the
province it is in. They are listed by rank from the theatres down, then the fleets, then
the air units, and within one kind in the order they were made. A unit in the field is
listed where the new one could be placed in the province it stands in, and:

| the new unit | may join |
| --- | --- |
| land, of one brigade | a land unit with fewer brigades than the country may put in a division |
| land, of more | a unit of a higher rank; and unless that is a theatre, one with fewer than five land units under it |
| a ship | a fleet |
| a wing | an air unit - but a wing of ordinary aircraft not a carrier's air group |

A headquarters is a land unit of one brigade, so a lone brigade may join one. **When
nothing may be joined the plain list is shown again**, and the unit is still the one
being placed.

**Picking a line of that list** joins the unit to it (section 7 of `buildqueue.md`). **A
click on a province the unit may be placed in** puts it there by itself. Either way the
list goes back to the units waiting.

*Ours:* the provinces the unit may be placed in are tinted green on the map while one is
being placed - reviewed in play on 2026-10-09: "Looks good". That the original marks them is
how the game is remembered; how was not
read, nor was the click on the map that places a unit - only the menu's side of it. While
a unit is being placed a click on the map is for the province and not for a counter on it.
**A unit's line may be dragged into the order of battle browser** - never onto the map, which this said until
2026-11-13 (`reversing/findings/FINDINGS-deploymenu.md`, section 6, `confirmed`). The left button going down on a unit's
line marks it - a building's line, or one of the target list, marks nothing - and while the menu is up, each frame, a marked
line is forgotten with the browser closed, and with it open is handed to it once the pointer is inside the browser's window,
no button asked: a unit of one brigade, ship or wing as that one, any other as the unit. The browser's own drag rules then
hold (`oobbrowser.md`, section 6), the rows lit by their test with the province left out. **The drop** on a row its test
allows: for a unit, `CDeployUnitCommand` with the row's unit its target and then the browser's own `CAttachUnitCommand`; for
a lone brigade, ship or wing, only where it may be placed in the row unit's province, the deploy command and then the
browser's `CTransferSubUnitCommand`. The deploy command so posted puts the unit where the original's join puts it - the row
unit's province, or the nearest province of the home area where it may go - and asks nothing more of the target, as the
original's asks nothing; the second command then finds the work done. A drop on no row posts nothing, and the line stays.
Built 2026-11-13 and tested, and tried on screen with `--click` the same day: a division ordered, finished by the
console's `inc`, dragged from the menu onto a corps' row, which it joined, the player told. *Not
established*, so not built: what the original does where the join finds no province at all - the attach would hang the
waiting unit under the row by the reading, not traced - and whether the player is told `TRANSFEREDSUBUNIT` twice. Its
rule about a carrier's air group and the room aboard was not read through, and is not
asked.

## 7. Ours

- **`OpenHOI3.Interface.Map`** has what can be checked with no engine: which units have a
  counter and how they stack (`CMapCounters`), what one says (`CCounterText`), where its
  parts go (`CCounterLayout`) and what it hands `counter.fx` (`CCounterShading`).
  `OpenHOI3.Sim.Units.CUnitFigures` has the two figures, and
  `OpenHOI3.Sim.Construction.CDeployment` who may join whom.
- **`src/Map/CUnitCounters`** draws them, over the map and in its coordinates; a click on
  the top counter of a stack selects a unit (section 3), which then wears the ring.
- **What is on the map goes by how high the original's camera would be.** Read on
  2026-10-09 (`reversing/findings/FINDINGS-camera.md`), after an account of play that
  the original has "2 modes, the close and far", and that "in the far only the map shows,
  no counters or buildings":

  | the camera's height | |
  | --- | --- |
  | under `counter_distance`, 100 in a new install and the player's own in `settings.txt` | the near view: figures, or with `always_counters` the near counter |
  | from there to 500 | the far counter |
  | **from 500 up** | **the far view: the map and nothing on it** - no counter, no building, no line of a hierarchy |

  The camera goes from 40 to 1000 and starts at 330. It hangs straight over the map - with
  counters only it looks straight down - and sees 0.785 radians from the top of the screen
  to the bottom, so from a height `h` it sees `0.828 h` map units, and **a map unit is
  `screen height / (0.828 h)` pixels**: on a screen 1080 high, 2.6 at a height of 500, 13 at
  100, and 32.6 at 40. `confirmed` for the camera and for 500 and `counter_distance` as
  what stops a click on a counter or a building and what draws the hierarchy; that the
  far view *draws* none of them is that account's, the compare that hides them not
  having been found. The screenshot of a near counter (section 2) is 14.6 pixels to a map
  unit, a height of 89.

  *Ours:* the map here is drawn at a zoom, so each rule is asked of the height that zoom
  would be. The zoom goes as close as the original's lowest height; the other way it goes
  on past the original's highest, until the whole map is on screen. **A game opens over the
  player's capital at the starting height, 330**, a new game and a loaded one alike - by the
  decision of 2026-11-12, where it had opened on the whole map; where the original
  puts its camera on a load was not read. The console's `tag` and the screens made again keep
  the camera where it was.
- **The zoom eases, as the original's does** (`reversing/findings/FINDINGS-camerazoom.md`; `CMapZoom`), built
  2026-11-12 on a report from testing that ours was not smooth - it had been ×1.25 a notch at once. A wheel notch
  sets a **target height** of the current height less `0.2 × zoom_speed / 100` of it towards the map, or more
  `0.4 × zoom_speed / 100` away [exe the figures; mod: settings.txt `zoom_speed`, 50 by default - a tenth in, a fifth
  out], held to the limits, at most one notch a frame; and, unless the target landed on a limit or the pointer is within
  six pixels of an edge, it **pulls the look target** `(4/15) × zoom_speed / 100` of the way - 0.133 by default -
  towards the point under the pointer, **in or out**. Then **every frame** the height and the look point each close
  **0.197 of the gap** [exe] - a frame and not a second, as the original's: about half the way in three frames and nine
  tenths in ten (`docs/enhanced.md`). A drag or the keys carry the target along; a jump - a province centred, the
  minimap pressed - ends the position's ease, and with a height given the height's. `use_legacy_camera` [mod:
  settings.txt] takes the height at once and pulls nothing. *Ours* as before: the highest height for the wheel is where
  the whole map is on screen, past the original's 1000; the lobby's map does not zoom, as the original's front end's
  does not.
- **The near view is always counters.** The original's is counters or 3D figures by a
  setting. There are no figures here.
- **The counters are worked out again** once a day, whenever a unit of the player's is
  placed or ordered, and each hour that a unit is on the march or has just come off it -
  the hour it arrives is the one its counter has to change province in - not every frame.
  **And in the frame after any command has run, paused or not** (`CSimulation.Executed`): a unit renamed,
  reorganised, merged or split shows on its counter at once, as in the original - decided on 2026-11-18,
  when ours waited for the next hour.

## 8. The hierarchy of a selected unit

Read out of the original on 2026-10-09, after an account of play that it "has hierarchy
lines and highlight" and that "they can be toggled with h":
`reversing/findings/FINDINGS-hierarchy.md`. `confirmed` unless marked.

- **While a land unit is selected, lines join it to its command**: one from the unit to the
  unit that commands it, and one from every unit under it, at any depth, to its own
  commander. A line runs between the two provinces' unit points, the short way round the
  map.
- **No line** for a unit in its commander's own province, for a fleet or an air unit, or
  for a unit aboard a ship or aeroplanes.
- **The colour says how far under the selected unit a line's unit is:**

  | the line of | colour |
  | --- | --- |
  | the selected unit itself, up to its commander | green, 0 120 0 |
  | a unit directly under it | blue, 20 50 200 |
  | a unit under one of those | a paler blue, 100 100 210 |
  | anything further down | grey, 200 200 200 |
  | a unit out of its commander's radio range, wherever it stands | dark red, 128 0 0 |

- **The width is the unit's rank**: half of it is 0.2 of a map pixel for each step of the
  size mark its counter carries (section 2) - 0.8 wide for a division of several brigades,
  1.2 for a corps, 1.6 for an army, 2.0 for an army group.
- **The picture** is `gfx/mapitems/hierarchy_line.tga`, 64 by 64: arrowheads in a band in
  its left half, a broken line in its right. A line in reach is the left half, tinted, a
  fifth of the picture to each stretch as long as the line is wide, **and it runs**: the
  arrowheads go from the commander's end to the unit's. A line out of reach is the right
  half, still. What the shader does is in `gfx/FX/hierarchy.fx`, which ships as text - and **since 2026-11-07 the lines
  are drawn through it**, translated, on the map's surface (`mapdraw.md`, section 4), the 2D drawing kept for a map
  drawn flat. **Across the seam** whichever end lies further east is moved a map's width west, and the original draws
  the lines where they are and a map's width east, never west (`reversing/findings/FINDINGS-hierarchy.md`, section 7);
  the end moved was the commander's, east, until the same day.
- **How fast it runs**: the original moves the picture 0.006 of its length a frame,
  whatever the frame rate. *Ours:* 0.36 of its length a second, which is that at sixty
  frames.
- **The H key switches the lines off and on.** It flips the setting `render_hierarchy`,
  which the original keeps in the player's settings file and also offers in its settings
  menu as "Show hierarchy on map". *Ours:* on when a game starts and kept nowhere - there
  are no settings yet.
- The original draws the lines only where counters are drawn - with the camera further
  than `counter_distance`, or with `alwaysCounters` on - and never from higher than 500.
  *Ours:* whenever a unit is selected; the near view here is always counters (section 7).
- **The lines are made again** when a unit of the hierarchy is in another province, or the
  selection or the order of battle changes. *Ours:* each frame, from the units as they are.

**The rings.** Apart from the lines, and whether or not they are switched on: `confirmed`

| counter | ring |
| --- | --- |
| the selected unit | white |
| the selected unit, where an AI commands it or a unit above it | the counter type's `color1`: green |
| **every unit under the selected one, at any depth** | the counter type's `color2`: a pale blue-green, `0.28 0.8 0.82` |

- Every counter of a stack that is one of those wears it, not only the top one, and each
  pulses as the selected one's does (section 2).
- A unit under the selected one that is selected itself keeps its own ring.
- The first reading missed the ring of the units under it; an account of the original and a
  screenshot of 2026-10-09 put it right. On that screenshot the selected headquarters wears
  green - an AI commands it there - and the three counters under it the blue-green.
- The original gives the rings out when a unit is selected and takes them back when it no
  longer is, so a unit attached under it in between has none until then. *Ours:* they
  follow the order of battle as it is.
- **The ring is all there is under a selected counter.** The original has a mark for a
  selected unit, `selection_projection` of `interface/mapitems.gfx` - a picture laid on the
  map under the unit's stack that grows, turns and stays until the unit is deselected,
  following it as it marches - but **it makes it only where `alwaysCounters` is off**, for
  the units' figures [exe] (`reversing/findings/FINDINGS-movement.md`, section 13.8, read
  2026-11-14). With counters, which is the install's setting and the only one here
  (`docs/compatibility.md`, entry 12), it makes none, so none is drawn here
  (`movement.md`, section 7).

A unit out of radio range is any link up its chain of command letting less than all of its
command through (`leaders.md`, section 8); its line is dark red and drawn from the broken half
of the picture, standing still. Built on 2026-10-11 and checked with `--click`: the Soviet 42nd
Corps and its two divisions, out in Karelia, have such lines. The green ring is built since
2026-11-21: a selected unit with an AI agent on it or on any unit above it - or a plan switched on, which a
player's hand-over sets at once where the agent comes with the AI's next pass. Looked at with `--click`.

## 9. Buildings on the map, the port and the airfield

Read out of the original on 2026-10-09 for the port and the airfield, which are how a fleet
in port and an air unit at its base are seen and picked:
`reversing/findings/FINDINGS-seaair.md`, section 6, and for the capital marker
`reversing/findings/FINDINGS-citypoint.md`. `confirmed` unless marked.

**Every plain `billboardType` is drawn through the install's own `billboard.fx`** since
2026-11-07, translated for the canvas (`reversing/findings/FINDINGS-billboardfx.md`): the
buildings here, the moving unit's pointer (section 2), the battle markers (`combatview.md`)
and the air map mode's large pictures and icons (`airmap.md`). Every plain type shares the
one effect kept under the key `Billboard`, the file `gfx/FX/billboard.fx` unless a type
names another, technique `standard` by name; an unselected picture is drawn at a
`vMultiplier` of one - a selected one at 1.5, which nothing selects - and the quad is always
a square. *Ours:* a picture's frame is chosen by the part of its texture drawn rather than
by `vXOffset`, which stays nought, so one material serves a layer; and a battle marker's
number is written over its badge in an item of its own, the effect not being for text. The
pictures the original moves after hanging them - the large air and naval base and
anti-aircraft pictures, the battle and convoy icons, and a building's `building_nudge` - do
reach the place they are moved to, which the reading had left open: two
screenshots of the original's air map mode, 2026-11-07, have the air battle icon east of a
province and the bombing icon south-west of it in one and north-west in the other - 3 east,
3 south and 3 north, as read. Here they stand there too.

- **A province shows a picture for every building it has any of that `buildings.txt`
  declares `onmap`**: its air base, its naval base, its forts, its anti-aircraft guns, its
  radar and the rest. The picture is the `billboardType` named `building_` and the
  building's name, of `interface/mapitems.gfx`. **Only where the player knows the province at
  all, or the building is declared `visibility`** [mod: common/buildings.txt]; a port or an
  airfield shows itself in use - its second frame - only where the player knows the province
  at 2 or more [exe] (`reversing/findings/FINDINGS-seaair.md`, section 6). Since 2026-11-07.
- **It stands where `positions.txt` puts that building** in the province -
  `building_position = { air_base = { x y } }` - and is a square of twice the type's `scale`
  a side, 5 map units for all of them in the base game. The original translates the picture by
  the building's point less the label point, each half in single precision, and hangs it on the
  province's anchor at the label point [exe], which comes to the point itself: **a world point**,
  a pixel's middle on whole figures, as the capital marker's city point is - so the 2D map draws
  it half a pixel east and south of the file's figures (`mapdraw.md`, section 3). So do the large
  base pictures, the rings of range and the resource icons, which stand on the same points.
  *Corrected 2026-11-15*: a placed building had been drawn at the file's figures as they are, half
  a pixel west and south of the original's and of everything else on the map, and a click on its
  picture asked of the same wrong place.
- **A naval base's and an air base's pictures have two frames.** The second shows for a port
  **with a fleet lying in it**, and for an airfield **with an air unit at home** - one whose
  base it is and which is there. The first is the dim badge of an empty base.
- **A click on a port's or an airfield's picture picks the next of the player's units in
  it**, one each click, round and round: of a port the fleets lying in it, of an airfield
  the air units based there, wherever they are. The original keeps a list for each, turns it
  by one at each click - the first unit goes to the end, and every unit that is not the
  player's after it - and selects the one that is then first; so the first click picks the
  second of several. A click on any other building's picture goes on to the province under
  it, which opens the province view (`provinceview.md`).
- **A counter over a building's picture takes the click** [exe]: the original scores every picture under the pointer and
  takes the highest, a building's picture 1.0 and a counter `(its place in its stack + 1) × 0.1 + 1`, so a counter always
  wins (`reversing/findings/FINDINGS-seaair.md`, section 6, read off the bytes on 2026-11-13). Here counters are asked
  before buildings, which comes to the same.
- **While a docked fleet or a based air unit is the one unit selected, a click still goes where it is aimed** - *ours*, by
  the decision of 2026-11-13: the original then offers no building's picture and no unit elsewhere, so that every
  second click on a port is lost, as seen in the running game (`docs/compatibility.md`, entry 26).
- The original has a second picture for three of them - `large_air_base`,
  `large_naval_base`, `large_anti_air` - which a province keeps beside the first and asks
  about a click at any height.

**The capital marker** (`reversing/findings/FINDINGS-citypoint.md`, section 3), read 2026-11-14 and built the same day:

- **It stands on the province that is the acting capital of the province's owner** [exe] - the owner and not the
  controller, so an occupied capital keeps its owner's marker, and a government in exile sitting in a host's province
  shows none there, the province being the host's. A province with no owner or no controller shows none. **No test of
  what the player knows**: every country's capital shows, the buildings' intelligence test is theirs alone.
- **It stands at the province's city point** (`map.md`, section 4) - `positions.txt`'s `city`, or the centre of the
  bounding box - as a building's picture stands at its building's place: the original works it out as the city less the
  label point in double precision, narrows it to single and adds the label point back, which comes to the city point.
  So Portugal's stands 0.86 east and 3 south of Lisbon's centre and every other capital's on its centre, in Their Finest
  Hour [mod: the files].
- **It is the `billboardType` named `capital`** of `interface/mapitems.gfx` [mod; the name exe], drawn as a building's
  picture is - through `billboard.fx`, a square of twice its `scale` a side (5 map units in the base game, 3 in BlackICE),
  frame 0 - in the buildings' group, so **in the close view alone** like them (section 7). The original makes one for
  every province and shows or hides it every fifteenth frame by the rule above, so it follows a moved capital at once;
  here the pictures are worked out again whenever the counters are (section 7), which is every day and every move.
- **A click goes through it** [exe]: the original's hit test of a province's pictures never asks the marker.

*Inferred*: the order the original draws one group's pictures in was not read; a province's marker is made before its
buildings' pictures in the one call, and here every marker is drawn before every building, so a building on the same
point lies over it.

**A building `positions.txt` gives no place stands in the middle of its province** - its label point, the centre of its
box, which nothing in the file moves (`CMap.LabelPositions`) - and so do the fleets in its port and the air units at its
base where a drag box asks for them. Observed in the original on 2026-11-07, of the resource icons that share a building's
point (`mapmodes.md`, section 5); until then it stood at the province's unit point, ours, the original's not read.

*Ours:*

- the units of a port or an airfield are gone through in the order of their numbers, where
  the original goes by the order they came in. Decided on 2026-10-09: "Its fine the
  way it is";
- every building's picture is drawn all through the close view and none in the far view
  (section 7). Of the three larger pictures, `large_air_base` and `large_anti_air` are the
  air map mode's, sized there by the level built (`airmap.md`, section 5, read since);
  `large_naval_base` is the naval mode's (`mapmodes.md`, section 11);
- a click has to fall on the round badge, two thirds of the square across - measured on the
  two bases' art;
- a selected fleet in port, or air unit at its base, has no counter and so no ring.

Not read: what makes a coastal fort's picture turn (`orientation`), a flag the original
sets on a port building's picture that stands well east of the naval base, and the order
two pictures that overlap are asked in - `buildings.txt` says in a comment that it is the
order of that file, "so leave bases on top".

## 10. The deploy menu's tooltips

Read 2026-10-12, `reversing/findings/FINDINGS-orderstooltips.md`; built in
`CDeployMenuTooltips` (`lib/OpenHOI3.Interface/Tooltips`) and tried with `--click`. **Only the
menu's lines speak**, each anywhere on it, its name over its button too: a line for each of the
unit's brigades, `£` and its type's key - which draws its counter - a space, its name and a line
break, as the unit panel's counters say (`unitpanel.md`, section 10.1).

- **A finished unit's line** says so only for a land unit. **A fleet's or an air unit's says
  nothing** - the original asks whether the unit is a land unit first. Kept as read.
- **A line of a unit the new one may join** says it whatever the unit is: a fleet lists its ships.

Nothing else in the menu says anything; its own close button would say `CLOSE`, as every
`closebutton` nothing else claims does, and the menu has none.

## 11. The front line of a headquarters under an AI

Read 2026-11-09, `reversing/findings/FINDINGS-frontline.md`; built in `CFrontLines` and drawn through the install's `front.fx` with its `front_arrow` picture. Hardcoded but that effect and that picture [mod]; it has not been reviewed in play.

A selected headquarters of the player's under an AI, its own or one above, has a line for **its own plan's area**. It comes when it is the single unit selected or its area changes, and goes when it is no longer selected or no AI is over it; an area gone empty leaves the line as it was. It is not drawn again on the hour, and it has no map mode, height or setting that hides it.

Each province of the area in the plan's order is walked for its neighbours on **foreign land**: land held by someone not the unit's owner, not allied with it, not fighting beside it and with no puppet's tie either way; a friend at peace counts. Each step's point is **half way between two provinces' centres**, so the line does not follow the borders' pixels. What a walk cannot reach starts a new chain, and chains that meet are joined by the original's eight tests. A chain's points are eased twice (all but the two at each end) and their corners cut once, then made a strip six wide - a corner goes along the two segments' average normal, not lengthened - the distance so far along it and 0 or 1 across. It is flat on the map at height 1, not depth tested, drawn once and not again a map's width over. All of what makes it black and dashed is the picture, two dashes every 6.67 map pixels.

## 12. A plan's objectives and arrows

Read 2026-11-09, `reversing/findings/FINDINGS-frontline.md`, section 7; `CPlanMarkers`, `CPlanMarkerLayer`. Hardcoded but the pictures and types in `items.gfx`, `core.gfx` and `arrows.gfx` [mod]; they have not been reviewed in play.

While a headquarters of the player's under an AI is the single unit selected, **every** plan of the player's under an AI shows: a marker on each objective - `icon_objective_strip` and the province's name in white, the frame and the grey of `MapObjectiveOther` for a plan not the selected one's - and, for a land unit with an axis, an arrow along it, `AutomationArrow` or `OtherAutomationArrow`, from province centre to centre and a quarter of the last leg further, drawn fully walked and twice, as the original makes two.

**A right click on a province**, with a land headquarters selected that holds an agent itself, is the plan's: plain, it makes the province an objective or takes it off (priority 400, to be held, and the agent's area worked out again); with Shift it draws the axis there from where the unit stands. A unit under an agent above it takes no order at all.

**A headquarters handed over runs as a country the AI runs does**: its divisions are committed, marched and sent to attack, its fleets and air units managed, with what its own plan says. Its objectives narrow its area: an objective on land the country does not own makes the fronts beside that objective's area the only ones taken, hot fronts and war candidates apart, and the area is worked out again at once (`reversing/findings/FINDINGS-setarea.md`, passes A to D). An objective in an area none of the theatre's fronts face changes nothing, the fronts being the theatre's own, as confirmed of the original on 2026-11-09; `OpenHOI3.SaveCheck automate` is the trial. ~~The plan starts at Prepare with its air and naval stances lowest, as a theatre's adoption gives it.~~ **The plan starts
defensive, ground, air and naval**, by decision of 2026-11-21 (`compatibility.md`, entry 32); the original's theatre
adoption gives Prepare and the air and naval stances lowest.

**What the axis does**: it is no order to march. At Attack or Blitz a province of the enemy's scores more as a place to attack where it is on the axis, and a little more beside it, in `CLandAttack`; at other stances it does nothing. The AI's own axis is only ever drawn for countries it runs.

**Ours**: the route is `CRouteFinder`'s, where the original has a planned path finder not read, so an axis into a neutral country's land finds no way here. The name's size on the map is a reading of the type's scale. A headquarters' `army_role` is not tested, there being none here.

## What is not built

- **A leader's name on a counter**, and the orange corner of a unit with a brigade being
  changed into another type, which nothing here makes.
- **The red frame of a moving unit's pointer.** Its picture has a green one and a red
  one; when the original shows the red was not read.
- ~~**Dragging a unit's line onto the map.**~~ The original has no such drag; the line goes into the order of battle
  browser (section 6).

## What is not established, in one place

For the reverse engineering:

- ~~how a province's **five stack points** are made from its one `unit` point~~ - read 2026-10-10 (section 1);
- ~~what a counter's **place in its stack** is taken from~~ - read 2026-11-14 (section 3); what else
  the original's frame does with a province's units in a battle, showing or hiding them, was not;
- what a **text's size** is divided by;
- who tells a unit's picture it is **seen from far away**, and what hides a counter and a
  building's picture in the far view - the heights are read (section 7), the routines
  that act on them for the drawing are not;
- how the map shows **where a unit may be placed**, and the click that places it;
- **when the alert comes up**;
- the **carrier air group** rule among the units a wing may join.

And what a look beside the running game would still settle: whether a counter is the
size it is there at the same zoom, the far counter's texts - only the near one has been
measured - and which way a stack fans.
