# Windows a mod defines

**Ours**: an enhanced game's only (`docs/enhanced.md`, *The shape*), and the way BlackICE's utility comes to this
engine (`docs/blackice.md`, step 6). Nothing of it is in the original. Built 2026-11-22 as far as the window, pages
that read, and the first page that changes the game, by an order (section 6.1).

**They are drawn with Dear ImGui**, by a page's own Lua - the user's decision of 2026-11-22, on seeing the first
shape of this, pages laid out in the original's `.gui` elements: "I don't really like the way it looks. Maybe instead
of trying to make use of the existing gui framework, we use imgui as a base for the modded ones." That shape lasted
half a day and is gone. What stays of the rules given for it, all the user's:

- **Every word is the localisation's**; the engine words nothing (2026-11-21). A page's Lua asks for each text.
- **Nothing registers the pages in one file**, which a submod would have to overwrite: a page is there by its file
  being there (2026-11-22).

What a mod declares in `.gui` files for this engine - the game options' rows (`gameoptions.md`, section 5) - is for
what stands inside the game's own windows. A page of a mod's own is Lua and its texts, and no `.gui`.

## 1. A page is one file, and its texts

| | where |
| --- | --- |
| the page | a `.lua` file of the mod's `script/openhoi/windows/` |
| its texts | a `.csv` of the mod's `localisation/openhoi/` |

Both folders are read in an enhanced game alone, and the original opens neither: its listing of `localisation`
takes one folder and drops directories, and it does not list `script` at all
(`reversing/findings/FINDINGS-guifolders.md`).

**The engine lists `script/openhoi/windows/*.lua` over every layer** (`CLuaWindows`), each name once, from its
frontmost layer, as every listing of a folder goes. So a submod adds a page by adding its file, and replaces one by
shipping a file of the same name.

## 2. The page's file

It is run once, as the pages are first asked for, as the body of a function - so what it declares `local` is its own
and is kept from frame to frame: a filter's text, a ticked box - and what it returns is the page:

    return {
        title = "OPENHOI_WIN_FLAGS",             -- the localisation key of its name, in its tab's list
        order = 20,                              -- its place in that list: lower first, then by file name
        group = "OPENHOI_WIN_GROUP_INSPECTOR",   -- the localisation key of the tab it stands under
        group_order = 40,                        -- where that tab stands among the tabs (section 5)
        Collect = function(player) ... end,
        Draw = function(data, player) ... end,
    }

- The file's name without `.lua` is the page's name: letters, digits, `-` and `_`.
- A file that does not parse, fails as it runs, returns no table or has no `Draw` is no page, and is reported with
  the Lua error, as every Lua error is.
- `player` is the tag of the country played, as text.

**The pages run in the Lua state the screens have for themselves** - the one a dialog asks an AI's opinion in
(`docs/multiplayer.md`, section 12), with the install's scripts loaded and the classes the AI's scripts are given -
never the state the AI's ministers run in.

## 3. `Collect` reads the game, `Draw` draws

They are two because of when each runs.

- **`Collect(player)` reads the game** and answers whatever the page will show - any Lua value, kept for it. It is
  asked as the page opens and then **every two seconds** of real time while the page is open, and **never while the
  game's hours run beside the frame** (`tick.md`, section 2): a page opened then waits for them, undrawn.
- **`Draw(data, player)` draws the page** from what `Collect` last answered - `nil` where the page has none - with
  the widgets of section 4. It runs **every frame** the page is open, the frames the hours run in among them, as the
  library asks of a window that is to stay on screen. So it is **given no game to ask**: a class of the game's called
  there raises a Lua error. Anything of the game a page shows, its `Collect` has put into the data.

**A `Draw` that fails is said in its window**, the Lua error's text in the place of the page, and its Lua is not run
again until the page next gathers: one line in the log, not one a frame. A page that asks the game while it draws
fails so.

## 4. The widgets: `OpenHOI3.ImGui`

A page draws with a table of functions under the names Dear ImGui gives them, so that what is known of that library
carries over (`CLuaImGui`; `IModWindowCanvas` is the list). The window itself - its title bar, its close box, its
place and size, which the library remembers - is the engine's; a page draws what is inside.

| | |
| --- | --- |
| text | `Text`, `TextDisabled`, `TextWrapped`, `TextColored(r, g, b, a, text)` |
| layout | `Separator`, `SeparatorText(text)`, `SameLine`, `Spacing`, `NewLine`, `SetNextItemWidth(width)` |
| pressed, answering true or false | `Button`, `SmallButton`, `Selectable(label, selected)`, `CollapsingHeader` |
| a value, answering `changed, value` | `Checkbox(label, value)`, `InputText(label, text [, most])`, `InputInt(label, value)`, `SliderInt(label, value, least, most)`, `SliderFloat`, `Combo(label, choice, list)` - the choice counted from one |
| tables | `BeginTable(id, columns)`, `TableSetupColumn`, `TableHeadersRow`, `TableNextRow`, `TableNextColumn`, `EndTable` |
| tabs | `BeginTabBar(id)`, `BeginTabItem(label)`, `EndTabItem`, `EndTabBar` |
| parts | `BeginChild(id [, width, height])`, `EndChild`, `TreeNode`, `TreePop`, `PushID`, `PopID` |
| greyed | `BeginDisabled(disabled)`, `EndDisabled` - what stands between is greyed and takes no press where `disabled` is true; ended whatever was given |
| the item before | `IsItemHovered`, `SetTooltip(text)` |

As in the library, a `Begin...` answers whether what it begins is open - a table off screen, a tab not chosen - and
only an open one is filled and ended, but a child, which is ended whatever it answered.

**The engine keeps the pairing for the page.** The library forgives neither an `End` too many nor one too few, and a
page's Lua may fail half way through a table. So what a page began and did not end is ended for it when its drawing
is over, the last begun first; and an `End...` with nothing of its kind open is passed over, the page told so in its
window as a failed drawing is.

**More widgets are added as pages need them** - "lets not limit ourselves" (the user's, 2026-11-22). The list is a
choice, not the library's whole.

## 5. The one window: tabs, a list of pages, a page

**One window holds everything** - the user's, 2026-11-22: where BlackICE's overlay has a small main window that
switches others on and off, here "only … a single window. The tabs on this window are the ones from the original
main, and the pages from those windows should be listed vertically on the left side." Its first shape here, a menu of
tick boxes with a window to each page, lasted an afternoon.

The **Insert** key shows and hides it - the key the overlay had. It is titled by the key `OPENHOI_WINDOWS_MENU`, and
within it (`CModWindows`):

- **a tab along the top for each group of pages**, named by the group's key localised;
- **the tab's pages listed down its left side**, each by its title localised (its name where it has none), the one
  chosen marked;
- **the page chosen on the rest of the window**, drawn by its Lua.

**Nothing declares the tabs**: a tab is there because a page names it. Two pages whose `group` is the same key stand
under one tab; a page naming none stands under a tab named by `OPENHOI_WINDOWS_UNGROUPED`. So a submod's page joins a
tab, or brings a new one, by its file alone (`CModPageGroup`).

- **The tabs' order**: by the lowest `group_order` any of a tab's pages gives - one not given counts as nought -
  and then by key. BlackICE's follow its overlay's: Setup 10, Country Info 20, Game Info 30, Inspector 40, Stats 50,
  Options 60, Help 70, Debug 80.
- **A tab's pages**: by `order`, then by file name.
- **Each tab keeps the page chosen under it** while another tab is looked at, and while the window is hidden; a tab
  first looked at shows its first page.
- **Only the page looked at runs**: it alone gathers and draws. Chosen, it gathers before it is first drawn, and
  shown again after the window was hidden it gathers afresh.

## 6. What the engine gives a page

Under the global `OpenHOI3`, beside `Files`, `Enhanced` and `Fixes` (`mods.md`, section 6), once the pages are loaded:

- `OpenHOI3.ImGui` - section 4;
- `OpenHOI3.Localise(key)` - the key's text, or the key where no table has it; it asks no game, so `Draw` may call it;
- `OpenHOI3.CountryFlags(tag)` - the names of the flags that country holds, sorted by name without case; a flag set
  and cleared again is not among them; empty for a tag that is no country's, and where no game is asked;
- `OpenHOI3.GlobalFlags()` - the same of the game's own flags;
- `OpenHOI3.Conversion(tag)` - a country's two conversions (`ic.md`, section 4.1), or nil for no country or no game:
  `played`, `oil_rate`, `energy_cap`, `energy_floor`, `crude_cap`, what it holds as `energy`, `crude_oil` and
  `fuel`, and `energy_conversion` and `crude_oil_conversion`, each `{ own, per_day, keep, target }` - figures in a
  stockpile's units;
- `OpenHOI3.SetConversion(tag, which, own, per_day, keep, target)` - **an order**, section 6.1: `which` is `energy`
  or `crude_oil`. Answers whether it was understood.

### 6.1 A page that changes the game

**A page changes nothing itself: it gives an order**, as a player does, and the order is a command of the game's.
An order asks no game, so a page gives it as it draws - on a button's press. What a page gave is kept
(`IModPages.TakePosted`), and the screen posts it when the page's drawing is over, through the one way into the game
from outside (`CSimulation.Post`): so it waits by itself where hours run beside the frame, goes to the host between
several machines, and is recorded and replayed like any order. A quarter of a second on the page gathers again, to
show what came of it.

A page holds what is typed until its own button sends it - BlackICE's conversion page has an Apply for each half -
so that half a number is never an order.

**The orders a page may give are the ones the engine names**, each a function under `OpenHOI3` that makes one
command: the conversion's is the first. A command a mod defines itself, its body an effect block
(`docs/enhanced.md`, *What mods build on*), is not built.

More come page by page (`docs/enhanced.md`, *What mods build on*: variables, timed modifiers, modifier totals, a
province's figures, a leader's state, the selection).

## 7. How it is drawn

- **The library** is Dear ImGui through ImGui.NET, drawn by the plugin "Dear ImGui for Godot", which is kept in
  `addons/imgui-godot` as it was released, its C#-only package - somebody else's code, to which this project's rules
  of style are not applied; `addons/imgui-godot/third-party.txt` says where each part is from. All of it is under
  the MIT licence, and their notices go with an export (`tools/build_engine.ps1`). The plugin's root is an autoload
  of `project.godot`.
- **The typeface is one of ours**, Liberation Sans at 16, as everything drawn is: the library's own default is a
  bitmap a dozen pixels high.
- **The windows are over everything** and scale with the canvas. The node that draws them is the session's own and
  is processed always (`CModWindows`) - not the HUD's, which is not processed while hours run.
- **What the player arranges** - where each window stands, its size - the library keeps in its own `imgui.ini`, in
  Godot's folder for this game's user data and nowhere in an install.

## 8. Checked

`LuaPagesTests`, with a canvas that writes down what it is asked: the pages of a folder in the menu's order, a file
that fails, a page gathering with the game and drawing without it, a widget's `changed, value`, what a page left open
ended for it, a failed drawing said in the window until the page gathers again, and a page that asks the game while
it draws. And on 2026-11-22, BlackICE's working copy played from a linked stand-in install: a window of the library
drawn in the client and in an export; and with `--screen=modwindows:flags` the window on BlackICE's first page, Active
Flags - Germany's tag, a filter box and a table of its three flags after thirty days.

**The mouse and the keyboard were tried by the user by hand**, 2026-11-22, in an export, in the first shape - a menu of tick boxes, a window to a page: a page ticked on the menu and
shut by its close box, the filter typed into, a window dragged and resized, a click on a window kept from the map
beneath, and no shortcut of the game's set off by typing - all as they should be. **One thing was not, and is
mended**: an edit box of the game's that has the keyboard - a unit being renamed - took the keys typed into a box of
a page as well, the original's rule being that an edit box keeps the keyboard until another is given it
(`interface.md`, section 8), and a page's box being no such other. So while a box of a page's is typed into
(`CModWindows.IsTyping`, the library's own word for it) the game's edit box takes no key (`CGuiContext.TypingElsewhere`),
and the arrow keys, which the map asks of the keyboard each frame, do not scroll it. Not tried again since.

**The conversion page**, 2026-11-22: `LuaPagesTests` has a page read a country's conversions and give an order as it
draws, the order then run; and with `--screen=modwindows:conversion` BlackICE's page was drawn under Country Info,
Germany's rate -0.90 and the line that says the conversion is shut. **Its Apply buttons were pressed by nobody
yet** - a scripted click does not reliably reach a widget of the library.

## Not built

- Orders beyond the conversion's (section 6.1), and a command a mod defines itself.
- A key of the mod's choosing in the place of Insert; a button for the window on a screen of the game.
- A theme and a text size of the player's choosing, which the overlay has.
- Sorting a table by a column's header, and the rest of the library's widgets.
- The pages in the front end: they are a game's.
