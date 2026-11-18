# Bookmarks: a game begun at a later date

A bookmark is a dated entry point: the lobby's Historical Start lists them, and choosing one moves the world to its date
before a country is chosen. The Quick Start panel offers four starts of its own, dated in the executable. Built
2026-11-15 from two readings of that day - `reversing/findings/FINDINGS-bookmarks.md` (the file, the lobby, which
history a later date brings) and `reversing/findings/FINDINGS-laterstart.md` (what the new-game pass does once the
history stands) - and checked against three reference saves made at the first hour of the 1939.9.1, 1941.6.22 and
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

**The end of the entry** takes every country owning no land and in no exile off its wars and out of its faction - telling
nobody of a change of diplomacy, so the landless keep their war laws - and starts the played country's strategic warfare
records of its enemies (`strategicwarfare.md`); rebel-held provinces begin their rebels' clock at the start.

**A second round of 2026-11-15** (`FINDINGS-laterstart.md`, sections 13 to 16; `FINDINGS-alignment.md`, section 2.5):

- **A history war goal is added only where its `is_valid` holds at the date**, the countries' histories having been
  replayed before the wars', and stamps its cooldown a month on from the defines' start date (`war.md`, section 3): so no
  exile of the date has a goal against it, and France's and Britain's `protect` of Belgium is the save's 50. A history's
  `set_country_flag` and `clr_country_flag` are read.
- **A government in exile has a day of its own** (`war.md`, section 14): base production whatever it holds, its IC and
  conversions, its supplies and fuel handed to whoever holds its seat, then its shares, into its own pool; its brigades
  upgraded towards its faction leader's levels, and its units drawing on friends' depots for free.
- **The day is run a whole country at a time** (`alignment.md`, section 3; `tick.md`): a country's alignment step reads the
  IC and the surrenders of the countries before it and the day's most-threatening country, so at 1939.9.1 China's
  first-day capitulation puts it in the Axis for every country after it. A puppet's automatic `align_towards_*` gives no
  influence, and the rebels measure from province 0's centre at (1, 1).
- **What the two days post runs after the look's laws**, in the order posted (`politics.md`, section 12), and the
  peace-time revert takes the last allowed law in the file's order (section 9): China, Japan's puppet on the first day at
  1939, gives up its war laws.
- **The history's wars stamp `last_war` on both sides** at the start date (`war.md`, section 4); a save omits a record
  towards a country owning nothing.
- **An order of battle's `TAG = { ... }` block is the country's record towards that one** (`units.md`, section 3): Germany
  may pass through Sweden from the 1941 bookmarks on.
- **`conquer_prov` never holds a province the country owns** (`aistrategy.md`, section 5).

**A third round of the same day** (`FINDINGS-laterstart.md`, section 17):

- **Every theatre headquarters the AI makes as a game is entered brings fifteen days of its use out of its owner's
  stockpile**, being put on the map as a unit entering a province is - the 2.010 supplies a country with one had too many
  on the first screen, at 1936 as at every later start (`supply.md`, section 14; `theatres.md`, section 10). A new command
  over a unit and a rebel army do the same, and their brigades hold every technology at nought.
- **The energy conversion balances on the day's own pools**, usage cleared at the head of the day (`ic.md`, section 4):
  crude oil and energy agree at 1936 for 77 of 77 countries.
- **An order of battle's `expeditionary_owner` makes its unit lent** (`units.md`, section 3; `expeditions.md`, section 4).
- **A faction's members stand as the history's passes reached them**, and **a NAP keeps the end date its history gave
  it**, and **the finished wars stand in the save's order** (`war.md`, sections 1 and 13; `savegame.md`, section 10).
- **The Lua's `GetPossiblePuppets`**, which our binding had under another name, so that `ITA.lua` failed from the first
  hour at 1939 with Ethiopia's capital Italy's; mended, Italy releases Ethiopia within a month (`lua.md`).
- **A country's `usage` is one pool, the original's** (`supply.md`, section 11): what the provinces beside the capital took
  out of the stockpile in the supply pass and what units in the capital drew, then industry's charge. The Lua's
  `GetDailyExpense` and `GetDailyBalance`, the top bar, its tooltip, the production screen and the save read it, so a
  country at war reads the original's deficit - Germany at 1941.12.11 a supplies balance of -720 a day where it read +55,
  against the save's pools' -638, the rest being Berlin's throughput. A first screen shows its second day's usage, no
  longer an estimate: the 1936 start save's for Germany, Britain and Ireland to the thousandth, Britain's 178.246 against
  178.293 at 1941.12.11.
- **What a country made at home is its second day's** (`resources.md`, section 3): entering collects no production, so
  the first screen shows the pool the second day left, and a non-existent country's holds two days' money and supplies,
  the original's shares adding to a pool only a country's own day clears. Supplies and money made are every country's
  the save's at 1936 and all but one country's at each later start - Ireland's 9.971 at 1936, 11.465, 21.840 and 26.208
  after, where an estimate taken after the days had given 0.728 at 1936 - and with them the day's balance on the top
  bar, its tooltip, the production screen and the Lua: Ireland's +8.23 a day at 1936 against the save's pools' 8.233,
  where it read -1.01. Germany's resources at 1941 differ on the second day itself, 312.093 metal to 332.084 at 1941.6.22.

### 3.1 Set beside the start saves

`OpenHOI3.SaveCheck` on 2026-11-15, the three later saves and the 1936 start save (whose figures are unchanged); the
stockpile, the build queue and the brigades' rows again on 2026-11-16:

| | 1939.9.1 | 1941.6.22 | 1941.12.11 |
| --- | --- | --- | --- |
| technology levels | 21,238 of 21,238 | 20,662 of 20,664 | 20,660 of 20,664 |
| occupation policies | 9 of 9 | 39 of 39 | 48 of 48 |
| nationalism | 560 of 560 | 1,337 of 1,337 | 1,383 of 1,383 |
| manpower, officers, influence | 73, 74, 74 of 74 | 72 of 72 each | 72 of 72 each |
| the stockpile's supplies | 39 of 74 | 46 of 71 | 45 of 71 |
| crude oil, energy | 73, 72 of 74 | 71, 71 of 71 | 71, 71 of 71 |
| the build queue | 55 of 55 | 61 of 61 | 45 of 45 |
| brigades' strength | 4,644 of 4,823 | 5,349 of 5,357 | 5,513 of 5,513 |
| brigades' organisation | 4,778 of 4,823 | 4,862 of 5,357 | 4,795 of 5,513 |
| laws | 707 of 707 | 707 of 707 | 707 of 707 |
| relation, threat | every pair | every pair | every pair |
| neutrality, effective | 101 of 101 | 101 of 101 | 101 of 101 |
| alignment | 101 of 101 | 101 of 101 | 101 of 101 |
| spies abroad | 10,052 of 10,100 | 10,051 of 10,100 | 10,054 of 10,100 |
| theatres (number, headquarters, fronts) | 106, 106, 104 of 106 | 96 of 96 each | 97 of 97 each |
| AI strategy lists | 74 to 75 of 75 | 73 of 73 each | 73 of 73 each |

**What still differs, and where it was left** (the worklist's *Loose ends*):

- ~~**Alignment**~~ and ~~**an exile's own pool**~~ - settled in the second round (above): alignment 101 of 101 at every
  date, the 1936 start's one miss with it, and every exile's pool the save's in every good but France's and Belgium's
  supplies.
- **The stockpiles' smaller drifts** of the two days, of the kind the 1936 start has; and **2.010 supplies** that a
  country holding its own seat misses by - Ireland at every later start, France and Belgium, and Denmark, Poland and Greece
  at 1936.
- **Brigades' organisation** held to a running ceiling by a law changed (Australia, Canada, Italy), supplies received,
  and lent units at their lender's officer ratio.
- ~~**`protect` towards Belgium**~~, ~~**China's three laws at 1939**~~, ~~**three `last_war` pairs**~~ and ~~**the
  `military_access` line**~~ - settled in the second round (above).
- **New Zealand's six posts at 1941**: `OfficeManagement`'s picks at war, made in the AI's pass at hour 0, the frame after
  the entry and before the first hour - a chance draw of a pass not built (`politics.md`, section 12).
- **Japan's and China's fronts in another order at 1939**: the original keeps areas up province by province as China's
  cores come back on its first day, where ours makes them whole (`theatres.md`, section 12).
- **What a written game shows that the checks above do not** (`OpenHOI3.SaveCheck write <install> new <start save>`, set
  beside the 1936 start's own differences, 2026-11-15): a NAP's history end date is written since (the German-Soviet
  pact's 1941.6.22 at 1939) and the finished wars stand in the save's order (`war.md`, section 1). **Since 2026-11-16**
  `out_of_supply_days` is the save's in every province at all four dates, the history counting a day for each change of
  controller (`supply.md`, section 18), and the two provinces at 1941.12.11 whose controller differed - Cyp and Wadi al
  Hamim - the save's too since land in no area goes to the enemy around it each hour, a new game's first included
  (`war.md`, section 11): with it every province's owner and controller is the save's at all eight start dates; every
  unit's leader is the save's but one fleet a later date, which the AI regroups before the first screen - order-of-battle
  leaders put on in file order, a country at a time, and a lent unit made with its lender's brigades (`leaders.md`, section
  5; `expeditions.md`, section 4); and the 1939 "Japanese cruisers" were no class at all - every ship has the save's type
  and keys, the save keeping 19 of Japan's 28 fleets under a theatre, the placement the 1936 start's `theatre/navy`
  difference shows. Still differing: the two active wars of 1941.12.11 above, a province `history` block the save writes
  for 144 provinces at 1939 and 8 at 1941.12.11, and keys inside 18 to 32 country blocks in another order.
- **China keeps its government in Chongqing at 1939.9.1**, and so its stockpile (837.034 metal, 415.163 rare materials, 3.200
  money, the save's): the original capital a government goes back to is the first declared in play, else the declared one,
  never the history's first (`war.md`, section 14). **The supply loss has its suppression term** (`supply.md`, section 8):
  Germany's first-screen supply need 59.371 at 1941.6.22 and 77.882 at 1941.12.11, and every brigade's strength of the
  1941.12.11 start save the save's. **The production need is the second day's**, a change of law repricing the queue without
  counting it (Germany 13.984 at 1936, `buildqueue.md` section 4); the diplomacy slider's need is influence's upkeep; and the
  ledger's Used by Industry is a day's IC in resources before the cap (`sliders.md`, section 2).
- **The IC a country's set-up works out at its head** (2026-11-16; `production.md`, section 1; `ic.md`, section 3), before
  its history's decisions and before its empty posts are filled: the starting shares divide the standing army by it, so
  the Soviet Union's purge dissent stays out of them (supplies made 163.604 at 1939.9.1, the save's; 1938.9.1 likewise),
  and a country owning no province keeps it, its history's ministers counted - Estonia from 1941 at IC 8, making 22.914.
  With both, every country's home production is the save's at every start but Italy's supplies at 1944.6.20, IC 6 in the
  save and 7 here, not explained. The 0.001 of two brigades of the 717. Infanterie-Division at 1941.6.22 is the Berlin
  network's starved Yugoslav end with the units where the AI has them, not a rule.
- **What the four saves of 2026-11-16 show that the three before them do not** (`firstdays`, a look for later): at
  1938.9.1 Spain's `civil_war` policy towards the Spanish Republic where the save has none; at 1943.2.1 and 1944.6.20
  brigades' organisation far below the earlier dates (3,849 of 5,444 and 4,818 of 6,693), British lines of the build queue
  about 0.85 the save's speed and American lines' progress short, and at 1944.6.20 Britain's supplies 4,723 over and
  France's fuel; Spain's, France's and the United States' smaller stockpile drifts. 1940.5.10 shows nothing new.
- **A guarantee a new puppet had**: China's of the Chinese Communists at 1939.9.1, which the original keeps as a dead
  object written in the save, and a game here deletes (`war.md`, *Ours*).
- ~~**Each faction's members in another order**~~ - settled: those of the defines' start date first, the bookmark pass's
  joiners after, each in the countries' order (`war.md`, section 13); every faction of the three saves agrees.

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
start save of that bookmark. The reference saves, of 2026-11-15, Ireland played in each:
`Ireland1939_09_01_00.hoi3`, `Ireland1941_06_22_00.hoi3`, `Ireland1941_12_11_00.hoi3`.

The client's `--date=1939.9.1` beside `--country=` begins a game at that date, as the lobby's bookmark of it would.

**A later start runs forward**: `OpenHOI3.SaveCheck headless <install> <end> from=<bookmark>` (`headless.md`, section 5).
On 2026-11-15 each of the three bookmarks ran four months with nobody playing - nothing thrown, no goods below nought, no
script key met that is not built. Poland falls in a month, as the original's does; the war in the east moves, slowly;
Japan's December war moves on China and the Philippines but hardly on the Indies; at 1941.6.22 the United States is in the
war within days, by Britain's invitation and the original's script on the save's neutrality. Germany's supplies fall from
some 29,600 to 2,000 within three weeks of 1941.12.11 - **parked, not lost**: the provinces rise from 29,571 to 66,676 as
the original's throw-back of demand swells what a narrow road east is asked for and the provinces beside Berlin keep it
(`supply.md`, section 12.1; `docs/enhanced.md`). A game loaded from the original's own start save does the same, and the
original's Japan emptied so in the Ireland game (527 left in its stockpile in May 1940, 112,729 in its provinces); whether
the original's Germany empties from this bookmark is not seen, its first hour alone being saved. A day takes about 3 s at
the 1941 starts, against 1.1 s at 1936.

## 6. Not established

- Which settings fields hold the original's two bookmark folders: `common/` and `cgm/` is inferred from the install.
- What a Quick Start game's victory conditions hold in the original, with none chosen; here none are.
- Where the original's sorted insert puts a history entry read out of date order among entries of its date (`CHistory.Fold`
  puts it after them, by its place in the file).
