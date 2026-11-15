# Bookmarks: a game begun at a later date

A bookmark is a dated entry point: the lobby's Historical Start lists them, and choosing one moves the world to its date
before a country is chosen. The Quick Start panel offers four starts of its own, dated in the executable. Built
2026-11-15 from two readings of that day - `reversing/findings/FINDINGS-bookmarks.md` (the file, the lobby, which
history a later date brings) and `reversing/findings/FINDINGS-laterstart.md` (what the new-game pass does once the
history stands) - and checked against three saves the maintainer made at the first hour of the 1939.9.1, 1941.6.22 and
1941.12.11 bookmarks (section 5).

**[exe]** marks what the executable fixes, **[mod: file]** what a file may change.

## 1. The file

`common/bookmarks.txt` [mod: common/bookmarks.txt], read through the layers, so **a mod's file replaces the game's
whole** - the two are not merged [exe]. Their Finest Hour has none of its own, so the base game's is read: eight
bookmarks, 1936.1.1 to 1944.6.20. BlackICE ships one, of 1936 (`CBookmarkReader`, `CBookmark`).

Each top-level block is a bookmark, read in file order:

| key | holds |
| --- | --- |
| `name`, `desc` | localisation keys, the row's title and the chat's lines |
| `icon` | the row button's sprite |
| `date` | `year.month.day.hour`, the hour nought where it is left out |
| `country` | a tag, repeatable: a shortcut shield in the lobby, in order. A tag that is no country is dropped and reported - `Bad tag <X> in bookmark <name>` - and the bookmark kept |
| `is_cgm` | `yes` puts the bookmark on the custom games' list, which is not offered (`docs/compatibility.md`, entry 17); it is left out |
| `cgm_folder` | a custom game's folder; not read |

**A key that is none of the seven ends the file** [exe]: that bookmark and every one after it are lost, with one report,
`Failed to read bookmark in <path>`. A missing file reports `Failed to open file <path>`.

**Nothing of a bookmark but its date reaches the game.** Its countries are shortcuts - any country may be chosen on the
map - and its name stays in the lobby, for the year label and the row's highlight.

## 2. The lobby, and the world at a date

**Opening the lobby chooses the file's first bookmark** (`CLobby.Open`), as the original's `SelectDefaultBookmark` does.
**A click on a row chooses that bookmark**: the world is made at its date, the save rows unticked, the row at frame 2 and
every other at frame 1, a shield for each of its countries, its name in yellow and its description in the chat, and the
year label written again. The country chosen stays chosen; Play says NOT_EXIST where it owns nothing at the new date.

**The date is held between the defines' `start_date` and `end_date`** [mod: common/defines.lua] (`CFrontEnd.ChooseBookmark`).
The world is then **the history folded to that date** - every undated entry, then every dated one up to the date in date
order, a date's entries in file order (`CHistory.Fold`) - for the provinces, the countries, the leaders, the diplomacy and
the wars, and **the game's clock set to it** (`CGameStateLoader.BuildWorld`, its `date`). **The defines' `start_date` is
not replaced**: it stays `CGameState.StartDate`, which the technology head start reads (`research.md`, section 9) and the
date widgets hold to.

So the lobby's map shows the chosen date's world at once - owners, controllers, countries gone and countries new: a
country exists where it owns a province, so Austria is gone in 1938 and Slovakia there in 1939 by their provinces'
dated `owner` lines, with no annexation in the history. Which order of battle a country gets is its history's last
`oob` entry at or before the date, read only as the game is made (section 3).

**The Historical Start tab, with a save loaded, lets the save go for the defines' start date and chooses no bookmark**:
Play then says CANT_START_REASON_SELECT_BOOKMARK until a row is clicked (`CLobbyText.WhyNotPlay`), as the original's reset
leaves it. Without a save loaded the tab changes nothing.

**Every world the front end makes is made on countries read afresh** (`CFrontEnd.BuildWorldAt`, and a save loaded the same
way): the history is written onto the country objects, so a world at one date cannot be made on another's. Making one
takes about a second.

**Ours**: the original moves its one world - forwards by replaying the history from the clock to the date, **backwards
by rewinding to `start_date` and replaying forwards** - and a rewind is only as exact as each history entry's undo, so in
the original choosing a later bookmark and then an earlier one may leave a world that differs from choosing the earlier
one at once (`FINDINGS-bookmarks.md`, section 4, item 5). Here every choice makes the world afresh at its date, which
gives what choosing it first would give. `docs/compatibility.md`, entry 27.

## 3. The new game at a later date

What a new game does once the history stands is the same at any date (`tick.md`, section 6; `politics.md`, section 13;
`research.md`, section 9); a later date exercises parts of it a 1936 start does not, and three readings of 2026-11-15
put those right (`reversing/findings/FINDINGS-laterstart.md`, sections 2a, 5 and 10 to 12).

**The history, at the date.** Each history change of controller or owner is a capture, in the original's three passes -
undated to year 2, to the defines' `start_date`, to the bookmark's date - so the occupation policies are the captures'
with the country history's policy lines on top, and an owner change sets nationalism (`occupation.md`, sections 2 and
5). A country's list of the provinces it controls is in the order those passes reached them, 1936's land first and the
bookmark's pass after, and the areas, theatres and `defend_prov` follow it (`supply.md`, section 2; `theatres.md`). A
`government_in_exile` line puts its country into exile or brings it back, every line in turn - ten base-game countries
have one - which keeps an exile out of surrender (`war.md`, section 14). A goods line sets its good; the last by the date
stands, in the pool of the seat the government sits in - an exile's its host's (`stockpile.md`, section 2).

**One country at a time.** The new-game entry sets every country up in turn, in the countries' order (`politics.md`,
section 13; `diplomacy.md`, section 5.3): each is brought up to date with the history's wars - mobilised, the lobby's
30 days run out - then its IC is worked out (a country whose turn is still to come has none yet, so a law's
`enemy_ic_ratio` is false while any enemy comes later), its effective neutrality against the lobby's most-threatening
country and the threat spread so far, its laws and technology, and its history's threat spread last, which a later
country does not resist unless the history's wars mobilised it. Its manpower weighs the land it holds of a country set up
later by that country's history law (`manpower.md`, section 4). The install's Lua looks at its laws once.

**After the decisions.** The stockpile's days of IC - 90, five fewer a month at war counted from the war's first history
day, never under 40 - are added once the history's decisions have run, so a decision's money stops the money fill; the
influence comes from the same base IC.

**The units.** Each country's order of battle is its history's last `oob` line by the date, read only for a country that
owns a province, or the rebels: Austria, Czechoslovakia and the Spanish Republic keep their files and get no units and no
numbers, so the next unit number is each start save's - 7201, 7875 and 8098. A unit written `army {` is read whole
(`units.md`, section 3). Entering runs every share once with nothing to spend, which brings every brigade a doctrine level
nearer its country before the two days (`upkeep.md`, section 5). Each country's theatres are made right after its own
units in the hour, so Wake Island, taken in the Day of Infamy's first hour, stays in the USA's theatre.

**The end of the entry** takes every country owning no land and in no exile off its wars and out of its faction, and starts
the played country's strategic warfare records of its enemies (`strategicwarfare.md`); rebel-held provinces begin their
rebels' clock at the start.

### 3.1 Set beside the start saves

`OpenHOI3.SaveCheck` on 2026-11-15, the three later saves and the 1936 start save (whose figures are unchanged):

| | 1939.9.1 | 1941.6.22 | 1941.12.11 |
| --- | --- | --- | --- |
| technology levels | 21,238 of 21,238 | 20,662 of 20,664 | 20,660 of 20,664 |
| occupation policies | 9 of 9 | 39 of 39 | 48 of 48 |
| nationalism | 560 of 560 | 1,337 of 1,337 | 1,383 of 1,383 |
| manpower, officers, influence | 73, 74, 74 of 74 | 72 of 72 each | 72 of 72 each |
| the build queue | 54 of 55 | 61 of 61 | 45 of 45 |
| brigades' strength | 4,644 of 4,823 | 5,347 of 5,357 | 5,505 of 5,513 |
| brigades' organisation | 4,778 of 4,823 | 4,848 of 5,357 | 4,737 of 5,513 |
| laws | 704 of 707 | 707 of 707 | 707 of 707 |
| relation, threat | every pair | every pair | every pair |
| neutrality, effective | 101 of 101 | 101 of 101 | 101 of 101 |
| alignment | 54 of 101 | 35 of 101 | 47 of 101 |
| spies abroad | 10,052 of 10,100 | 10,051 of 10,100 | 10,054 of 10,100 |
| theatres (number, headquarters, fronts) | 106, 106, 104 of 106 | 96 of 96 each | 97 of 97 each |
| AI strategy lists | 74 to 75 of 75 | 70 to 73 of 73 | 69 to 73 of 73 |

**What still differs, and where it was left** (the readme's *Loose ends*):

- **Alignment**, misses of 0.001 to 1 a country; threat and neutrality agree, so another drift term (relations with the
  members, proximity, member IC). `diplomacy`'s `explain=` works at the save's date for it.
- **An exile's own pool**: the saves' hold two days of an exile's income, ours none - the running exile day is not built.
- **The stockpiles' smaller drifts** of the two days, of the kind the 1936 start has; and supplies.
- **Brigades' organisation** held to a running ceiling by a law changed (Australia, Canada, Italy), supplies received,
  and lent units at their lender's officer ratio.
- **`protect` towards Belgium** (France 25 against 50; Britain drops it): the save implies a usable casus belli on an exile
  the reading says has none.
- **New Zealand's six posts at 1941**, and **China's three laws at 1939**, which its surrender in the two days reverts in
  the original and the posts made in the days are dropped here.
- **Japan's and China's fronts in another order at 1939**; three of the 13 `last_war` pairs of 1941.12.11; a
  `military_access` line opening `GER_DayOfInfamy.txt`, reported as unknown.

## 4. Quick Start

The main menu's `toggle_quick_normal` shows the Quick Start panel, `easymode_panel` [mod: interface/temp_frontend.gui],
in its place, and the panel's own `toggle_quick_normal` (Custom Start) brings the main menu back. **The front end opens on
the panel where the player's `settings.txt` says `simple=yes`**, and the toggle keeps `simple` in step; closing the
settings window clears it and comes back to the main menu (`CMainMenu`, `CFrontEnd`; `reversing/findings/FINDINGS-frontend.md`,
A.1). The panel's Exit ends the program; its Tutorial is greyed, as the main menu's is.

Each of the four buttons **starts a game with no lobby** (`CQuickStart`, `CFrontEnd.QuickStart` and `Prepare`):

| button | date | country |
| --- | --- | --- |
| `quickstart_ger` | 1939.9.1, hour 1 | GER |
| `quickstart_usa` | 1936.1.1, hour 1 | USA |
| `quickstart_jap` | 1941.12.7, hour 1 | JAP |
| `quickstart_sov` | 1941.6.22, hour 1 | SOV |

all [exe]. **No bookmark is read and the defines' range does not hold the date**, so a mod with other bookmarks, or
BlackICE with its one, still gets these four starts from its own history. Japan's 7 December 1941 is no bookmark's date.
**Each begins at hour 1** of its day, where a bookmark of the same day begins at hour 0. **No victory conditions are
chosen** (`CGameStateLoader.StartNewGame`'s `chooseVictoryConditions`): only the lobby's choice of a bookmark chooses them.
**The player hands Diplomacy, Production, Technology, Politics and Intelligence to the AI**, the tech minister on sliders
and research; the slider automation and the Armies level are left at a fresh record's - so a Quick Start leaves the player
the army alone. The difficulty and game mode are a fresh world's.

**What looks wrong and is the original's** (`docs/enhanced.md`): the panel's own texts promise each start its own share of
the work - FE_QS_USA_INFO "full control over the production, research, politics and intelligence", FE_QS_SOV_INFO "full
control over both your military forces and your production" - while all four bodies hand the same five areas to the AI.

## 5. Checking

`tools/OpenHOI3.SaveCheck`'s checks that set a new game beside a save of its first hour - `firstdays`, `politics`,
`diplomacy`, `espionage`, `aistrategy`, `theatres`, `agents`, `techgap`, `supplypass`, `write ... new` and the plain
start-date check - make their game **at the save's date** (`CStartSave`), so a save made at a bookmark's first hour is a
start save of that bookmark. The maintainer's, of 2026-11-15, Ireland played in each:
`Ireland1939_09_01_00.hoi3`, `Ireland1941_06_22_00.hoi3`, `Ireland1941_12_11_00.hoi3`.

The client's `--date=1939.9.1` beside `--country=` begins a game at that date, as the lobby's bookmark of it would.

## 6. Not established

- Which settings fields hold the original's two bookmark folders: `common/` and `cgm/` is inferred from the install.
- What a Quick Start game's victory conditions hold in the original, with none chosen; here none are.
- Where the original's sorted insert puts a history entry read out of date order among entries of its date (`CHistory.Fold`
  puts it after them, by its place in the file).
