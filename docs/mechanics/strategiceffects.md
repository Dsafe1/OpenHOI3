# The Strategic Effects window

`modifier_window` of `interface/modifier_interface.gui`, headed `MODIFIERS_ACHIEVMENTS` - "Strategic Effects": every
triggered modifier the player's country might have, those it has drawn plain and the others greyed, each with how
many of its conditions are met. Read out of the original on 2026-10-14
(`reversing/findings/FINDINGS-countrymodifier.md`, section 3; the tally's rules
`reversing/findings/FINDINGS-triggereval.md`, sections 10 to 12). What a triggered modifier is and when a country
has one is `modifiers.md`, section 5. `confirmed` unless marked.

**P** is the player's country.

## 1. Opening it

- **`menubar_modifiers_button`**, by the minimap in `menubar`, opens it where it is shut and shuts it where it is
  open. Its tooltip is `STRATEGIC_EFFECT_BUTTON_TOOLTIP`, one line.
- Opening it fills it at once (section 2). It also shuts the window `menubar_goals_button` opens, and that button
  shuts this one: the two never stand together. *The goals window is not built.*
- `close_button` shuts it.
- **Nothing is shown where nobody is played.**

## 2. What it lists

While it is open it is filled again **once a game day**, and as it opens:

    rows = every entry of triggered_modifiers.txt, in file order, whose potential holds for P
           (the potential block alone, in a scope of P with no FROM and no province)
    where the list has another number of rows, it is emptied and made again with that many

So an entry P could never have - `winter_war` for anybody but Finland - is not listed, and one P could have and has
not is.

## 3. A row, `modifier_entry`

For each listed entry, in order, on the list's next row:

- `modifier_name`: the entry's name, its key localised.
- `modifier_effect`: **every value of its modifier**, one line a kind - the kind's own name, `": "`, a coloured sign
  and figure, `§W` (`modifiers.md`, section 8).
- `modifier_strip`: the frame of the entry's **`icon`**, a 17-frame strip, **counted from one** as every frame is -
  `icon = 1` its first frame. Corrected 2026-11-27 on the maintainer's look at the original: this said the strip was
  counted from nought, guessed from the file's icons running 1 to 16 on a strip of 17 frames, and every icon was drawn
  one frame late. What `icon = 0`, or none, draws is not known; *ours* draws the first frame.
- **Plain where the entry is on P's list, greyed where it is not**: the row's background (`modifier_entry_bg`) and the
  strip are drawn in the buttons' "up" or "disabled" look (`interface.md`, section 5). The routine sets the states 1
  and 4; that they are those two is `inferred` from the shader's order counted from one.
- `modifier_status`: `n / m`, how many of the entry's **`trigger`** conditions hold, counted as in section 4, out of
  how many there are.
- `modifier_progress`, a bar standing upright: n x 100 / m in hundredths, nought where m is nought.
- `modifier_info`, the "?", is drawn as its file has it and carries the row's tooltip (section 5);
  `modifier_progress_overlay` is drawn and does nothing.

## 4. The tally

The `trigger` block is an "all of", and each of its conditions counts so, P's scope given to each:

| condition | counted | met |
| --- | --- | --- |
| a plain condition | 1 | where it holds |
| "all of" (`AND`), and a block over a set - `any_owned_province`, `any_core`, `any_neighbor_country`, `any_neighbor_province`, `enemy`, a region | its own conditions, each as this table counts it, **in the scope it was given** | as they are met |
| "one of" (`OR`) | 1 | where any of its conditions holds |
| "none of" (`NOT`) | its own conditions as this table counts them | those counted **less** those met |
| a scope - `GER = { }`, `1626 = { }`, `controller = { }` | its own conditions, in the scope it names | as they are met |

- **A block over a set does not move the scope**: `any_owned_province = { ... }` counts its conditions in P's own
  scope, not in any province's. The original's.
- **A scope that names nothing counts its conditions all the same**, in a scope of nobody, where asking whether it
  holds would have refused. The original's.
- "None of" with nothing in it counts nothing.
- Every condition is asked again for the tally, whatever it answered before; "all of" does not stop at the first
  that fails.

So `naval_supremacy_in_the_west`, ten provinces each asked whether its controller is in P's faction and a "one of"
over ten `controls`, says `n / 11`.

## 5. Tooltips

- **A row's "?", `modifier_info`, and nothing else in it** says the entry's **`trigger` block's account of itself**
  - the window hands the element to each row in turn, and a row takes it only where it is that row's
  `modifier_info`. Corrected 2026-10-14 on an account of the original ("It should only show on the '?' icon"), which the
  routine then bore out: the first reading had every element of a row answer. In P's scope: a line for each
  condition with a green or red asterisk, as a decision's `allow` and a technology's requirements are written
  (`scripttext.md`, section 3).
- The button's, section 1. Nothing else in the window says anything.

## What looks wrong and is built so

- **The tally counts a block over a set in the wrong scope** and **a scope that names nothing as though it named
  somebody**, so `n / m` can disagree with whether the modifier applies (section 4).
- **The rows say nothing of `potential`**: a modifier whose `potential` holds but whose `trigger` stands at `m / m`
  can only be one the day has not yet tested.

## Ours

- The goals window is not built, so opening this one shuts nothing.
- `--screen=strategiceffects` opens it at the start, for a look.

## Not established

- What the frame of an icon set to nought is in the original (section 3).
