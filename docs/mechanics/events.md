# An event firing and a decision taken

When the original asks an event, how it fires, who answers and how, and how a decision is offered
and taken; and the two windows a person meets them in. `script.md` is how events and decisions are
read, `triggers.md` how their conditions answer and `effects.md` what their effects do; **this is
when and by whom**.

Sources: `reversing/findings/FINDINGS-events.md` (the candidate lists, the daily pass, the mean time
to happen) and `reversing/findings/FINDINGS-eventfire.md` (firing, answering, decisions and the two
windows, read 2026-10-12). Nothing here was tried in a running game.

## 1. Which events are asked: each country's candidates

A country's **candidates** are the events its daily pass asks. They are made again **once a month**
- in the month's pass, which runs **after** the day's on the 1st - and **when a game is entered**.
Making them throws every country's away and then, for each event in the order the files were read
and each country **that owns a province** in the order the countries were declared, keeps the event
for that country where:

- it has at least one option;
- it is not `is_triggered_only`;
- it is not a `fire_only_once` event that has already fired (section 6);
- its `trigger` holds in a scope of that country - no province, no FROM - **looking ahead the days
  of the current month** (`triggers.md`, section 5, `date`).

So an event dated within the coming month becomes a candidate early, and a condition that turns true
mid-month waits for the next month to be asked at all. **The first day an event with a mean time to
happen can fire is the 2nd**: on the 1st the day's pass runs over the old lists.

## 2. The day's pass

Once a day, after the provinces' own day and before intelligence: for each country that owns a
province, in order, and each of its candidates in order,

1. its `trigger` is asked again, in a scope of the country that looks no days ahead;
2. where it holds, the **chance** of section 3 is worked out, and a roll is drawn on the game's
   generator: the draw with its top bit dropped, over 2^31 - a number from 0 to just under 1;
3. **where the chance is above the roll, the event fires** (section 4).

A candidate is not taken off its list when it fires: **an event without `fire_only_once` may fire
again on any later day** while its trigger holds.

Every scope a pass makes - this one and the candidates' - takes **one draw of the game's generator at
the start of the pass** as its seed and its state of chance, shared by all of them.

*Ours*: no event is asked in the two days a new game runs before its first hour (`tick.md`,
section 6); whether the original's are was not read. The candidates are made as the game is
entered, after them.

## 3. The chance of a day

From the event's `mean_time_to_happen`, in single precision as the original works it:

    days = its days
    each modifier whose conditions hold:  days = days x factor
    if days is not above 0:  the chance is 1
    else:                    the chance is 1 - 0.5^(1 / days)

So `days` is a **median**: an event of 30 days has an even chance of having fired after 30 days of
holding, 2.28% a day. The original works the power as `exp(log(0.5) / days)`, then
`exp(log(that))` again, rounding to single precision between each step, and so does this.

## 4. An event fires

In this order:

1. **A `fire_only_once` event that has fired** does nothing at all.
2. **Who answers**: the scope's country, or where it has none the owner of its province.
3. **A country nobody plays** - the AI - chooses an option (section 5) and **posts** the answer as a
   command: the option runs when the command does, at the next turn of the queue.
4. **A country a person plays** has the event **opened** for him: it waits on his list of open events
   for him to choose (section 7) - **unless this very event is already open** for him, when no second
   one is opened.
5. **The `immediate` block runs** - always, in the event's scope - after the answer is posted or the
   event opened, and before either answer's option runs. **An event fired again while it is open runs
   its `immediate` again** though it opens nothing.
6. A `fire_only_once` event is marked as fired - **now, when it fires**, not when it is answered.

The trigger is not asked here: whatever fires an event has asked it.

## 5. The AI's choice of an option

Each option's **weight** is its `ai_chance`'s days times its `factor` (both default to 1), times the
factor of each of its modifiers whose conditions hold - in single precision. Then:

- where the weights sum to nothing (or less), **option 0**;
- otherwise each option's **share** is its weight over the sum divided by a hundred, **cut to a whole
  number**, a roll from 0 to 99 is drawn, and the option is **the first whose running sum of shares
  the roll does not exceed**; a roll past them all is **option 0**.

So six even options have shares of 16, running to 96, and a roll of 97, 98 or 99 falls to the first
- three even options, running to 99, leave no roll over; and a first option weighing nothing is still
taken one time in a hundred, on a roll of 0.

**The roll is the game's generator's**, `% 100`. The original draws it on a fresh generator seeded
with the time of day, which no run can repeat; the maintainer's choice is to draw it on the game's
own (`docs/compatibility.md`, entry 11).

## 6. `fire_only_once`

A game keeps the events that have fired with `fire_only_once`, by the event alone - **not by country**:
such an event fires once a game, for whichever country gets it first. It stays on other countries'
candidate lists until the month's rebuild, and the first test of section 4 stops it there.

## 7. A person's answer

An open event waits for its player's choice. **Choosing** posts the answer, as the AI's is posted.
**Unanswered for four months** of game time, the event answers itself with **option 0**, whatever the
AI would have chosen. The answer is a command which, when it runs:

1. does nothing where the event no longer exists;
2. sets the scope's state of chance back to its seed - so an option's rolls repeat the ones the
   `immediate` block began with;
3. runs the option's effects in the event's scope (`effects.md`).

It does not ask the trigger again, nor whether the event was open. Taking the event off the player's
list of open events is the answer's too.

*Ours*: the original's window counts the four months each frame, and only while it is shown; here the
game counts them each hour, so an event a person never looks at answers itself all the same. The
message the original raises when an answer runs - `EVENTHAPPENUS` and the rest - waits for messages,
which are not built.

## 8. Events nothing asks

**A province event is never a candidate**, so one with a mean time to happen **never fires on its
own** - in the original either. Only an on_action fires one, and the on_actions - a battle won or
lost, an encirclement, a surrender, a coup, a pride of the fleet sunk, a nuclear bomb - are not built:
`common/on_actions.txt` is not read. When they are, an on_action takes **the first** of its events, in
file order, whose trigger holds; the weights its file writes are never read.

## 9. Decisions

**Offered**: a country's decisions are its **diplomatic** ones - the `diplomatic_decisions` blocks -
in file order, those whose `potential` holds, asked in a scope of the country with nothing else and a
seed of nought. **The political ones are read and never offered**, to a person or to the AI. A
decision **may be taken** where its `allow` holds too.

**Taking one** posts a command carrying the decision's **name** and the scope. It is asked again just
before it runs - its state of chance set back to its seed, then `potential` and `allow` - and dropped
where either fails or the name finds nothing (the diplomatic decisions first, then the political).
When it runs:

1. its state of chance is set back to its seed;
2. **its effects run**, in the scope;
3. the country's modifier is made again, and its province's where the scope has one.

**Nothing records that a decision was taken**: it may be taken again whenever `potential` and `allow`
hold - taking one once is the script's affair, a flag set in its effects and asked in its
`potential` by custom. *Ours*: the original files an entry in the country's history that it never
applies; there is no history here to file it in. The message it raises waits for messages.

**The AI takes decisions in its Lua scripts** - the original's own C++ reads `ai_will_do` nowhere -
and **there is no AI**, so no AI country takes one. That a country nobody plays takes no decision is
the one thing here that waits for the AI.

**A decision in a country's history** (`decision = X`) is run when a new game is made - **if** its date
is by the start; every one in the base game's and Their Finest Hour's history is dated after
1 January 1936, so none runs here, and none is read.

## 10. The event window

The window is **`EventWindow`** - or **`Event_Province_Window`** where the event's scope has a
province, whatever kind of event it is - from the interface files, where the file puts it. Several
open events have a window each, at the same place; the newest on top. *Ours*: the order the original
draws them in was not read.

| element | what it shows |
| --- | --- |
| `Title` | the event's `title`, localised (section 11) |
| `Subtitle` | its `subtitle`, localised - **only where it has one**; otherwise the file's text stays |
| `Description` | its `desc`, localised, and - where the `immediate` block's description is longer than a character - a blank line and that description in white |
| `event_images` | the picture `gfx/pictures/events/` + `picture` + `.tga`; `GFX_event_no_image` (`GFX_prov_event_no_image`) where none is written or none loads |
| `event_option<i>` | a button for each option, in file order and **with no cap**, made from the type `EventOptionButton` (`EventPOptionButton`) and saying the option's `name`, localised |

**Button i** of n stands at x = the offset's x and **y = the window's height - (n + 1 - i) x the
offset's y**, the offset being the position the file gives `event_option_offset`
(`event_province_option_offset`): the last option always sits two steps above the bottom and more
options grow upwards. **The buttons cannot be pressed for the first second** the window is up.
Pressing one answers with its option and closes the window. **There is no close button**: an event is
answered or it times out. Both windows' files say `moveable`, so **the window is dragged by the left
button held anywhere on it but on an option** (`interface.md`, section 7).

*Not built*: an option's tooltip and the description of the `immediate` block are what its effects
say of themselves, and nothing yet writes effects as text; both are left out.

## 11. An event's text

A key of the localisation, with **`$NAME$` variables** replaced from the event's scope - not
`[This.GetName]`, which nothing in the original's files uses. C is the scope's country, or the
controller of its province where it has none.

| variable | value | only when |
| --- | --- | --- |
| `THISCOUNTRY` | THIS's country's name | THIS has a country |
| `FROMCOUNTRY`, `FROMCOUNTRY_ADJ` | FROM's name, and its adjective | the scope has a FROM country |
| `COUNTRY`, `COUNTRYNAME` | C's name | C exists |
| `COUNTRY_ADJ` | C's adjective | C exists |
| `CAPITAL` | the name of C's capital | C exists |
| `MONARCHTITLE` | C's government's title | not built: left as written |
| `YEAR` | the year | always |
| `PROVINCENAME` | the province's name | a province |
| `REGIONNAME` | its first region's name, localised; empty for none | a province |
| `CONTINENTNAME` | its continent's name **as the map writes it, not localised** | a province |
| `OWNERNAME` | its owner's name | a province |
| the last battles' losses, `PRIDE_SUNK`, `PRIDE_SINKER` | not built: left as written | |

*Ours*: a country's adjective is the localisation of its tag with `_ADJ`, for both. The original
reads them out of a table nobody has read the filling of, and passes `FROMCOUNTRY_ADJ`'s through the
localisation and `COUNTRY_ADJ`'s not - which matters only if the table holds keys.

## 12. The decision list

On the diplomacy screen (`country_diplomacy`), in **`decisions_listbox`**, made again **each time the
screen is opened**: the decisions offered (section 9), **those that may be taken first, in reverse
file order, then those that may not, in file order**. Each row is a `diplomatic_decision_entry`:

| element | shows | tooltip |
| --- | --- | --- |
| `name` | the decision's `<name>_title`, localised (section 11) | `<name>_desc`, localised |
| `select_button` | the file's `SELECT`; **disabled where the decision may not be taken** | its effects' description - *not built* |
| `info_icon` | the file's icon | its `allow` block's description - *not built* |

**While the screen is up**, each row asks again `allow` and then `potential`, and disables its button
where either fails - **and never enables it again** until the screen is opened again; a row whose
`potential` fails stays listed until then. Pressing the button takes the decision (section 9) - the
button does not ask `allow` itself; the command does when it runs. The rest of the diplomacy screen is
not built.

## Not established

- What the original draws when an event's picture does not load.
- The order several event windows are drawn in.
- How often the original's diplomacy screen asks its rows again.
