# Native pages

An enhanced game's own: a mod's Lua fills a window of the game's own interface. Nothing here is the original's, where
what a window does is in the executable and no mod can add a screen.

Decided 2026-11-24. Asked how BlackICE's two slider pages - the mod's own Lua over country variables - should become
"native game GUI", the user chose **Lua-driven native windows** over moving the logic into the engine and over an
engine screen hardwired to the mod's variables: a general way for a script to fill and read a window declared in
`interface/openhoi`, the logic staying the mod's. It is the mod's own windows (`modwindows.md`) with the game's
interface in the place of the drawing library, and the two share everything but how a page shows itself.

`INativeWindowCanvas`, `CModPage` in `lib/OpenHOI3.Content/Windows`; `CLuaGui`, `CLuaWindows` in `lib/OpenHOI3.Lua`;
`CNativePages`, `CNativeWindowCanvas` in `src/Screens`.

## 1. In a line

A page's file under `script/openhoi/windows` that names a `window` is a native page: the engine makes that window
from the interface files, and the page's `Fill` sets its texts and asks what was pressed and typed.

## 2. A page

    local Gui = OpenHOI3.Gui
    return {
        window = "my_mod_window",            -- a windowType of interface/openhoi
        host = "production",                 -- the country screen it belongs to; optional
        opener = "my_mod_window_opener",     -- a window with a button "open", shown on the host; optional
        Collect = function(player) return { ... } end,
        Fill = function(data, player) Gui.SetText("title", ...) end,
    }

**A page that names a window must have a `Fill`** and needs no `Draw`; one without is reported and is no page. It is
left out of the mod's own windows' menu. Its name, its file's reading and its `Collect` are a page's as
`modwindows.md` has them: **`Collect` alone may ask the game** - as the window opens, every two seconds while it is
up, a quarter of a second after an order of the page's own, and at once after `OpenHOI3.Refresh()`, never while the
game's hours run - and what the mod's Lua posts there is sent as the player's order.

**`Fill(data, player)` runs every frame the window is up**, from what `Collect` last answered, and asks no game. The
window is not shown until it has been gathered and filled once. A `Fill` that fails is reported once and the window
left as it stands until the page gathers again.

## 3. Where it is opened from

- **`host`** names a country screen: `production`, `technology`, `diplomacy`, `politics`, `intelligence` or
  `theatres`. The page's window goes when that screen shuts.
- **`opener`** names a second window of the interface files, put up at its own position for as long as the host
  screen is open, under every page's window. Its button **`open`** opens the page's window and shuts it again.
- The page's window's button **`close`**, where it has one, shuts it; so does `Gui.Close()`.
- `--screen=native:<page>` opens one by its page's name, host or none.

A page with no host has no opener of the engine's and is opened by that switch alone for now. *Not built:* opening
one from a page of the mod's own windows, or from a key.

Both windows are the mod's to lay out and place: their positions are on the 1920 by 1080 canvas, as every window's
(`interface.md`). A window whose file says `moveable` is dragged.

## 4. `OpenHOI3.Gui`

Every function takes an element by its name in the page's window - an icon, a text box, an edit box or a button. An
element in a list's row is `list#3/name`, the rows counted from one. **A name the window does not have does nothing,
answers false or an empty text, and is reported once.** Outside a `Fill` every function does nothing.

| function | |
| --- | --- |
| `SetText(name, text)` | a text box's or a button's words. An edit box takes them as what it holds - **but not while the player is typing in it** |
| `Text(name)` | what a text box says or an edit box holds |
| `Edited(name)` | whether an edit box was typed into since this was last asked |
| `Pressed(name)` | whether a button was pressed since this was last asked |
| `Checked(name)`, `SetChecked(name, ticked)` | a `checkboxType`'s tick |
| `SetVisible(name, shown)` | |
| `SetEnabled(name, enabled)` | a button greyed and unpressable, or not |
| `SetTooltip(name, text)` | empty for none |
| `SetFrame(name, frame)` | an icon's or a button's frame, from one |
| `SetColour(name, r, g, b [, a])` | the element tinted, each 0 to 255; `SetColor` is the same |
| `SetRows(list, entry, count)` | a list given so many rows of an entry window, at most 2000; the rows are kept where the count and the entry are the same, and made afresh where not |
| `Close()` | the window shut |

**A page's edit box gives the keyboard up to a press of the mouse anywhere else**, and its caret goes with it - the
original's boxes keep the keyboard until another takes it, and those are left so. A box's caret is drawn only while
it has the keyboard, and taken away as another box takes it (until 2026-11-24 the old box kept its last drawing).

**An edit box takes typing from the first time the page names it** (`interface.md`, section 8), and a button's press
and a box's typing are remembered from the first time they are asked for - so a page asks every frame, as it would of
the drawing library.

A press or a typing is the page's to act on: it changes what the page holds and, where the game is to change, notes
it for the next `Collect` and calls `OpenHOI3.Refresh()` - the page of `modwindows.md`, section 6.1.

## 5. What a window is made of

Anything a `.gui` file may hold. With **built-in elements** (`builtin.md`) it needs no texture: a `panel` for its
ground, a `header`, `row`s, `button`s, and a `field` under each `editBoxType`. A tick with no sprite sheet of its
own is a `field` button whose words the page sets to `X` - BlackICE's `script/openhoi/nativeform.lua` does it so.

## 6. BlackICE's three

`script/openhoi/windows/country_info/prodsliders.lua` and `lssliders.lua`, over `interface/openhoi/sliders.gui`:
**Prod. Sliders AI** behind a button on the production screen and **LS Sliders AI** behind one on the technology
screen, each the form the mod's own window had - Enable, Apply, Reload, the country, what became of the last Apply -
with the mod's own data functions behind it unchanged. **A production category's priority is moved by two arrows**,
the production queue's own, a step at a time between one and the number of categories - up a higher priority, which is a lower number - the category that had the
number taking the one left - so no two share one - where the mod's own window had a box to type it into; asked for
on 2026-11-24. Tried with `--click` on 2026-11-24: opened from their
buttons, a box typed into, a tick and a mode turned, Apply answered "Applied". Not reviewed in play.

**Trade AI** is the third, since the same day: `tradeai.lua` over `interface/openhoi/tradeai.gui`, behind a button
under the sliders' one on the production screen, the two together in the middle of the gap between that screen's pairs of buttons - the largest daily deficit, and for each resource its daily surplus and, but
for money, the stockpile it sells above and the one it stops buying above. Tried with `--click`: opened from its
button, Apply answered "Applied". With it none of the mod's three AI settings pages is in its own windows' menu.

**A window a unit is ordered from counts as the production screen shut** (2026-11-25): while one of the builder windows is up, a page hosted on the production screen has its opener and its window taken down, as when the screen is closed (`CProductionView.BuilderOpen`, `CSession.HostScreenOpen`). The opener comes back with the screen; the window stays shut until it is opened again.
