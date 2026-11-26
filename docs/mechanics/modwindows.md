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
| the page | a `.lua` file of the mod's `script/openhoi/windows/`, or of a folder below it |
| its texts | a `.csv` of the mod's `localisation/openhoi/` |

Both folders are read in an enhanced game alone, and the original opens neither: its listing of `localisation`
takes one folder and drops directories, and it does not list `script` at all
(`reversing/findings/FINDINGS-guifolders.md`).

**The engine lists `script/openhoi/windows/*.lua` over every layer** (`CLuaWindows`), each name once, from its
frontmost layer, as every listing of a folder goes. So a submod adds a page by adding its file, and replaces one by
shipping a file of the same name.

**And the folders below it, however deep** - the user's, 2026-11-22, for a mod to keep its pages in order by
("either add a prefix to the LUA files to group them by page, or enable the use of subfolders"): each folder listed
the same way, a folder's own files before its subfolders' (`CDirectorySettings.ListFilesRecursive`). **A folder
says nothing to the engine**: the tab a page stands under is its `group` and nothing else, so a mod may sort its
files a folder a tab, as BlackICE does, or any other way. A file is one page by its path below the pages' folder:
a submod replaces `country_info/conversion.lua` by a file at that path, and two folders may each hold a `flags.lua`.

## 2. The page's file

It is run once, as the pages are first asked for, as the body of a function - so what it declares `local` is its own
and is kept from frame to frame: a filter's text, a ticked box - and what it returns is the page:

    return {
        title = "OPENHOI_WIN_FLAGS",             -- the localisation key of its name, in its tab's list
        order = 20,                              -- its place in that list: lower first, then by the page's name
        group = "OPENHOI_WIN_GROUP_INSPECTOR",   -- the localisation key of the tab it stands under
        group_order = 40,                        -- where that tab stands among the tabs (section 5)
        Collect = function(player) ... end,
        Draw = function(data, player) ... end,
    }

- The file's path below the pages' folder, without `.lua`, is the page's name - `flags`, `country_info/conversion`:
  letters, digits, `-` and `_` in the file's name and in each folder's. A file named otherwise is reported and is no
  page.
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
| tables | `BeginTable(id, columns)`, `TableSetupColumn(label [, width])` - a width is the one the column begins at - `TableHeadersRow`, `TableNextRow`, `TableNextColumn`, `EndTable` |
| panes | `BeginPanes(id, columns)` - columns side by side with a divider between them that the player drags, with no border, stripes or heading; set up, filled and ended as a table is, by `EndTable` |
| a picture | `ImageSize(path)`, answering `width, height` in the picture's own pixels and nought where there is none; `Image(path, width, height)` - the path below the install, a mod's own file by its folder (`tfh/mod/<mod>/gfx/...`) or a file of the game's found through the layers; decoded once and kept |
| keyboard and clipboard | `IsWindowFocused()` - the part being drawn, or one within it, was the last thing clicked; `IsKeyPressed(name)` by the library's name, `DownArrow`, `Home`; `SetScrollHereY([share])` - the item last drawn scrolled to that share of the height, its middle with none given; `SetClipboardText(text)` |
| tabs | `BeginTabBar(id)`, `BeginTabItem(label)`, `EndTabItem`, `EndTabBar` |
| a box of text | `InputTextMultiline(label, text [, width, height, read_only])`, answering `changed, text` - several lines, which may be selected and copied; read-only, a page's way of showing a long text |
| room | `GetContentRegionAvail()`, answering `width, height` - what is left to the right and below, for sharing out |
| parts | `BeginChild(id [, width, height])`, `EndChild`, `TreeNode`, `TreePop`, `PushID`, `PopID` |
| greyed | `BeginDisabled(disabled)`, `EndDisabled` - what stands between is greyed and takes no press where `disabled` is true; ended whatever was given |
| the item before | `IsItemHovered`, `IsItemActive` - held, or typed into - `IsItemDeactivatedAfterEdit` - let go of this frame, changed: a slider released - `SetTooltip(text)` |
| a number and a bar | `InputFloat(label, value [, decimals])`, answering `changed, value` - a box with no buttons beside it; `ProgressBar(share [, width, text])` - filled to a share of its width, the text over it |
| a tooltip of the page's own | `BeginTooltip()` ... `EndTooltip()` - what is drawn between them is the tooltip at the mouse, for more than `SetTooltip`'s one text: a coloured line, several. Left open, it is ended with the page's drawing |
| several lines | `PlotMany(id, series [, height, axes])`, answering the step under the mouse or nil - with `axes`, `{ x = , y = , from = , to = }`, the least and the most value stand left of the frame, `y` over it, and under it `from` and `to` at its ends with `x` between them; while the mouse is over the frame an upright line stands at the nearest step, which is what the call answers, for the page to say that step's values in a tooltip (2026-11-24: "make the cursor show a tooltip with the values at that point"). every series `{ first = , colour = { r, g, b, a }, values = { } }` in one frame as wide as the room there is: across from the earliest `first` to the latest last step, so that a series beginning later stands where it belongs; up from the least value among them all to the most; each line in its own colour, a pixel and a half thick. Built 2026-11-24 for the Statistics page's "One plot" |
| a line | `PlotLines(label, values [, width, height, text])` - a line through a list of numbers, one after another at even steps across the width, between the lowest and the highest of them; the library's own, which says the value under the mouse. The list crosses into the engine as one text, a series being longer than a call takes arguments |

**A text is a text**: a percent sign in what a page gives `TextDisabled`, `TextWrapped`, `TextColored` or
`SetTooltip` is drawn as one. The library takes those four as a format in the C manner, and a page's "100%" lost its
sign and what stood behind it until the canvas doubled the sign for it (`CImGuiCanvas.Plain`).

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

The **Insert** key shows and hides it - the key the overlay had - until the player chooses another (section 6.4). It is titled by the key `OPENHOI_WINDOWS_MENU`, and
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
- **A tab's pages**: by `order`, then by the page's name, its folders with it.
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
- `OpenHOI3.CountryVariables(tag)` - that country's variables, each `{ name, value }`, sorted by name without case;
  one at nought is among them, and leaving it out is the page's;
- `OpenHOI3.CountryModifiers(tag)` - the modifiers it was given for a time (`add_country_modifier`), in the order
  given, each `{ name, expires, never }`: `expires` the hour it ends at as `year.month.day hour:00` unpadded, `never`
  true for one that does not end, which the original keeps as a date in year one;
- `OpenHOI3.LeaderDetails(id)` - where the running game has the leader of that number: nil for none, else
  `{ active, unit_name, province_id }` - whether he is in service, and the name of the unit he commands with the
  province it stands in, both nil where he commands none. The shape is the one BiceLib's read has on the original,
  so that the mod's data function asks whichever is there;
- `OpenHOI3.ProvinceDetails(id)` - a province's own figures, nil for a number the map has not: `id`, `supply_pool`
  and `fuel_pool` (what lies there), `oil`, `metal`, `energy`, `rares` (what it makes now), `manpower`,
  `leadership`, and `modifiers` with `local_ic`, `local_oil`, `local_energy`, `local_metal`, `local_rares`,
  `local_leadership` - its own modifier of the kinds `local_ic`, `local_crude_oil`, `local_energy`, `local_metal`,
  `local_rare_materials`, `local_leadership`. **All in thousandths**, which is the shape and the scale BiceLib's
  read has on the original and the mod's data divides. No test of what the country knows of the province, as on
  the original;
- `OpenHOI3.OrderOfBattle(tag)` - every unit of a country, each before what stands under it, as a list of
  `{ id, parent, branch, kind, name, province, leader, supply, fuel, supply_used, fuel_used, dug_in,
  combined_arms, cooldown, orders, regiments }`: `parent` the number of the unit over it and 0 for none; `branch`
  `land`, `air` or `naval`; `kind` its level in words; `leader` `{ name, skill, max_skill }` or nil; `supply` and
  `fuel` the shares it gets; `supply_used` and `fuel_used` what its own regiments use a day, as the game works it
  out; `combined_arms` a division's bonus as a share; `cooldown` the hours it may not attack; `orders` the words
  `priority`, `reinforcing`, `upgrading` that apply; `regiments` each `{ name, type, strength, organisation }`. A
  name's colour escapes are taken out. **A whole order of battle is thousands of small reads**: a page asks for it
  on a button and when the country is another, not every two seconds. No test of what the player knows of it
  either;
- `OpenHOI3.CombatTally(tag, from_day, to_day)` and `OpenHOI3.LastCombats(tag, count)` - a country's finished
  battles counted over days, by kind, and its last ones, from the record an enhanced game keeps
  (`combatreports.md`, section 4);
- `OpenHOI3.Today()`, `OpenHOI3.DayNumber(year, month, day)` and `OpenHOI3.DateOf(day_number)` - the game's date,
  and a date as its day's number and back (`combatreports.md`, section 4); the last two ask no game;
- `OpenHOI3.Refresh()` - section 6.2;
- `OpenHOI3.Conversion(tag)` - a country's two conversions (`ic.md`, section 4.1), or nil for no country or no game:
  `played`, `oil_rate`, `energy_cap`, `energy_floor`, `crude_cap`, what it holds as `energy`, `crude_oil` and
  `fuel`, and `energy_conversion` and `crude_oil_conversion`, each `{ own, per_day, keep, target }` - figures in a
  stockpile's units;
- `OpenHOI3.OffmapIc(tag)` - a country's off-map IC in whole IC, as the day's IC last made it (`ic.md`, section 3);
  nought in a game without the fix `offmap_ic`, nil for no country or no game;
- `OpenHOI3.Trickleback(tag)` - the share of a country's casualties that returns to its manpower, as a fraction:
  its modifier's and its technologies' `casualty_trickleback` together, held at nought, which is what a battle
  applies; nil for no country or no game;
- `OpenHOI3.SetConversion(tag, which, own, per_day, keep, target)` - **an order**, section 6.1: `which` is `energy`
  or `crude_oil`. Answers whether it was understood;
- `OpenHOI3.Profile` - the engine's own timing, section 6.5.

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

**And the original's own commands, posted the original's own way.** A mod's Lua that already changes the game by
`CCurrentGameState.Post(CSetVariableCommand(tag, name, value))` - every setter of BlackICE's data functions does -
may go on doing so from a page, **in its `Collect`**, where the game's classes answer: while a page gathers, what
the Lua posts is kept with the page's orders (`CAiScripts.CallInGame` with a place for it) and the screen posts it
as it posts the others. It does not go into the game's outbox from there - a page's Lua is the player's hand and
not a script of the game's - so it reaches the host between several machines and a recording like any order. So a
press that sets something is noted as the page draws and done as it next gathers, which it asks for at once
(section 6.2); BlackICE's `pagekit.lua` has that as `Kit.Ask`. **A page may so post for any country**, as on the
original, where nothing checks whose variable a machine sets: no rule for it is decided.

### 6.2 A page that shows what was just chosen

A page that lists things and shows the chosen one reads the chosen one's figures in `Collect`, the only place the
game may be asked - and `Collect` comes every two seconds. So **a page may ask, as it draws, to gather again at
once**: `OpenHOI3.Refresh()` (`IModPages.TakeRefresh`). The screen then gathers the page after that frame's drawing,
or as soon as the game may be read where hours run. The page notes the choice in a `local` of its own, asks, and
its `Collect` answers for what is noted; until the answer is for the choice now made, it draws a "loading" line.
What a press asks of the mod's own state that needs the game - BlackICE's Units page changing an assumed level -
goes the same way: noted while drawing, done in `Collect`.

### 6.3 What BlackICE's pages are made from

**The mod's own data functions run here unchanged.** BlackICE's overlay on the original keeps its data apart from
its drawing: `BiceLibGui.<Page>.<Function>` in the mod's Lua parses the mod's files and asks the game's Lua
classes, and the DLL only draws. `autoexec.lua` loads them in every Lua state, the screens' own among them, and on
2026-11-22 they answered here as they are - 343 traits, 1,175 technologies, 1,650 unit types, Germany's 2,016
leaders and 1,919 unit models. So a page of BlackICE's for this engine is a `Collect` that calls them and a `Draw`
in `OpenHOI3.ImGui`; `script/openhoi/pagekit.lua` holds what the pages share. Where a data function asks BiceLib
- where a leader stands - the mod's function asks the engine's read of the same shape here
(`OpenHOI3.LeaderDetails`), which is the one line of the data layer that knows which engine it is on.

### 6.4 The window's own settings: `OpenHOI3.Window`

The window the pages stand in is the engine's, and so are its settings; a page offers them to the player -
BlackICE's Utility Options - through `OpenHOI3.Window`, which answers while a page draws and asks no game:

- `Key()` - the name of the key that shows and hides the window, as the engine names keys: `Insert`, `F10`,
  `Page Up`. `CaptureKey()` makes the next key pressed that key, which nobody else then hears; Escape, or
  `CancelCapture()`, gives the choosing up; `Capturing()` says whether it is under way. **The key is asked for where
  the game's own keys are** (`CSession._UnhandledInput`), before them, so a key of the game's given to the window is
  the window's, and a letter typed into a box of the game's or of a page's still types.
- `Theme()`, `Themes()`, `SetTheme(name)` - its look: the three the drawing library has of its own, `dark`,
  `light` and `classic`, and **`army_green`**, olive panels and amber headings - the user's, 2026-11-22, on seeing
  the three: "Please also Army green theme", after the look of that name BlackICE's overlay has on the original.
  Its colours are this engine's own (`CModWindows.ArmyGreen`), made from the description "olive panels, amber
  headings" and the overlay's amber, 226,162,38, which BlackICE's own page on its features gives; BiceLib's colours
  were not read. The overlay's Grey is not here.
- `Scale()`, `SetScale(factor)` - the size of its text as a factor, held between 0.6 and 2.
- `ResetLayout()` - the window back where and as large as it first stood.

**The key and the look are the player's and are kept**, in `openhoi_settings.txt` with this engine's other
settings (`gameoptions.md`, section 5; keys `window_key`, the name in quotes, and `window_theme`), written as they
change; the game options' rows show neither and leave them as they are. **The text's size is not kept**, as the
overlay's was not. The layout the library keeps itself (section 7).

More reads come page by page (`docs/enhanced.md`, *What mods build on*: modifier totals, a province's figures, a
leader's state, the selection).

### 6.5 The engine's own timing: `OpenHOI3.Profile`

For a page that says where the engine's time goes - asked for on 2026-11-26, "a profiling page which details the
time taken for different parts of the engine". BlackICE's is Engine Profiler, under its Debug tab. Nothing like it
is in the original or in BiceLib.

- **`OpenHOI3.Profile.Read()`**, asked in `Collect`: **asking starts the timing** - the game keeps a profile
  (`CTickProfile`, the one `OpenHOI3.SaveCheck profile` has always set) from the first asking on, also while the
  page is closed - and answers, nil for no game:

  | | |
  | --- | --- |
  | `seconds` | how long the timing has run, in real seconds |
  | `hours`, `hour_ms`, `hour_most` | the hours that turned no day: how many, their milliseconds together, the longest |
  | `days`, `day_ms`, `day_most` | the same of the hours that turned a day |
  | `last` | the last 240 hours' milliseconds, the oldest first |
  | `steps` | every step, the longest first, each `{ name, ms, calls, part, parent }` |
  | `engine` | what the program says of itself, by key |

- **`OpenHOI3.Profile.Reset()`** forgets what was counted and begins again; **`OpenHOI3.Profile.Stop()`** ends the
  timing. Both ask the game, so a page calls them in its `Collect`, a press noted while it draws.

**An hour** is one hour as the simulation runs it, the orders it drains with it (`CSimulation.RunOneHour`).

**A step** is one of the tick's, by the engine's own name for it - `Trade.CTradeAi.RunHourly`,
`CSupplyPass.RunDaily` - which a page shows as it is: it is the name a developer looks the code up by, and no word
for a player. **A part** (`part` true) is a piece of a step timed inside it, and `parent` names that step; it is
counted in its step as well. **Parts that run side by side on several threads - the unit AI's - add up to more
than their step**, which is the time that passed. **`the orders drained`** is a step of the simulation's own:
every order an hour runs but its tick - the players' and the AI's - each kind of order a part of it by its class's
name. What the steps do not come to is not timed: the events the clock raises for the screens, mostly.

**The program's figures**, each under a key the page words: from Godot's own monitors `fps`, `frame_ms` (a
frame's process time), `draw_calls`, `primitives`, `nodes`, `memory_mb`, `video_memory_mb` - given by the client
(`CLuaWindows.EngineFigures`), so absent where there is no screen - and from the runtime `managed_mb`,
`collections`, `collections_full` and `threads`, the processor's.

**A profile is no part of a game**: it is not saved, not in the hash, and a game plays the same with one. It costs
two readings of a clock a step and an order, so it is left running once asked for.

Checked with `OpenHOI3.SaveCheck pages`, which draws a mod's page with no screen: on BlackICE's first three days
the page's steps come to all but a hair of the hours' time, 44% of it the orders drained and 36% the AI's
decisions among them. Not looked at on screen.

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

`LuaPagesTests`, with a canvas that writes down what it is asked: the pages of a folder in the menu's order, a page
in a subfolder named by its path (`--screen=modwindows:` takes that name or the file's alone), a file
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

**The Inspector's and Game Info's pages**, 2026-11-22: `LuaPagesTests` has a page read a country's variables and
given modifiers, ask to gather again, and draw a box of text. With `--screen=modwindows:<page>`, BlackICE's Traits,
Modifiers, Techs, Generals, Units and Unit Models were drawn with the mod's own data, each also with a row chosen
by a line put into the page for the run and taken out again - a trait's effects and triggers, a technology at its
level, a leader's definition, a unit's figures and model string, a model's required levels. Active Variables and
Active Event Modifiers were drawn on a new game's first hour, where Germany has neither; their rows are the test's
alone. **No list was clicked in a run**: choosing a row, the level buttons and the filters are to be tried by hand.

**The mouse held on the window reached the game's windows beneath it** - the user's, 2026-11-22: dragging the
utility window dragged the event or message window under it too. A moveable window of the game's is dragged from
its `_Input`, and so are a list's wheel and others; the library's own layer takes its input after all of them, being
an autoload, so its "handled" came too late. Mended the same day: `CModWindows` keeps itself the last child of the
session - the first asked for input - and while the library wants the mouse it hands a mouse event to the library
itself and lets nobody else have it. **Not tried by hand since.** And with it, the same day, what the first pages
lacked beside the overlay's: the divider dragged between two panes, the arrow keys, Home and End in a list - the map
not scrolling while the window was the last thing clicked (`CModWindows.HasKeyboard`) - a picture, the clipboard and
where a leader is. Drawn in a run: the panes, the Unit Models page's sprite - 192 by 104, "Country specific (exact
level)" - and von Rundstedt in Berlin with Heeresgruppe Süd. The keys, the divider's drag and the Copy button are
the test's and to be tried by hand.

**The arrow keys chose no row** - the user's, the same day: the map stood still and the list did nothing. The
program's root stops every arrow key before Godot's own interface answers it (`Main._Input`; `tick.md` has why), and
that is before the library's layer too, so no page ever heard one - nor could a box of a page move its caret by
one. `CModWindows._Input` now hands an arrow key to the library itself while the window has the keyboard, as it
hands the mouse. **Not tried by hand since, and no scripted run can try it**: the library takes the pointer's place
from the system and not from a scripted event, so a scripted click never lands on the window - which is also why a
scripted click over it reaches the map, where a real one does not. The user tried the keys, the drag over an event
window and the divider by hand the same day: "looks good".

**Three of the Inspector's larger pages**, 2026-11-22: `LuaPagesTests` has a page read a province's figures and a
country's order of battle. With `--screen=modwindows:<page>`, BlackICE's working copy on a new game: OOB Report -
Germany's 83 units, 63 land, 13 air and 7 naval, in 375 regiments, by kind from theatre to fleet, its three top
formations and 94 types of regiment - OOB Browser, with a division chosen by a line put in for the run - the tree
open to it, Schroth, skill 3 of 7, at Königsberg, 55% combined arms, seven regiments - and Province Buildings, with
Industry chosen the same way: 263 provinces, Berlin's 10 first, and a province's base production and local
modifiers. **Nothing was clicked**: the tree's arrows and keys, the lists and the Copy buttons are to be tried by
hand. **A regiment's strength is the game's own figure** - a brigade's 72.0, a staff's 3.2 - and was not set beside
what the original's overlay shows.

**Country Info's pages**, 2026-11-22: `LuaPagesTests` has a page post the original's `CSetVariableCommand` as it
gathers - the command among the page's orders, the game's outbox empty, the variable untouched. With
`--screen=modwindows:<page>` all eleven were drawn on a new game's first hour with the mod's own data: Country
Modifiers' four sections - Germany's base IC 258, IC modifier 19.50% - IC Days, Puppets, Strategic Res., Strategic
Trades, Global Market, National Focus, Minister Buildings, and the three forms on their defaults, "Never
configured". Two figures of Country Modifiers the mod asks of BiceLib and has none of here: Offmap IC reads 0 and
Trickleback "unavailable". **Nothing was set in a run**: the slider, the focus buttons, the checkboxes and the
forms' Apply and Enable are to be tried by hand.

**The Stats tab's pages**, 2026-11-22: `LuaPagesTests` has a page draw a line through three values and through
nine thousand. With `--screen=modwindows:<page>` Setup was drawn on a new game - no run number yet, the list of
every country - and Statistics, which said "Nothing collected yet."; and once with two series put into the page
for the run and taken out again, each a line with its lowest and highest and its first and last day. A page reads
the run's files itself, by `io.open` and `OpenHOI3.Files` under the working folder as the mod's Lua does.
**The mod's collection itself does not work on this engine** - the user's, the same day, having tried it; it
makes its folders with the shell's `mkdir` through `os.execute`, from the ministers' ticks, and why it fails was
not looked into: "lets ignore that for now". So no real series has been read here, and none will be this way: the
collection is to become a record of the engine's own (`docs/enhanced.md`, *Interface*).

## Not built

- Orders beyond the conversion's (section 6.1), and a command a mod defines itself.
- A button for the window on a screen of the game.
- Looks a mod declares itself, the overlay's Grey, and a text size that is kept.
- Sorting a table by a column's header, and the rest of the library's widgets.
- A scrollbar across a picture wider than its pane, which the overlay's sprite has: a child takes no flags here.
- The pages in the front end: they are a game's.
