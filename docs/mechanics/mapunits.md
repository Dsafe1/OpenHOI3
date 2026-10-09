# Units on the map: counters, and putting a finished unit there

What the original shows of a unit on its map and how a finished one gets there, written
as rules this project can implement. No addresses and no offsets. Each rule says how sure
it is, in the fact base's vocabulary - `confirmed`, `likely`, `inferred` - or **measured**,
meaning counted here against the files of a Their Finest Hour install on 2026-10-09.

Sources: `reversing/findings/FINDINGS-counters.md`,
`reversing/findings/FINDINGS-deploymenu.md`, and for the command a finished unit is placed
by `reversing/findings/FINDINGS-buildqueue.md`. The maintainer looked at it on 2026-10-09
and gave a screenshot of one of the original's counters, which section 2 is measured
against; the last section says what a further look would settle.

Where this project deliberately does something else, or had nothing to go by, the
paragraph is marked **Ours**.

## 1. Where a province's units stand

A province has one point that stands for it - `map.md`, section 4: the centre of its
bounding box unless `positions.txt` gives it a `unit` point, or for a sea zone a
`text_position`. That file is read now (2026-10-09), by a reader of its own, since it is
six megabytes of which those two keys are a small part.

**A unit stands at its province's `unit` point.** The maintainer, 2026-10-09: "The unit
position is part of the positions.txt." The file has that one point for a province and no
other for units (measured: 2754 `unit` blocks, and beside them only the text's and one
`city`).

The original keeps **five points to a province**, one for each stack of section 3, and
stands a counter at its stack's: the first for a land unit or a fleet with no path, the
second for one with a path, the last for an air unit, and the two between for the sides of
a battle. `confirmed` (`reversing/findings/FINDINGS-movement.md`, section 9). With one
point in the file the other four must be worked out from it, and **how was not found.**
**The stack of units on the move stands 2.6 map units east and 1.5 north of the standing
one** - half a counter and a third of one - and is drawn over it. (measured) On a
screenshot of the original the maintainer gave on 2026-10-09, a division setting off lies
3.7 map units east and 0.5 north of the top counter of the stack it leaves; that counter
is itself 0.3 west and north for each of the four under it. The count of the stack is
read off its edges, so the figure is good to a few tenths. Before the screenshot it was a
guess, first of a whole counter's width - the maintainer: "Its way too big now" - and then
of three tenths of one. *Ours:* the stack of aircraft is a counter's width to the east and
north. Nothing fights
yet, so the other two are never used.

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
over through the mask's blue.

- **The unit type's picture** is the sprite `GFX_small_counter_<key>` on the far counter
  and `GFX_map_counter_<key>` on the near one, `<key>` being **the type of the unit's first
  brigade, ship or wing**. `confirmed`
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

- **A counter's own scale** is `(size - 1) * level_scale + 1`, `size` counting from 0 for
  `X`: with the far counter's `level_scale` of 0.2 a lone brigade is drawn at 0.8, a
  division at 1 and a theatre at 1.8. The near counter gives no `level_scale`, so every
  near counter is the same size. `likely`
- **A counter is `2 * scale * its own scale` high** and as wide as its plate's picture
  makes that. `likely`: it is how the original tests a click against one.
- **`offset`, `offset2` and `offset3` are in tenths of `scale`**, times the counter's own
  scale, and so are the sizes of the two texts. Their three numbers are east, up off the
  map, and north. `likely` for the tenths; north is **measured** (below).
- **The flag** is on the near counter only, which is the one with a `shieldtype`: its
  middle at `offset`, `shieldtype`'s two numbers wide and high in `scale`s.
- **The two figures** start at `offset2`, at the size `size`.
- **A name** starts at `offset3` on the near counter, at the size 3.5: the unit's leader's,
  or with the `show_unit_names` setting the unit's own, cut to its first eight letters.
  `confirmed` for the rule, `likely` for the size.
- **Only the top counter of a stack carries a flag, figures and a name.** `confirmed`

**Measured on a screenshot of the original's near counter**, which the maintainer gave on
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

*Ours:* the original runs the shader for every counter every frame. Here a counter is
painted once for each owner's colour, unit type and size that comes on screen, by the same
arithmetic, and drawn as an ordinary picture. **The name is the unit's own**, as with
`show_unit_names` on - which is how the maintainer's game is set, and there are no
leaders to name and no settings to choose by. The maintainer, 2026-10-09: "It can stay
that way until we implement the settings" (`docs/compatibility.md`, entry 7). A unit type
with no counter sprite of its
own is shown by the `unknown` one of the same family.

### The pointer of a unit on the move

The maintainer, 2026-10-09, with a screenshot: "In the original a moving unit also has a
small green pointer at all times." It is the `counter_arrow` `billboardType` of
`interface/mapitems.gfx`, which every counter has one of:
`reversing/findings/FINDINGS-counters.md`, section 9.

- **Every unit with a path shows it**, selected or not. `confirmed`
- Its picture is a strip of `noOfFrames` frames; the first, green, is the one a marching
  unit shows. `confirmed`. The second is red and its use was not read.
- It is drawn in a square `2 * scale` across - 2 map units in the base game - times the
  counter's own scale. `confirmed`
- **It stands `offset` from the middle of the counter, in the direction the unit is
  going, and points that way** - 4.5 map units in the base game, times the counter's own
  scale. The direction is from the unit point of the province the unit is in to that of
  the next province of its path, the short way round the map. `confirmed`, and the
  screenshot agrees to the degree: the pointer lies on the line it points along. It is
  about a seventh further from the counter there than 4.5, which is not explained.

*Ours:* the pointers of a stack are drawn with their counters, so one under the top
counter's can be hidden by it.

## 3. Which units have a counter, and how they stack

**A fleet in port and an air unit at its own base have no counter.** The original shows
those by the province's port and airfield, which are not drawn here yet. Nor has a unit
that is aboard something. Every other unit that is somewhere has one. `confirmed`

A province sorts its counters into **five stacks**, by what the unit is doing: `confirmed`
for the order of the tests.

| stack | |
| --- | --- |
| air | an air unit |
| attacking, defending | a unit in a combat |
| moving | a unit with provinces left to move through |
| standing | every other |

**A stack shows its top counter, or its top six** each drawn 0.3 map units west and 0.3
north of the one under it. `confirmed`. Which it is depends on how well the player knows
the units: the original has an intel level for every unit, 9 for the player's own, and
under 3 a stack shows one counter with the unknown picture, under 2 nothing at all. There
is no fog of war here, so every stack shows up to six.

**Within a stack the higher rank is on top.** The maintainer, 2026-10-09: "Correct." The
original sorts a stack by a number on each counter whose source was not read, so the
rule is his and not the reading's. *Ours:* of two of a rank the one made later is on top,
and a selected unit is brought to the top of its stack.

**Only the counter on top of a stack takes a click.** `confirmed`: the original asks a
near counter whether the mouse is on it only where it is the top one of a stack. The
maintainer, 2026-10-09, of what follows from it: "You click the top unit only, if you hit
the lower ones the click goes through to the province. Then if you keep clicking the unit
it cycles through the units in the province." So a click on the top counter selects its
unit, and each further click the next unit of the stack. *Ours* for the order, which was
not read: from the top down, and round to the top again.

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
- A figure the player does not know - an intel level under 7 - is written `?`. Never, here.

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

*Ours:* the alert is up while the country has a unit to place. When the original puts it
up was not read; its tooltip is "We have units available to be deployed." It is the only
alert built, so it is always at the start position; a second click on it closes the menu.

## 6. The deploy menu

The window `sidebar_deploy` of `interface/sidebar.gui`: a title, the list
`deployment_queue`, and `close`. `confirmed` throughout, but where marked.

**Its list is the country's units waiting to be placed**, in the order they were finished,
each a `deployment_entry`:

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
being placed - the maintainer, 2026-10-09: "Looks good". That the original marks them is
how the game is remembered; how was not
read, nor was the click on the map that places a unit - only the menu's side of it. While
a unit is being placed a click on the map is for the province and not for a counter on it.
The original also lets a unit's line be dragged onto a unit on the map: not built. Its
rule about a carrier's air group and the room aboard was not read through, and is not
asked.

## 7. Ours

- **`OpenHOI3.Interface.Map`** has what can be checked with no engine: which units have a
  counter and how they stack (`CMapCounters`), what one says (`CCounterText`), where its
  parts go (`CCounterLayout`) and the painting of one (`CCounterPainter`).
  `OpenHOI3.Sim.Units.CUnitFigures` has the two figures, and
  `OpenHOI3.Sim.Construction.CDeployment` who may join whom.
- **`src/Map/CUnitCounters`** draws them, over the map and in its coordinates; a click on
  the top counter of a stack selects a unit (section 3), which then wears the ring.
- **Near and far go by the zoom**: the near counter from a zoom of 8, the far one under
  it, and none at all once one would be under six pixels high. The original goes by the
  camera's distance against a setting, `counter_distance`, of 100; what it compares was
  not found.
- **The near view is always counters.** The original's is counters or 3D figures by a
  setting. There are no figures here.
- **The counters are worked out again** once a day, whenever a unit of the player's is
  placed or ordered, and each hour that a unit is on the march or has just come off it -
  the hour it arrives is the one its counter has to change province in - not every frame.

## 8. The hierarchy of a selected unit

Read out of the original on 2026-10-09, after the maintainer's word that it "has hierarchy
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
  half, still. What the shader does is in `gfx/FX/hierarchy.fx`, which ships as text.
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
- The first reading missed the ring of the units under it; the maintainer's word and a
  screenshot of 2026-10-09 put it right. On that screenshot the selected headquarters wears
  green - an AI commands it there - and the three counters under it the blue-green.
- The original gives the rings out when a unit is selected and takes them back when it no
  longer is, so a unit attached under it in between has none until then. *Ours:* they
  follow the order of battle as it is.

Not built: the dark red broken line, since nothing works out radio range; and the green
ring, since there is no AI.

## What is not built

- **The port and the airfield on the map**, which is how a fleet in port and a wing at its
  base are seen in the original. Until then such a unit shows only in the province
  readout.
- **The other fifteen alerts**, and the tooltips of this one.
- **A leader's name on a counter**, and the coloured corner that marks something about a
  unit's state.
- **The red frame of a moving unit's pointer.** Its picture has a green one and a red
  one; when the original shows the red was not read.
- **Dragging a unit's line onto the map.**

## What is not established, in one place

For the reverse engineering:

- how a province's **five stack points** are made from its one `unit` point;
- what a counter's **place in its stack** is taken from;
- what a **text's size** is divided by;
- who tells a unit's picture it is **seen from far away**, and the test against
  `counter_distance`;
- the **three marks** that colour a counter's corner, and what `delayAfterAttackColor` is
  for;
- how the map shows **where a unit may be placed**, and the click that places it;
- **when the alert comes up**;
- the **carrier air group** rule among the units a wing may join.

And what a look beside the running game would still settle: whether a counter is the
size it is there at the same zoom, the far counter's texts - only the near one has been
measured - and which way a stack fans.
