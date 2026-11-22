# Windows a mod defines

**Ours**: an enhanced game's only (`docs/enhanced.md`, *The shape*), and the way BlackICE's utility comes to this
engine - as windows of the game and not an overlay of a library's (`docs/blackice.md`, step 6). Nothing of it is in
the original. Begun 2026-11-22, with the menu and one page that only reads; a page that posts commands is not built.

What it takes its shape from, all the user's:

- **A mod declares its own elements**, in `.gui` files, and the engine generates none (2026-11-21).
- **Every word is the localisation's**; the engine words nothing (2026-11-21).
- **Nothing registers the windows in one file**, which a submod would have to overwrite: a page is there by its files
  being there (2026-11-22).
- **The original's element kinds to begin with, and new kinds of this engine's where a page needs one** - "lets not
  limit ourselves" (2026-11-22). None is added yet.

## 1. A page is two files, and its texts

| | where | what |
| --- | --- | --- |
| the window | a `.gui` file of the mod's `interface/openhoi/` | a top-level `windowType`, and a `windowType` for the rows of each list box in it |
| the page | a `.lua` file of the mod's `script/openhoi/windows/` | says which window it fills, and fills it |
| the texts | a `.csv` of the mod's `localisation/openhoi/` | the keys the window's file and the page name |

All three folders are read in an enhanced game alone, after the folders they stand in, and the original opens none of
them: its listing of `interface` and of `localisation` takes one folder and drops directories, and it does not list
`script` at all (`reversing/findings/FINDINGS-guifolders.md`).

**The engine lists `script/openhoi/windows/*.lua` over every layer** (`CLuaWindows`), each name once, from its
frontmost layer, as every listing of a folder goes. So a submod adds a page by adding its files, and replaces one by
shipping a file of the same name.

## 2. The page's file

It is run once, as the pages are first asked for, as the body of a function - so what it declares `local` is its own -
and what it returns is the page:

    return {
        window = "openhoi_flags",          -- the window type it fills
        title = "OPENHOI_WIN_FLAGS",       -- the localisation key of its name on the menu
        order = 20,                        -- its place on the menu: lower first, then by file name
        Collect = function(player) ... end,
        OnClick = function(element, row, player) ... end,
    }

- The file's name without `.lua` is the page's name: letters, digits, `-` and `_`.
- A file that does not parse, fails as it runs, returns no table or names no `window` is no page, and is reported with
  the Lua error, as every Lua error is.
- No game is asked while a file is run: a page reads the game in its `Collect`.
- `player` is the tag of the country played, as text.

**The pages run in the Lua state the screens have for themselves** - the one a dialog asks an AI's opinion in
(`docs/multiplayer.md`, section 12), with the install's scripts loaded and the classes the AI's scripts are given -
never the state the AI's ministers run in. They only read: what a page may change, it changes by posting a command
(section 5).

## 3. What `Collect` answers

A table, each entry under the name of an element of the window:

| the value | does |
| --- | --- |
| a string, a number | the text of the text box of that name, or of the button |
| `true`, `false` | shows or hides the element - an icon, a text box, a button or a list box |
| a list of tables | the rows of the list box of that name, in order: each row a window of the type `<list box>_entry`, and each table of the same kind for that window's elements. An empty list empties the list box |

An element not named is left as its file made it; a name the window has not is passed over. So a label's text is its
file's, a key the localisation words, and a page names only what changes.

**A page shown is asked again every two seconds** of real time, and at once after a click. While hours run beside the
frame it is not asked (`tick.md`, section 2): the screen stands under the HUD.

## 4. The menu

The **Insert** key shows and hides the window `openhoi_windows_menu` - the key BlackICE's overlay had. The engine
fills its list box `pages_list` with one `pages_list_entry` a page, in the menu's order, setting the row's
`page_button` to the page's title, localised (the page's name where it has none); pressing it opens the page in the
place of the one shown. `close_button`, in the menu or in a page, closes the window it is in; hiding the menu shuts
the page with it. A window whose file says `moveable` is dragged as any other.

With no file declaring `openhoi_windows_menu` the key does nothing, which is reported as any missing window is.

## 5. A click

Every button of a page, and of its rows, is handed to the page's `OnClick(element, row, player)` when pressed -
`row` the row's place in its list from nought, -1 for a button of the window itself - and the page is asked again.
A page changes nothing itself. **What it may post is not built**: the conversion of resources, the first page that
changes the game, comes with that setting's own command (`docs/enhanced.md`, *Rules*).

## 6. What the engine gives a page

Under the global `OpenHOI3`, beside `Files`, `Enhanced` and `Fixes` (`mods.md`, section 6), once the pages are loaded:

- `OpenHOI3.Localise(key)` - the key's text, or the key where no table has it;
- `OpenHOI3.CountryFlags(tag)` - the names of the flags that country holds, sorted by name without case; a flag set
  and cleared again is not among them; empty for a tag that is no country's;
- `OpenHOI3.GlobalFlags()` - the same of the game's own flags.

More come page by page (`docs/enhanced.md`, *What mods build on*: variables, timed modifiers, modifier totals, a
province's figures, a leader's state, the selection).

## 7. Checked

`LuaWindowsTests`: the pages of a folder in the menu's order, a file that fails, a `Collect`'s texts, shown and rows,
the engine's reads, a click and a failing `Collect`. And on 2026-11-22 with `--screen=modwindows` and `--click`,
BlackICE's working copy played from a linked stand-in install: the menu from its `interface/openhoi/windows.gui` with
its one page, Active Flags, and that page opened - Germany's tag and no flag on 1 January 1936, three after thirty
days, each a row of its list.

## Not built

- A page that posts commands, and what `OnClick` may post.
- A filter box, sorting by a column's header and the count of rows shown, which the overlay's pages have.
- More than one page open at once.
- A key of the mod's choosing in the place of Insert; a button for the menu on a screen of the game.
- New element kinds.
- The pages in the front end: they are a game's.
