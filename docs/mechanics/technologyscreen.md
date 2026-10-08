# The technology screen: what goes in each element

What the original's technology screen writes into its elements, where it puts a
technology, and what its entries show, written as rules this project can implement. No
addresses and no offsets. Each rule says how sure it is - `confirmed`, `likely`,
`inferred` in the fact base's vocabulary. Where this project chooses its own behaviour
the paragraph is marked **Ours**.

Source: `reversing/findings/FINDINGS-countryviews.md`, section 5, read for this. **Nothing
in it was checked against a running game.**

What the screen shares with the others the top bar opens is `countryviews.md`; its four
sliders are `sliders.md`; what research does is `research.md`. Colours are written `§R`,
`§G`, `§Y` and `§W`: the escapes for red, green, yellow and white.

In what follows *level* is the level of a technology the country has, and a technology is
*of one level* when its file gives it no `additional_offset` (`research.md`, section 1).

## 1. The window

`confirmed` The screen is the window `country_technology` of
`interface/country_technology.gui`. The code finds these by name:

| kind | names |
| --- | --- |
| text boxes | `leader_ship_label`, `current_research_label`, `automation_setting` |
| icon | `bg_countryskin`, and one named `bg_tech_` + folder for each folder |
| buttons | `close_button`, `mode_arrow_left`, `mode_arrow_right`, and one named `techtree_` + folder for each folder |
| list box | `current_research` |
| windows | `tech_tree`, `tech_categories`, `tech_details` |

and four windows of the type `tech_slider`, at `leadership_slider_start` and each next one
further by `leadership_slider_offset`. The original makes those children of a third
top-level window, `country_leadership_sliders`, which lies exactly over the screen.
*Ours:* they are children of the screen itself, which comes to the same place.

## 2. The two lines of figures

`confirmed` **`leader_ship_label`** is the text of `DISPOSAL_OF_LEADERSHIP`, then `:§Y `,
then the country's leadership to two decimals: `Distribution of leadership:§Y 27.31`.

`confirmed` **`current_research_label`** is the text of `CURR_RES_LABEL` with two places
filled:

- `$MAX$` is what the research slider holds, to two decimals;
- `$CURR$` is the number of projects on the queue, between a colour and `§W`: **`§G`
  unless the projects outnumber `$MAX$` by a whole one or more, then `§Y`**. The routine
  has a red arm that cannot be reached.

In English: `§G7§W projects of §Y6.82§W possible.`

## 3. The folders, and where a technology goes

`confirmed` The folders are those `common/technology.txt` lists under `folders`. For each,
on every pass: the icon `bg_tech_` + folder is shown if it is the folder chosen and hidden
if not, and the button `techtree_` + folder is set to **frame 2 if chosen and 1 if not**.
The first folder that has a button is the one chosen when the screen is made.

`confirmed` **The tree is one `tech_entry` window for each technology of the folder
chosen** - each technology's file names its `folder` - made as children of the window
`tech_tree`, which is emptied first. **An entry is put at the `positionType` named after
the technology: its key + `_position`.** A technology with no such position stays at the
corner of `tech_tree`.

That is the whole of the layout. The lines and arrows between technologies are the
folder's picture, not anything drawn from the data; a mod that adds a technology gives it
a position and, if it wants one, paints its arrow.

*Not read:* what the folder buttons do. *Ours:* pressing one chooses its folder.

## 4. An entry in the tree

`confirmed` throughout. The window type `tech_entry` has:

| kind | names |
| --- | --- |
| buttons | `bg_finnished`, `bg_endless`, `bg_oneshot`, `bg_cant` |
| icons | `progress`, `icon_finish_type`, `tech_status_researching` |
| text boxes | `name`, `difficulty`, `year` |
| box of overlapping elements | `input_box` |

**The background** is one of the four buttons; the other three are hidden.

| the technology | shown |
| --- | --- |
| of one level, and the country has it | `bg_finnished` |
| of one level, not had, and may be researched | `bg_oneshot` |
| of many levels, and the next may be researched | `bg_endless` |
| may not be researched | `bg_cant` |

"May be researched" is `research.md`, section 5. A technology of many levels is never
shown finished: with every level had, the next may not be researched and it is `bg_cant`.

**`name`** is the technology's name for one of one level. For one of many it is the name,
`: `, **the level being worked towards** - one more than the country has - ` / `, and the
last level there is: `Small Arms: 3 / 12`. With every level had it is the name, `: ` and
the text of `TECH_AT_MAX`: `Small Arms: (Max)`.

**`progress`** is a bar, filled to `level * 100 / last level` hundredths, cut off - the
levels had, of those there are - and empty for a technology of one level. It is not how
far the current level has got; that is on the queue.

**`difficulty`** is the technology's difficulty behind `§G` under 3, `§Y` from 3 to 5 and
`§R` above 5.

**`year`** is the year the level being worked towards is due (`research.md`, section 3):
the start year for level 1, the first offset for level 2, and so many years more for each
level after.

**A technology of one level that the country has shows no difficulty and no year.**

**`tech_status_researching`** is shown while the technology is on the queue.

**`icon_finish_type`** is given the sprite `icon_` + the category the technology adds to
when a level is finished + `_small`, and **`input_box`** one `tech_category_entry` window
for each category that makes it cheaper, whose icon `tech_icon` gets `icon_` + category +
`_small`. In both, with no sprite of that name the icon keeps what its file gives it.

*Not read:* what clicking an entry does. The original's own tooltip keys say
`TECH_DC_TO_START` and `TECH_DC_TO_STOP`. *Ours:* a click chooses the technology for the
panel of section 7, and a double click puts it on the queue or takes it off.

## 5. Theories and practicals

`confirmed` The window `tech_categories` holds one `tech_category_skill_entry` window for
each theory and practical, put at the `positionType` named after it - its key +
`_position` - and filled on every pass:

- the icon `tech_cat_icon` has the sprite `icon_` + key;
- **`skill`** is how much of it the country has, to one decimal, cut off; **a dash when it
  has a tenth or less**; behind `§G` when the figure is another country's being shared
  with it;
- **`tech_skill_indicator`** is a bar of twenty steps: the whole part of the figure, at
  least one for any at all, at most twenty.

`likely` The bar's sprite has 21 frames, the first of them empty, and twenty-one settings
from nought to twenty: so the icon's frames are counted from nought, where a button's are
counted from one (`interface.md`, section 5). *Ours:* drawn that way.

*Ours:* nothing shares a category between countries yet, so no figure is green.

## 6. The queue

`confirmed` throughout. The list box `current_research` holds one `current_research_entry`
window for each project on the queue, in order. It is made again when its length or order
is not the queue's, and each line is filled on every pass.

| kind | names |
| --- | --- |
| buttons | `bg_current_green`, `bg_current_orange`, `bg_current_red`, `cancel`, `tech_prio_maxup`, `tech_prio_up`, `tech_prio_down`, `tech_prio_maxdown` |
| icon | `progress` |
| text boxes | `name`, `level`, `data` |

**The background** says how well the project is manned, from the leadership it is given a
day (`research.md`, section 2): `bg_current_green` with a whole point - 0.99 of one or
more - `bg_current_orange` with part of one, `bg_current_red` with none. The projects are
manned in the queue's order, so with 6.82 to spend the seventh is orange and an eighth
red.

**`name`** is the name, and for a technology of many levels `: ` and the level being
worked on: `Small Arms: 2`.

**`level`** is the level being worked on **in roman numerals**: `II`.

**`data`** is the day the level will be finished, **year first**: the year, the month's
name and the day, as `1936 April 14`. It is `?` when a day adds nothing. The days it takes
are what is left of the level over what a day adds, and one (`research.md`, section 3).

**`progress`** is how far the level has got, in hundredths.

*Not read:* what the five buttons do. *Ours:* `cancel` takes the project off the queue;
`tech_prio_up` and `tech_prio_down` move it one place towards the front or the back, and
the two `max` buttons all the way. Clicking the line chooses the technology and brings up
its folder.

## 7. The panel for one technology

`confirmed` unless said. Choosing a technology hides `tech_categories` and shows
`tech_details` in its place:

- **`photo`** is the picture `gfx/pictures/tech/` + key + `.tga`, if the install has one;
- **`name`** is the name and, in brackets, the level it would be: `Agriculture (5)`; a
  technology of one level always reads `(1)`;
- **`name_expl`** is the name of that level - **where a model's name comes from**: the text
  of `TAG_key_level` if there is one, else of `key_level`, else the technology's own name,
  with `: ` and the level for one of many. `inferred`: the separator between the three
  parts was not read, and is taken from the keys the localisation files have;
- **`desc`** is the text of key + `_desc`;
- `difficulty`, `year` and `input_box` as on an entry - but the input icons are the
  larger `icon_` + category, `likely`;
- **`icon_finish_type` is the full picture here, `icon_` + category with no `_small`.**
  The routine for the panel builds the name without it. This was drawn small until the
  maintainer saw it was the wrong size, 2026-10-08: the reading had it right and the
  first write-up of it said "as on a tree entry";
- `tech_status_finnished` is shown for a technology of one level that the country has,
  `tech_status_researching` for one on the queue;
- **`start`** reads the text of `ITEM_RESEARCH_START` and is disabled when the technology
  may not be researched; for one already on the queue it reads the text of `cancel`;
- **`effects`** is a list of what the technology does, one `detail_entry` window a line,
  its text box `text` holding the line.

*Ours:* `start` puts the technology on the end of the queue, or takes it off; the panel's
own `close` goes back to the theories and practicals. **The lines of `effects` are ours**:
the original writes each effect out in words, unit by unit, from a routine that was not
read; here a line is the effect's key and its figure, as the technology's file gives them,
and what a technology does to a kind of unit is not listed at all.

## 8. Ours

- **The screen is opened by the top bar's Technology button**, or with
  `--screen=technology`. `--research=key,key` queues technologies and `--technology=key`
  chooses one, for looking at the screen without a mouse.
- **What each element says is worked out in `OpenHOI3.Interface`** -
  `CTechnologyScreenText`, `CSliderText`, `CRomanNumerals` - and tested.
  `src/Screens/CTechnologyView.cs` and the four entry classes beside it find the elements
  and set them.
- **Every change to the queue is one command carrying the whole queue**
  (`CSetResearchQueueCommand`), which the game refuses if any technology in it may not be
  researched - so a project that has become impossible has to be cancelled before the
  queue can be changed otherwise.
- **What each project is given is worked out for the screen as the day's research would
  work it out** (`CResearch.ShareOut`), so the backgrounds and dates are right the moment
  the queue is changed, before a day has run.

## What is not established, in one place

1. **Little was compared figure for figure.** The maintainer used the screen on
   2026-10-08 and found the folder buttons, the queue's priority and cancel buttons,
   `start` and the queue's dates reading year first all right.
2. **What the buttons do**: the folders, an entry's click and double click, `cancel`, the
   four priority buttons, `start`, the panel's `close`.
3. **The effects' wording**, and what a technology does to units.
4. **Whether an icon's frames count from nought.**
5. **The separator in a level's name key.**
6. **Sharing.** A category another country shares shows green, and an entry's input icons
   may show the sharer's figure; nothing shares yet.
7. **Tooltips**, which on this screen carry most of the detail: what a level costs, why a
   technology may not be researched, what a priority button does to a project's speed.
