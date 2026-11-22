# Windows a mod defines

**Ours**: an enhanced game's only (`docs/enhanced.md`, *The shape*), and the way BlackICE's utility comes to this
engine (`docs/blackice.md`, step 6). Nothing of it is in the original. Built 2026-11-22 as far as the menu and a page
that reads; a page that changes the game is not built.

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
        title = "OPENHOI_WIN_FLAGS",       -- the localisation key of its name, on the menu and on its window
        order = 20,                        -- its place on the menu: lower first, then by file name
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
| layout | `Separator`, `SameLine`, `Spacing`, `NewLine`, `SetNextItemWidth(width)` |
| pressed, answering true or false | `Button`, `SmallButton`, `Selectable(label, selected)`, `CollapsingHeader` |
| a value, answering `changed, value` | `Checkbox(label, value)`, `InputText(label, text [, most])`, `SliderInt(label, value, least, most)`, `SliderFloat`, `Combo(label, choice, list)` - the choice counted from one |
| tables | `BeginTable(id, columns)`, `TableSetupColumn`, `TableHeadersRow`, `TableNextRow`, `TableNextColumn`, `EndTable` |
| tabs | `BeginTabBar(id)`, `BeginTabItem(label)`, `EndTabItem`, `EndTabBar` |
| parts | `BeginChild(id [, width, height])`, `EndChild`, `TreeNode`, `TreePop`, `PushID`, `PopID` |
| the item before | `IsItemHovered`, `SetTooltip(text)` |

As in the library, a `Begin...` answers whether what it begins is open - a table off screen, a tab not chosen - and
only an open one is filled and ended, but a child, which is ended whatever it answered.

**The engine keeps the pairing for the page.** The library forgives neither an `End` too many nor one too few, and a
page's Lua may fail half way through a table. So what a page began and did not end is ended for it when its drawing
is over, the last begun first; and an `End...` with nothing of its kind open is passed over, the page told so in its
window as a failed drawing is.

**More widgets are added as pages need them** - "lets not limit ourselves" (the user's, 2026-11-22). The list is a
choice, not the library's whole.

## 5. The menu

The **Insert** key shows and hides the menu - the key BlackICE's overlay had - a window of the library's titled by
the key `OPENHOI_WINDOWS_MENU`, with a box to tick for each page, in the menu's order, by the page's title localised
(its name where it has none). **Every page ticked is a window of its own**, so several stand open side by side;
a page's close box unticks it. Hiding the menu hides the pages with it and keeps which were open.

## 6. What the engine gives a page

Under the global `OpenHOI3`, beside `Files`, `Enhanced` and `Fixes` (`mods.md`, section 6), once the pages are loaded:

- `OpenHOI3.ImGui` - section 4;
- `OpenHOI3.Localise(key)` - the key's text, or the key where no table has it; it asks no game, so `Draw` may call it;
- `OpenHOI3.CountryFlags(tag)` - the names of the flags that country holds, sorted by name without case; a flag set
  and cleared again is not among them; empty for a tag that is no country's, and where no game is asked;
- `OpenHOI3.GlobalFlags()` - the same of the game's own flags.

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
drawn in the client and in an export; and with `--screen=modwindows:flags` the menu and BlackICE's first page, Active
Flags - Germany's tag, a filter box and a table of its three flags after thirty days.

**Not tried**: the mouse and the keyboard on a window of the library - a press on the menu's box, typing into the
filter, whether a click on a window is kept from the map beneath - a scripted click having missed its box. Those are
the plugin's part and are to be tried by hand.

## Not built

- A page that changes the game: what a page may post, and where - `Draw` has no game. The conversion of resources,
  the first such page, comes with that setting's own command (`docs/enhanced.md`, *Rules*).
- A key of the mod's choosing in the place of Insert; a button for the menu on a screen of the game.
- A theme and a text size of the player's choosing, which the overlay has.
- Sorting a table by a column's header, and the rest of the library's widgets.
- The pages in the front end: they are a game's.
