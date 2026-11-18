# The convoy windows and the go-to box

*Spec for the windows a player makes and manages convoys with, and `goto_box`: `reversing/findings/FINDINGS-convoywindows.md`. Read out of the original, tried with `--click`, and they have not been reviewed in play. Files: `tfh/interface/convoys.gui`, laid over `interface/convoys.gui`, and `interface/goto.gui`. Figures are exe-hardcoded unless a define is named.*

`CConvoyWindowText`, `CGotoBoxText` (`Interface/Screens`), `CConvoyDialogs`, `CConvoyEntry`, `CGotoBoxView` (`src/Screens`), the eight commands in `Sim/Commands`.

## 1. What there is

The production screen has `convoy_settings`, with `convoy_creation_window` behind it, and `convoy_details`. None pauses the game. All three go when the screen does. A dialog comes up over the other as it opens (*ours*: the original's layering was not read).

## 2. The list

`convoy_queue`: the country's convoys in the order they were made, then for each country it gives lend-lease to, that country's first such convoy. Made again when the count changes, the rows filled each frame.

A row: `convoy_name`, `<start> - <end>`, **red** where its way is under two provinces, it has no transports or its route is not usable, otherwise green at full efficiency and yellow under it; `icon_*_mini` for each good it carries; `num_convoys`, transports over those wanted - the giver of a lend-lease convoy sees its efficiency instead; `lend_lease`, "IC to" or "IC from" with the other country's flag. `select` opens the details, greyed on a lend-lease convoy that is not the player's.

Tooltip: what it shipped yesterday and its efficiency, `CONV_SHIP_IRO`, `NO_CONVOY_SHIP`, `CONV_EFF_IRO`; on a lend-lease convoy `LEND_LEASE_CONVOY_DESC`, `LL_CONVOY_EFF_DESC`, and for someone else's the delayed `LEND_LEASE_CONVOY_OWNER_DESC`. *Unbuilt:* `LL_CONVOY_EFF_LOW_DESC`, whose number of days was not read.

## 3. `convoy_settings`

`convoy_button` ("Create convoy") shows it; `close` shuts it and the port list. The three boxes take the country's automatic flags **when it is opened and at no other time**, so a box keeps its own tick until then, and each posts a toggle.

For each port the country holds: `create_resource` is possible where the port's block is not the capital's and no convoy of the country starts in that block; `create_supply` where no convoy of any country has the port on its way. Each is greyed besides while its automatic flag is on.

## 4. `convoy_creation_window`

**No map, no route drawn: two choices from a list.** `title` is `CDW_ORIGIN`, then `CDW_DESTINATION`. The window's `cancel` stands at x 5770, out of reach, as the file has it. Leaving it is by finishing, `close`, or pressing a create button again.

The ports offered: held by the country, with a naval base, and not the origin chosen.

| | origin | destination |
| --- | --- | --- |
| supply | in the capital's block | out of it, or in it more than `SUPPLY_SAME_AREA_DIST_CUTOFF` steps from its depot |
| resource | out of the capital's block, and no convoy starting there | in the capital's block |

A supply convoy is offered no port where a non-trade, non-lend-lease convoy for supplies or fuel already ends. The second choice posts the convoy. It takes shape when the command runs and starts with no ships.

## 5. `convoy_details` and the commands

`convoys_running`, `convoys_available`, both ports; each good's figure is what it **shipped the day before**, not what it may carry. `transports_value` is transports over those wanted. Steppers, one or Ctrl's ten: transports up to what is wanted and free, escorts up to what is free, down to none; a stepper greys with nothing to move. The six boxes are set **only as it opens**; a change of any posts all six, money always off. The boxes and `cancel` are greyed on a trade or lend-lease convoy. It closes itself when its convoy goes.

| command | does |
| --- | --- |
| `CCreateConvoyCommand` | a convoy for whoever holds the origin, with the same making as the supply system's; asks nothing |
| `CCancelConvoyCommand` | off the list, its ships back in the free pools |
| `CChangeEscortsInConvoyCommand`, `CChangeTransportsInConvoyCommand` | moves ships from or to the free pool; refused for more than there are |
| `CChangeResourcesInConvoyCommand` | the seven goods at once |
| the three toggles | turns a flag |

**Nothing marks a convoy as hand-made.** While an automatic flag is on it can undo what is done by hand, and the steppers are not greyed for it: the original's.

## 6. What else looks wrong and is the original's

A supply convoy may be given resources and the other way about; the owner of a new convoy is whoever holds the origin; the steppers stay live on the receiver's lend-lease convoy.

## 7. Tooltips

While `convoy_settings` is up nothing on the production screen speaks; while `convoy_details` is, only its four steppers, `BUILD_ADD_REM_BUNDLE` with 10. The windows' other parts, `convoy_button` and the port rows say nothing.

## 8. The go-to box

`button_goto` (shortcut F) brings up `goto_box`; that it shows and does not toggle is inferred. It is moveable and does not pause. Typing refills `provinces`, with **more than one character**:

- a number in range gives that province's row and lets `goto` be pressed;
- every province by number, skipping names of five letters or more that start `PROV`, whose lower-cased name begins with the text gets a row; a name equal to the text lets `goto` be pressed.

`goto` or Enter goes: to the number typed, whatever province that is, or the first by number whose name begins with the text. **A row's button goes at once**, by its exact name. Going centres the map and selects nothing; the text is emptied, the list cleared and the box put away. `cancel` only hides it, leaving the text.
