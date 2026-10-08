# The sliders: how IC and leadership are divided by hand

The six sliders of the production screen and the four of the technology screen are one
thing in the original, and one here. This is what a slider shows, how a set of them stays
adding up, and when what the player sets reaches the game. No addresses and no offsets.
Each rule says how sure it is - `confirmed`, `likely`, `inferred` - and where this project
chooses its own behaviour the paragraph is marked **Ours**.

Source: `reversing/findings/FINDINGS-countryviews.md`, sections 2 and 3, read for this.
**Nothing in it was checked against a running game.** What the shares *do* once set is
`production.md` and `leadership.md`.

Colours are written `§R` and `§W`: the escapes for red and white.

## 1. A slider is a window

`confirmed` Each slider is a window of its own, made from a window type and put down the
screen: the first at a `positionType` the files name, each next one further by another.

| | window type | first at | each next by | how many |
| --- | --- | --- | --- | --- |
| production | `domesticpanel_slider` | `domestic_eco_slider_start` | `domestic_eco_slider_offset` | six |
| technology | `tech_slider` | `leadership_slider_start` | `leadership_slider_offset` | four |

The code finds these in it by name:

| kind | name | what |
| --- | --- | --- |
| text box | `title` | the share's name |
| text box | `value` | what the slider holds |
| text box | `need` | what the share needs |
| scrollbar | `actual_slider` | the slider itself |
| button | `ic_need_button` | sets the slider to its need; a window may have none |
| icon | `limit` | a marker on the slider; only the production screen's has one |

`confirmed` **The order down the screen is the reverse of the shares' own order.** The
shares are made into sliders first to last and each is put in front of the one before:

| production, from the top | technology, from the top |
| --- | --- |
| upgrades, reinforcement, supplies, production, consumer goods, lend-lease | research, espionage, diplomacy, officers |

That is the order of the little pictures the interface files draw beside them.

## 2. What a slider shows

`confirmed` **The title** is the text of a key: `DISTRIBUTE_UPGRADE`,
`DISTRIBUTE_REINFORCE`, `DISTRIBUTE_SUPPLY`, `DISTRIBUTE_PRODUCTION`,
`DISTRIBUTE_CONSUMER_GOODS`, `DISTRIBUTE_LENDLEASE`; `DISTRIBUTE_RESEARCH`,
`DISTRIBUTE_ESPIONAGE`, `DISTRIBUTE_DIPLOMACY`, `DISTRIBUTE_NCO`.

`confirmed` **A slider holds an amount, not a share**: so much IC, or so much leadership.
It runs from nothing to the whole of what the country has - its IC, or its leadership -
whatever range the interface file gave the scrollbar. When the country's total changes
every slider is given the new one.

**`value`** is the amount to two decimals, behind a colour its share chooses:

| share | colour |
| --- | --- |
| production, consumer goods, diplomacy | `§R` when the need is greater than the amount, `§W` when not. `confirmed` |
| supplies, reinforcement, upgrades | the same, `likely`: their routines are the shape of production's and were not read |
| lend-lease | always `§W`. `confirmed` |
| research, espionage, officers | none. `confirmed` |

`confirmed` The comparison is in hundredths. *Not read:* whether the amount is rounded or
cut to them - the original prints it from a float, and adds a term nobody has read before
it compares. **Ours:** the amount is rounded to the nearest hundredth, for the figure and
for the comparison alike. A share is cut off when it is stored, so the amount it gives is
a hair under the amount it was set to - 6.898 IC where 6.90 was meant - and cutting again
would paint every country's consumer goods red on the day the game starts.

**`need`** is the text of `SLIDER_NEED` followed by the need to two decimals, for the
shares that have one:

| share | its need |
| --- | --- |
| consumer goods | the fraction of its IC at which the country's dissent stands still - what it demands, and what its dissent modifier adds - times its IC. `confirmed` |
| production | what the things being built want. `confirmed` |
| reinforcement, upgrades | what reinforcing and upgrading currently cost. `confirmed` |
| supplies | not read |
| research | **the number of projects on the queue.** `confirmed` |
| diplomacy | the upkeep of the country's diplomatic influence. `confirmed` |
| lend-lease | none: the line reads the text of `DISTRIBUTE`, and its button opens a window of its own. `confirmed` |
| espionage, officers | none, and no line. `confirmed` |

*Ours:* production, reinforcement, upgrades and supplies need nothing until there are
units; diplomacy needs nothing until there is diplomacy.

`confirmed` **`limit`** is hidden unless the share gives a figure of nought or more to put
it at, and only lend-lease does. *Ours:* it stays hidden.

## 3. The sliders always hold the whole between them

`confirmed` Whatever the player does to one slider, the others make room. The original
works it out again on every pass, up to ten times over:

1. what is left is the total less the sum of the sliders;
2. the sliders free to move are those that are neither locked nor in the player's hand;
3. what is left, divided equally between the free ones, is added to each - a slider will
   not go below nothing or above the total, and gives back what it could not take;
4. until what is left is under half a hundredth.

So a slider the player moves keeps its place, and the rest absorb the difference in equal
parts. With nothing free to absorb it, the moved slider cannot move.

**Ours:** the same rule in thousandths and in one pass - an equal part each, a part a
slider cannot take going round again, and the moved slider giving way if nobody can take
it. The original's floats need the ten rounds and the tolerance; whole numbers do not.

`confirmed` **A locked slider stays where it is.** The original's own words for it:
`SLIDER_UNLOCKED_TOOLTIP`, "Right click to lock", and `SLIDER_LOCKED_TOOLTIP`, "Locked,
right click to unlock". `inferred` The lock is kept with the country, so it is still on
when the screen is opened again. *Ours:* a right click on the slider locks and unlocks it; the lock lasts as long as the
screen object does. `confirmed` **A locked slider is drawn in grey**: locking is
disabling the slider's own button, and a disabled button is drawn by the `disable`
technique of the original's button shader (`interface.md`, section 5). The maintainer
said the same of the running game on 2026-10-08.

`confirmed` **`ic_need_button` puts the slider at its need and locks it there**; pressed
on a slider already locked it lets go. For lend-lease it opens the lend-lease window and
moves nothing.

## 4. When the game is told

`confirmed` The sliders are the player's until they are sent: the original sends what they
hold **at most once a second** of real time, and only when something changed.

**Ours:** the same, as a command - `CSetProductionSharesCommand` or
`CSetLeadershipSharesCommand` - carrying each slider's share of the whole, cut off so that
they never come to more than one. Closing the screen sends at once what had not been
sent. While something sent has yet to be taken the sliders show it; when the country's
shares change for any other reason the sliders follow them.

## 5. Ours

- **`CSliderGroup`** in `OpenHOI3.Interface` holds the amounts and the rule of section 3,
  and is tested with no engine. `CSliderPanel` in the client is the windows and section 4.
- **The scrollbar's layout is ours** - a button at each end, the track between, the slider
  on the track: `interface.md`, section 2.

## What is not established, in one place

1. **Little was seen running.** The maintainer tried the sliders on 2026-10-08: dragging
   is right, and a locked slider is grey. The figures beside them were not compared.
2. **Rounding**: whether `value` and `need` are rounded or cut to two decimals, and the
   term added before the colour's comparison.
3. **What locks a slider** in the original's code. The tooltips say a right click; the
   routine was not found.
4. **The supplies slider's need.**
5. **The commands the original sends**, past the once-a-second gate.
6. **`limit`**: where lend-lease's marker goes.
