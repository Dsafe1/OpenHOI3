# The Statistics screen

*Spec for `ledger`, behind the top bar's Statistics button: `reversing/findings/FINDINGS-topscreens.md`, section B. Read out of the original, tried with `--click`, and it has not been reviewed in play. The file's elements are `interface/hoi3ledger.gui` and `tfh/interface/hoi3ledgericons.gui`.*

`Interface/Ledger` (`CLedgerPages`, `CLedgerPage`, `CLedgerColumn`, `CLedgerRow`, `CLedgerCell`, `CLedgerPie`, `CLedgerSlice`), `src/Screens/CLedgerView`, `CLedgerPieChart`.

## 1. Opening and closing

The top bar's button toggles it, and it and a country screen shut each other. **It pauses a game while it is up** and leaves it as it found it (*single player only* in the original; there is nothing else here). `close_button` and Escape shut it. It opens on the page it was last left on. The picture behind it is the country's own where the install has one.

## 2. Pages

26, from nought, written one higher in `page_number`. `next_button` (Right) and `previous_button` (Left) go round. `ledger_overview` is page 0, `ledger_economy` 6, `ledger_misc` 12, `ledger_military` 13. `ledger_overlay` is hidden on the four pages of pies. **The graph section is dead in the original**: `graph_background` and `no_graph_data` are hidden and nothing shows them; `checkbox_autosend` and `textbox_autosend` too. Each page is made from the world when it is shown.

| page | what |
| --- | --- |
| 0 | every country with land: faction and victory points held |
| 1 | every province with points: holder, and its points under the holder's faction or Neutral |
| 2, 3, 4 | the player, its allies and its puppets: brigades of each buildable type of the kind |
| 5 | every ship sunk, and what sank it |
| 6, 7 | the provinces held: resources, men and leadership; each building's level |
| 8 | a row for each of the country's goods pools |
| 9, 10 | a province for each strategic resource, the holder's flag under the resource: the first nine, then the rest |
| 11 | every convoy |
| 12 | each minister in each post open to them |
| 13, 14, 15 | the player's brigades, ships and wings, with the chain above each |
| 16, 17, 18 | the same with each figure of its type |
| 19, 20, 21 | the player's land, sea and air leaders in service; a click selects the leader's unit and shuts the screen |
| 22 | four pies of the player's units by land, air and sea: numbers, supplies, fuel, reinforcement |
| 23, 24, 25 | the same by type, for land, air and sea |

Page 8's `LEDGER_USED_INDUSTRY` ("Used by Industry") is **what a day of the country's IC would cost in resources** - its IC before the shortage cap and lend-lease times 1 metal, 2 energy and 0.5 rare materials, a need and not a use (`ic.md`, section 4) - and `LEDGER_USED_TROOPS` ("Usage") beside it is everything that left the stockpile, industry's metal, energy and rare materials among it. `confirmed` (`reversing/findings/FINDINGS-supplypass.md`, section 11; `FINDINGS-uinumbers2.md`, section 2). Built 2026-11-15 and looked at with `--click` through the top bar's button: Germany's first screen at 1936 has 201.0, 402.0 and 100.5 there, beside a usage of 121.2, 242.4 and 60.6.

*Ours*: the original's page 12 and the rest of the politicians' columns are as read. Page 0's points off the map (`Offmap VP`), and of page 8 what is paid and owed in debts and what passes between puppets and overlord, are kept in no pool here and read nought; the `Order` column of the unit pages and a brigade's model on the figures pages are left empty, how the original writes them not having been read.

## 3. Tables

Rows start in the order of their names, compared as bytes. A left click on a header sorts by that column, again the other way, and each page keeps its own; `equal` cells keep the order they stood in. A cell compares as a whole number, a figure or text, in that order. A figure is written to one place and a share as a whole percent.

A header is its icon where it has one, and `TableHeaderText` after the icon, as wide as the column; cells are `TableItemText` as wide as the column, cut with three dots. The table starts at (66, 139) in the window: `ledger_UL` from the middle of the screen. The header's row stands at nought and the rows at 35, 30 apart, sixteen of them. `standardlistbox_slider` stands at (1033, 167); a bar that runs up and down has its greatest value at the top. The wheel over the window moves the rows and goes no further.

**Each page's columns are its own, figures in the original's code** (`CLedgerColumns`), some overlapping and some leaving gaps, kept as they are. Only page 0 asks the files, for `ledger_nat_stat<i>` and the two default widths. *Measured* no more: `ledger_columns.dds`'s bands agree with the rows.

**The strategic resources' pages**: a column is a resource, in its file's order, with its icon and name, each 996 / n wide and stopping 12 short. **Row r is the r-th province of each column's resource**: a flag in text and the province's name; the rows are in province order and the page sets no sort. Its tooltip gives the holder, its faction in brackets - green for the player's own, red for another, white when the player has none - and the province.

The tooltips of the column icons and of the cells (`BUILD_*_IRO`, `BUILD_*_DRO`) are not built; the other cells say nothing.

## 4. Pies

Four at `pie_one_offset` to `pie_four_offset`, each with a `pie_heading` and a line of `pie_text` for each slice in its colour. A slice shows only with something in it; a pie whose total is under a tenth has none and shows `no_pie_data_window`. Land, air and sea are red, green and blue.

*Ours*: the slices' angles, the leader lines, the size of a pie and the base the four offsets are measured from were not read (the pie is 70 square, the base 500, 330); a type's colour on pages 23 to 25 is from a list of our own, and a label is its name and its figure.

## 5. What is saved

Only `nation_size_statistics` is ever filled in the original, yearly, and nothing reads it; `income_statistics` and `inflation_statistics` stay empty (`reversing/findings/FINDINGS-topscreens.md`, B.6 and B.7). **The graphs it would feed are dead in the original**: the file's graph section is hidden and never drawn, so nothing here draws one either.

**Kept for the save since 2026-11-14** (`CNationSize`, `CStatisticsSeries`): as each year begins, for the null country `---` and then every country in `common/countries.txt`'s order, the provinces it owns are added to its line with the year - a line made at the end for one that has none - so a 1936 start's lines begin at 1937. A save writes the three blocks after the wars, every time: the two the original never fills empty, and each line of the nation size as a `ledger_data` block of its figures (`ledger_data_y`), its years (`ledger_data_x`) and its `name`, read back whole; one of the empty two that holds anything is reported. `Ireland1940_05_23_15` comes back with all three blocks the same. Where the yearly pass stands beside the day's and the month's was not established. Hardcoded [exe].
