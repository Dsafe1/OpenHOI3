# Battle plans

*Spec for `battleplans_window` and drawing on the map: `reversing/findings/FINDINGS-battleplans.md`, `reversing/findings/FINDINGS-battleplandraw.md`. Read out of the original, tried with `--click`, and they have not been reviewed in play. Files: `tfh/interface/battleplansinterface.gui`, `.gfx`, `battlearrow.gfx`, `battleplans/bp_settings.txt`. Figures are exe-hardcoded unless a file is named.*

`CBattlePlan`, `CPlanStroke`, `CPlanIcon`, `CPlanText` (`Sim/Plans`), `CBattlePlanFile`, `CBattlePlanSettings`, `CPlanObjectTypes` (`Content/BattlePlans`), `CBattlePlanBook`, `CPlanEditor`, `CArrowMesh`, `CBattlePlanText` (`Interface/BattlePlans`), `CBattlePlanWindow`, `CBattlePlanRow` (`src/Screens`), `CMapSurfaceBattlePlans.cs`, `CBattlePlanSigns` (`src/Map`).

A plan is the player's drawing. **Nothing simulated reads it, so it changes without commands**, and the AI knows nothing of it.

## 1. The bar's buttons

`menubar_plans_toggle` (shortcut `b`) turns whether plans are drawn with the window shut: its frame 1 on, 2 off; greyed while the window is up. `menubar_plans_open` opens or shuts the window. Neither pauses. Plans are drawn where the toggle is on or the window up, each plan by its own `visibility`: several at once.

## 2. The window

Moveable, not modal, pausing nothing. A map click still selects, unless a tool takes it. Shown, it goes to Current with save mode off; hidden, what is selected on the plan is let go.

## 3. The tabs

Current (the player's plans in their order, then each other country's shared one whose allies hold the player), Historical (`battleplans/historical/<TAG>`, every file of it over the layers by the original's lister, names sorted by byte with case - `mods.md`, section 2; a file whose header fails is left out, which is how the `#empty` files go) and Saved (`battle plans/<TAG>` beside the save games; a bad header stays a row). Which row of buttons shows is the tab's, or the name box in save or rename mode. A tab's frame is 1 chosen and 2 not. `tab_saved` has no title text: its art has the words.

## 4. The rows

`flag`, `title`, `file_path` (a file row: between the last slash and the first point), `visibility` (a live plan; frames 1 and 2), `share_with_allies` (a live plan of the player's). `background_button` chooses. With nothing chosen the first of the player's is. `countries` and `share_plan` are never looked up by the original.

## 5. The actions

No button is greyed; one with nothing eligible does nothing. **New** makes a plan titled `BP_NEW_PLAN`, chooses it and opens Rename on it. **Remove** asks `BP_DELETE_CURRENT_PLAN_PROMPT`. **Save** goes to Saved with the title in the box. **Rename**. **Import** (`load_plan`) reads a file row whole into the country the file names, drawn, the row and tab staying. **Delete** asks `BP_DELETE_SAVED_PLAN_PROMPT` and removes the file.

The name when saving: trimmed; empty does nothing; a byte of 0x80 or above or a slash raises `BP_ILLEGAL_FILE_NAME`; `.pln` is added unless the name ends in exactly that with a point past the first character; an existing file asks `BP_SAVE_PLAN_PROMPT`.

## 6. The plan

A title, a country, whether visible and shared, and four lists. Coordinates are map pixels, each object's points about its own origin. Nothing limits how many there are.

## 7. The format

Read by position, each key compared with the next expected and one missing not consumed; numbers whole thousandths, written `floor((value + 0.0005) * 1000)`; a block with nothing in it is not written. `title`, `country`, `visibility` in a saved game alone, `arrows_count` and N `control_points` and `text`, `matrix_transforms`, `ignore_smoothing`, `type`, `color`; `icons_count` and `positions`, `matrix_transforms`, `type`, `color`, `rotation`; `texts_count` and `positions`, `matrix_transforms`, K `text`, `scale`, `rotation`; `is_battle_plan_shared`; `borders_count` as the arrows.

A saved game holds a `battle_plan` block for each plan. `OpenHOI3.SaveCheck battleplans <install>` writes every shipped plan back and sets the two side by side.

## 8. `bp_settings.txt`

The file is taken whole. `tool_color`, `tool_arrow`, `tool_borders`, `tool_icons`, each an `icon` and `option` blocks; a colour's are `name`, `red`, `green`, `blue`, the others' `name`, `type`. A colour past the palette's end is magenta.

## 9. The tools

`select`, `arrows`, `borders`, `icons`, `colors`, `text`, `erase`, shown only on Current with a plan of the player's chosen and no name being typed. The chosen one wears `marker`. Each tool's option is remembered, a right-aligned grid of seven to a column. `erase` deletes what is selected and leaves the tool. `colors` recolours what is selected, **a text sign excepted**.

Every tool takes the left press; plain select leaves it to the map as well. Right clicks are never taken. Shift and the wheel turn by 0.15 radians, Ctrl and the wheel size in steps of 0.75 between 0.25 and 20, with select, icons and text.

**Select**: a release within 5 of its press clears the selection and tests: icons, signs, then arrows by their points (squared under 31.25, the nearest), then borders. **Arrows and borders**: a press, the mouse, a release; Shift at the press branches off the nearest arrow. **Icons**: press, hold, release. **Text**: a preview follows the mouse and a press drops it; the box `apply`s to a selected arrow's label or sign, an empty one deleting a sign.

## 10. The stroke

The first point twice; a new point once the last but one is over 5 away; the last always under the mouse. On release, more than two points: each but the ends a quarter itself and three quarters its neighbours' middle, from the old values. A Catmull-Rom spline, four samples a segment. Two vertices a sample, `start` to `stop` half-widths along it, `u` the arc length over `textureloop` where the type has one and the progress otherwise; four more for the head. A branch's prefix goes on with its parent's progress.

## 11. Arrows, borders and icons on the map

Through the install's `battlearrow.fx` (`BattleArrow`, two passes) and `mapinfo.fx` (`MapInfo`), at height 0.5 with no terrain, the depth tested and not written. A selected object pulses. Each is drawn where it is and a map's width east.

The arrows go a colour at a time, the stencil cleared between colours (`reversing/findings/FINDINGS-battleplandraw.md`), and the effect's stencil - equal to nought, incremented where it passes - lets a pixel be painted once in a colour: two arrows of one colour crossing paint the crossing once, the first pass's fill before the second's outline, and an arrow of another colour blends over them. The icons are drawn after every arrow. Godot clears its stencil once a frame, so each colour's passes write a value of their own (`shaders.md`, section 3.7), and each colour's fills and outlines have their own render priority, in the palette's order. Checked with `--click` on 2026-11-13: two red arrows crossing are one red where they cross (168,112,93, where the same run without the stencil gave a darker 193,85,72), and a blue arrow through them blends with both.

*Ours:* within a colour **every fill is drawn before any outline**, where the original draws each arrow's fill and then its outline, the arrows in an order a sort of its own gives (its comparison not read). Godot's sort of what blends does not keep the order a mesh was drawn in, so the original's cannot be followed. It shows only where two arrows of one colour cross: there the original lets the first arrow's outline cut across the second's fill, and here a fill always covers the other arrow's outline. A colour past the fiftieth shares the fiftieth's stencil.

## 12. Text on the map

*Ours*, by the settled decision that bitmap fonts are not drawn: signs and arrow labels are the typeface `allies_country` stands for, on the map's 2D layer, not through `mapinfo.fx`. A sign's size is the font's pixels times 0.05 times its size, centred on its point. A label is bent along its arrow's curve from the arrow's sixth point (after a branch's prefix), the path reversed where it runs west.

## 13. What is left out

Sharing goes nowhere without a second player: the flag is kept and saved. *Ours:* icons are hit-tested before signs, where the original keeps them in one list.
