# The statistics record

An enhanced game's own: every country's figures, a value a day for the whole game, kept in the game and its save.
Nothing here is the original's, which keeps statistics only for its ledger and its end screen.

It stands where BlackICE's Lua collected into files - a file a statistic a country, a line added a day - which did
not work here and was set aside when the utility was ported. The user's words, 2026-11-22: "the stat collection
should then also be made use a built-in data source, not the CSV-via-LUA approach." Decided on 2026-11-24, in this
order: the record **in the savegame**; **a fixed and native list** of series, with the collection a rule of the
lobby; **every country**; a page API. Asked whether older years might be kept at one value a week: "A lower
resolution is not acceptable." And of how finely a value is kept, section 3: "Go with the 3rd row, and keep it in
multiplayer."

`CStatistics`, `CStatisticSeries`, `CCountryStatistics`, `CStatisticsBlock` in `lib/OpenHOI3.Sim/Statistics`;
`CSaveStatistics` in `lib/OpenHOI3.Content/Saves`; `CStatisticsExport` in `lib/OpenHOI3.Content/Statistics`;
`OpenHOI3.Statistics` in `CLuaWindows`; the rule in `CGamePlaySettings` and `src/FrontEnd/CLobbyOptions`.

## 1. In a line

As a day turns, after its pass, the figures of every country that owns land or is in exile are kept under that date;
a page reads any one of them as a run of numbers, a number a day.

**Nothing in the game reads the record.** It is in no hash and no command: two machines of one game need not hold
the same one, and a game plays the same with it off.

## 2. The series

Fixed, the engine's own, 73 with the base game's ideologies - the names BlackICE's collector wrote its files under:

| group | series |
| --- | --- |
| leadership | `ls_TotalLeadership`; `ls_Percent_Research`, `_Espionage`, `_Diplomacy`, `_NCO`, each share in percent |
| spies and neutrality | `intel_FreeSpies`, `intel_DomesticSpies` (its level at home), `intel_TotalSpiesAbroad` (its levels in every other country added); `diplo_Neutrality`, `diplo_EffectiveNeutrality` |
| industry | `prod__IcEff` (the `industrial_efficiency` modifier and the technologies' `ic_efficiency`, in percent), `prod__TotalIc`; and for each of `LendLease`, `Consumer`, `Production`, `Supply`, `Reinforce`, `Upgrade`: `prod_<Name>_%`, the share in percent, and `prod_<Name>_IC`, the share times the IC |
| politics | `pol_Popularity_Single_<ideology>` for each ideology; `pol_Popularity_Group_<group>`, its ideologies' added |
| land and units | `other_ProvincesControlled`, `other_ProvincesOwned`; `other_u_LandCountTotal` (brigades), `other_u_AirCountTotal` (wings), `other_u_NavalCountTotal` (ships), `other_u_TotalDivisions`; `other_ManpowerTotal` |
| goods | for each of `energy`, `metal`, `rares`, `oil`, `supplies`, `fuel`, `money`: `res_pool_<good>` (the stockpile), `res_balance_<good>` (the day's income less its spending, `stockpile.md`), `res_tradeFor_<good>` and `res_tradeAway_<good>` (traded, allied supply left out) |

*Ours, against the collector:* `diplo_EffectiveNeutrality` is the effective figure, where the collector wrote the
plain one under both names; the unit counts are the country's units in the field for every country, where the
collector counted an AI country's build queue in.

**Who is kept:** every country that owns a province or is a government in exile, on that day. A country that comes
to be is kept from its first day; one that is gone is kept no more, and its series, read, holds its last figures
across any days it was away.

## 3. How finely a value is kept

A value is sampled in thousandths and **kept over a step, rounded to the nearest**:

- a stockpile, a balance and a trade - the `res_` series - to **whole units**;
- every other series to **hundredths**.

That is the choice that makes every country, every day, small enough. Measured on 2026-11-24 over a year of BlackICE
played by nobody, 81 countries, as a save holds it: 1022 KiB with everything in thousandths, 557 with the goods in
whole units, **505 as kept**, 432 with the rest in tenths. So about **half a MiB a game year**, some 6 MiB by 1948 - a
year of war not measured, and likely larger.

Hundredths is what the collector wrote of popularity and finer than the whole numbers it wrote of everything else.

## 4. A year packed

A country's part is its finished years, each packed, and the year running as plain rows. **A year is packed as it
turns** and never again: a series' values become their day-to-day differences, each written in as few bytes as it
needs - seven bits to a byte, its sign folded into its lowest bit - and the whole is deflated at the smallest size.
The block's first byte is the format's number, 1; then how many series and how many days. Bytes that are no such
block unpack to nothing.

Plain numbers of the same year are some twenty times the size.

## 5. In the save

An enhanced save's top holds `openhoi_statistics`, after the combat reports; a faithful game writes none.

    openhoi_statistics={
        collect=yes
        series={ "ls_TotalLeadership" "ls_Percent_Research" ... }
        GER={
            first=706650
            y={ 1936 365 <letters> }
            y={ 1937 120 <letters> }
        }
    }

`collect` is the game's rule (section 6). `series` is the names in the order the blocks hold them, so that a save of
another list of ideologies is read into today's places and a series today's game has not is left out. A country's
`first` is the number of the first day kept; each `y` a year, how many of its days the block holds, and the block's
bytes as **a bare word** - base64 with `-` and `_` for its `+` and `/` and no `=` at its end. A finished year holds
365 days; the running year is packed as it stands at each save, with the days it has.

On a load the last year of a country goes on as its running year where it is this year's; any other is finished.

**A reload from an earlier save has that save's record**, the days after it gone with the timeline: the collector's
files went on being added to, and its page had to drop the abandoned readings.

**Between machines** the record travels in the save. A game sent is deflated before it is cut into pieces
(`multiplayer.md`, section 3), which the blocks, being deflated already, gain nothing from: some 6 MiB more to a
client that joins in 1948, about 2 in 1940.

## 6. The rule

**Whether a game keeps the record is the lobby's choice**, on unless it is turned off: `CGamePlaySettings.
CollectStatistics`. It is shown in a window of the mod's beside the lobby's own options, `openhoi_lobby_rules`,
where the mod's `interface/openhoi` declares one: its button `statistics` turns the rule over and shows an `X` while
it is on, and the chat says the change as it says an option's. **What the rule keeps is said under the mouse on the `?` beside it**,
the button `statistics_help` and nowhere else - the text of
`OPENHOI_RULE_STATISTICS_TIP`: figures of the game, in the save, nothing of the player and nothing sent anywhere. Asked
for on 2026-11-24: "Players might think this is a user data collection." On a client the button is greyed and the host's
choice shown; the choice travels with the original's seven, after them.

A game with the rule off keeps nothing and writes `collect=no`. The rule is not changed in play.

## 7. `OpenHOI3.Statistics`

For a mod's page, in its `Collect` (`modwindows.md`):

| | |
| --- | --- |
| `Collecting()` | whether this game keeps the record |
| `Names()` | the series' names, in the engine's order |
| `Countries()` | the tags kept, sorted |
| `Series(tag, name)` | `{ first_day = <the day's number>, values = { a number a day } }`, the values in the series' own units; nil where the country or the series is not kept. `OpenHOI3.DateOf` turns a day's number into a date |
| `Export()` | the record written out, section 8: the folder and how many files, or nil and why |

## 8. Written out

`Export()` writes every country's every series as a file under the player's folder,
`openhoi_statistics/<year>_<month>_<day>_<hour>/<TAG>/<series>`, in the collector's layout, which the mod's plotting
tool reads: `Date,<series>` and then a line a day, the day counted from 1 January 1936 as nought, a comma, and the
value - whole where it is whole, else to the places it has. Only when asked: nothing is written as the game runs.

## 9. BlackICE's page

`script/openhoi/windows/stats/statistics.lua`: the countries and the statistics to tick, each list under a filter that only hides rows - a country ticked stays
ticked out of sight - Plot, Clear and Export, and **One plot**, which draws every series read into one frame
(`OpenHOI3.ImGui.PlotMany`, `modwindows.md`) with a line of the legend each, in place of a frame a series. Its
Setup page is gone - the rule is the lobby's and every country is kept - and the mod's own collector does nothing on
this engine.

Tried on 2026-11-24: ten days played, the page showing 80 countries and 73 statistics; the lobby's window shown and
its tick turned with `--click`. The one frame was looked at with three series of that game through a page made for the look and taken away again.
**Not reviewed in play**: the page's own plots, its filters, the export, and a game saved and loaded on
screen - the last is under test.

`OpenHOI3.SaveCheck headless <install> <until> statistics` keeps the record in a game nobody plays and says how large
it is in a save.
