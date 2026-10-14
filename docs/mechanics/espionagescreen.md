# The intelligence screen

`country_espionage`, the fifth country screen: the player's own espionage on the left, a list of countries with tabs
and sorts, and the chosen country's panel - its spies there and their missions, what the spies estimate of it, its
research, its production or its cabinet, and its parties' pie. Read out of the original in the espionage wave
(`reversing/findings/FINDINGS-espionage.md`, section D). The third piece of espionage (`espionage.md`), on the figures
the first two hold (`intelligence.md`). The interface file is Their Finest Hour's `interface/country_espionage.gui`.

**P** is the player's country; **chosen** the country the panel shows; **pres(c)** P's presence in c. Figures are
thousandths; "FFP" is a figure written with so many decimals, truncated; "trimmed" with as many as it needs.

## 1. Opening and the first chosen country

The top bar's `topbarbutton_intelligence` opens and closes it, as the other country screens. As a game starts the
chosen country is **the one P feels the most threat from** - of every country, in order, owning a province and not P,
the first of equals winning - and with no threat anywhere the first such country. P is never chosen.

**Each time it opens**: P's flag and name on the left and its figures (section 3); the country list **made again from
nothing** by its last tab, faction or continent; and the chosen country's panel.

**Each frame**: the left; the chosen country's spies count and month (section 4 is made again where either changed,
else only the production rows); the mission rows; every country row's live figures; the coup button; the covert-ops
points.

## 2. The country list

**Tabs**, each emptying the list, filling it, turning the sort direction over and running the last sort again - so a
sort and its direction are kept across tabs:

| button | who | in what order |
| --- | --- | --- |
| `all_button` (as it opens) | every country owning a province, not P; and where one province is selected on the map, its owner is chosen | by name |
| `majors_button` | owning a province, not P, `major` or an IC above 100 | the countries' order |
| `neighbours_button` | P's neighbours owning a province | theirs |
| `unaligned_button` | owning a province, in no faction, **neither P nor the chosen country** | the countries' order |
| a faction's button | its members owning a province, not P | by name |
| a continent's button | every country whose capital is on it, owning a province, not P | by name |
| `self_button` | **nothing**: no code asks its name, as in the original - the maintainer's word on 2026-10-14, "put it down as not working on purpose" | |

By name is the names' bytes compared, case and all, equals in the countries' order. The faction buttons stand at
`espionage_faction_start` a step `espionage_faction_offset` apart; the continent buttons from
`espionage_continent_offset` a step `espionage_continent_start` apart, wrapping at `espionage_continent_end` - the
file's two names the wrong way round, as diplomacy's (`diplomacyscreen.md`).

**Sorts**: one direction flag, true as the screen is made, serves all five. A sort button sorts by the flag as it
stands - true is largest, or Z, first - and then turns it over; **Priority always sorts highest first**, and turns the
flag over all the same. Equals keep the list's order.

| button | key |
| --- | --- |
| `sortby_name_button` | the name |
| `sortby_threat_button` | P's threat from the country |
| `sortby_covert_button` | pres(c)'s covert-ops points |
| `sortby_spy_level_button` | pres(c)'s spies |
| `sortby_spy_priority_button` | pres(c)'s priority |

**A row**, `espionage_country_entry`: `country_button` and `country_icon` (the flag) choose the country; `name`;
`threat` P's threat from it to one decimal; `covert` its whole covert-ops points; `spy_level` the whole spies as it is
made and **trimmed after a frame**; `priority` a button whose frame is the priority plus one - a click raises it,
4 wrapping to 1, a right click lowers it, 1 wrapping to 4, set at once and posting the priority (`espionage.md`,
section 3); the chosen country's `target_priority` follows. Threat, covert and spies are live. **No tooltip.**

## 3. P's own figures, `own_country_info`

| element | shows |
| --- | --- |
| `country_flag`, `label_nation` | P's flag and name |
| `value_free_spies` | the free spies, whole |
| `value_active_spies` | pres(P)'s spies, whole - the spies at home |
| `value_active_foreign_spies` | the sum over every country owning a province but P of pres(c)'s whole spies |
| `value_partisan_efficiency` | `§R` above 50, `§Y` above 0, nothing at or below, then P's `partisan_efficiency` trimmed - and no `§W` after |
| `value_spies_caught` | the spies caught this month |
| `foreign_spy_actions_indicator` | shown where pres(P)'s spies over the most are above 0.3 - more than three at home - and some country owning a province has spies in P, P itself counting; hidden else |
| `value_our_neutrality` | P's effective neutrality, rounded to a whole number |
| `own_priority` | pres(P)'s priority, a button as a row's |
| `our_spy_missions` | three mission rows about P: counter-espionage, boost ruling party, raise national unity |

## 4. The chosen country, `country_info`

**The header**: flag, name, and the faction symbol (hidden for no faction). Choosing a country **goes back to the
Production section**.

**Its spies**: `value_active_spies` the whole spies coloured by F (`intelligence.md`, section 1): `§G` above 0.6,
`§Y` above 0.3, `§R` else, then `§W`; `spy_bar` frame the spies plus one; `target_priority` a priority button;
`covert_ops_value` the whole covert-ops points.

**Missions**, `spy_missions`: a row for each mission but raise national unity - counter-espionage, military, tech,
boost ruling party, boost our party, lower national unity, increase threat, covert ops - each its title and a
`priority` button whose frame is the mission's weight plus one; a click or a right click posts the weight
(`espionage.md`, section 3). A mission row says the mission's description as its tooltip.

**The coup**: `attempt_coup_button` is **never hidden**: enabled where P has one whole spy there, at least
`COUP_COVERT_OPS_COST` points there, and the coup may be attempted (`espionage.md`, section 11); pressed, it posts the
coup with nothing more asked.

**The estimates.** A generator of the screen's own, seeded with the month of the year (0 for January) plus P's
number in the original's list - so they hold still for a month, and repeat in that month a year on. With F the figure
for the chosen country:

    no intelligence:  P's spies there under one whole spy
    u  = clamp(1 - F + 0.1, 0.1, 1)
    b  = SPY_INTEL_MISSION_BONUS (0.5), whatever missions are set
    i  = P's offmap_industry_intel

and an estimate is `v = base + trunc(k x scale / 1000)` with `k = floor(((r - 0.5) x spread + 0.0005) x 1000)`, r a
draw over 2^31 in single precision - so within half the spread of the scale either way - held to a floor, and some to
a ceiling that is the scale. In this order, each drawn whether or not it is shown:

| element | without intelligence | otherwise |
| --- | --- | --- |
| `counter_intel` | the title of counter-espionage, `: ?` | the title, `: `, and the chosen country's spies on counter-espionage at home - `level x share / 1000` - held to 0..10 spies, spread u, to no decimals |
| the seven goods, energy, metal, rare materials, crude oil, fuel, supplies, money | `?` | the stock - its exile pool, or its acting capital's pool - within `clamp(u - i, 0, 1) x (1 - b)` of itself, floored at 0; above 99999 in thousands with a `K`. Drawn only with intelligence |
| `ic_number` | `?/?/?` | **exact**: the IC going to waste, the top bar's first figure (`§R` above nought, else `§G`), `/`, its provinces' IC before any bonus (`§R` where above the IC it has, else `§G`), `/`, the IC it has |
| `dissent_number` | `?` | dissent held to 0..100, spread `u x (1 - b)`, two decimals |
| `national_unity_number` | `?` | national unity held to 0..100, spread u, one decimal |
| `diplomacy_number` | `?` | diplomatic influence, floored at 0, scale 100, spread u, no decimals |
| `manpower_number` | `?` | manpower, floored at 0, scale 100, spread u, no decimals |
| `leadership_number` | `?` | its leadership, floored at 0, scale 100, spread u, trimmed |
| `espionage_number` | `?` | its free spies, floored at 0, scale 10, spread u, whole |

**Research**, on the same generator after them, with `k = clamp(F + i, 0, 1)` and `u' = clamp(u - i, 0, 1)` - **not
hidden behind the one-spy test**:

- `current_research`: the first `trunc(research slots x k + 0.5)` of the chosen country's research queue, each the
  technology's name and ` (p%)`, p its progress in whole percent plus `trunc((r - 0.5) x u' x 99)`, held to 0..99;
- `top_tech` and `weak_tech`: the technology categories by their level - the chosen country's ability in each -
  `min(trunc(5k), all of them)` rows, the highest first and the lowest first; each the category's name and its level
  within `u' x 2.5` levels either way, floored at 0, trimmed - **the top list shows the least of this row's figure and
  the last's, the weak list the greatest**, so neither ever turns back.

So with nothing known - F at or below nought and no industry intelligence - both are empty.

**The pie**, `political_popularity_chart`: the chosen country's parties' popularity as the politics screen draws P's
(`politics.md`, section 11) - **exact, with no test of what is known**.

## 5. Production and ministers, `country_info_sections`

`button_viewproduction` and `button_viewministers` are two tabs, the pressed one on its second frame.

- **Production**: `units_label` says `SPY_PRODUCTION_LABEL`; `unit_queue` lists each of the chosen country's
  constructions in its order **that a roll shows**: with `chance = trunc(trunc(100000 x
  SPY_BASE_PRODUCTION_DETECTION_CHANCE (0.5) / 1000) x F / 1000) / 1000` - fifty in a hundred at F = 1 - and r from the
  C runtime's generator stepped twice from the construction's number plus the month's key: `s1 = seed x 214013 +
  2531011`, `s2 = s1 x 214013 + 2531011`, `r = ((s2 >> 16) & 32767) x ((s1 >> 16) & 32767) % 100`, shown where r is
  under the chance - the same each day of a month. Each row is the production screen's: the kind's background, the
  counter, the name, the progress, `+N` for a series; its target nothing.
- **Ministers**: `units_label` the government's name; `minister_list1` and `minister_list2` the chosen country's
  posts as the politics screen lays out P's, a `foreign_minister_entry` each - the post, the name, the personality,
  the party's colour, the portrait, and `???` where the post is empty - **with no test of what is known**.

## 6. Tooltips

| element | says | delayed |
| --- | --- | --- |
| the pie | the slice's party named for the chosen country, ` - §Y`, the percent, `%§W` | `QUALIFIES_FOR_CABINET`, its seats |
| `value_spies_caught` | `ESP_SPIES_CAUGHT` | where P's `counter_espionage` comes from |
| `foreign_spy_actions_indicator` | `DETECTED_SPY_ACTIONS` | |
| `value_our_neutrality` | `ESP_NEUTRALITY`, P's effective neutrality rounded | |
| `value_active_spies`, **in either window** | `ESP_NUM_SPIES` of the chosen country | `ESP_NUM_SPIES_DELAYED`, its priority |
| the seven goods, `ic_number`, `diplomacy_number`, `espionage_number`, `manpower_number`, `dissent_number`, `national_unity_number`, `leadership_number` | `ESP_ESTIMATE_RES`, NUM **the box's text as it stands**, colours and `?` included; ITEM the good's name, or `MODIFIER_IC`, `DISTRIBUTE_DIPLOMACY`, `DISTRIBUTE_ESPIONAGE`, `MANPOWER_IRO`, `DISSENT_IRO`, `ESP_NATIONAL_UNITY`, `PV_LEADERSHIP` | `ESP_ESTIMATE_RES_DELAY` |
| `attempt_coup_button` | two lines: `COUP_DESC_DISABLED` where the two ruling parties are of one group or the target's government is not fractured, else `COUP_DESC` where the player's group holds a seat there and `COUP_NOT_ENOUGH_SUPPORT` where it holds none, both with the target's name; then `COV_OPS_COST` and the cost, or `COV_OPS_COST_LACKING` with the cost and the points (`reversing/findings/FINDINGS-espionage.md` D.9, read after the maintainer's screenshot) | |
| a mission row, its title included | the mission's `SPYMISSION_*_DESC` | |
| a minister row | its colour: the party's name; anything else: what his personality does | |

Nothing else answers: not the country rows, the research rows, the production rows, the priority buttons,
`counter_intel`, `covert_ops_value`, `spy_bar` nor the icons.

## What looks wrong and is built so

The original's, none put to the maintainer but the first:

- `self_button` does nothing;
- the Priority sort never reverses, yet turns the shared direction over;
- each opening makes the list again, and only All takes up a selected province's owner;
- Unaligned leaves out the chosen country;
- the mission bonus narrows the goods' and dissent's estimates whatever missions are set, and nothing else's;
- IC is exact though its tooltip calls it an estimate, and the pie and the ministers show everything at any knowledge;
- the estimates' generator is seeded by the month of the year and P, so every target shares a month's draws, and the
  same month next year gives them again;
- the strong and weak technology figures are held against the row before;
- the home spies' tooltip on the left speaks of the chosen country;
- the spies' delayed tooltip says ten spies, whatever the define;
- the partisans' colour is red only above 50 and never closed;
- a row's spies are a whole number for a frame and trimmed after;
- an estimate's tooltip repeats the box's colours and its `?`.

## Ours

- **The estimates' generator** is the game's kind of generator seeded the usual way with the month's key; how the
  original turns that key into its first state was not read.
- **A good's name in an estimate's tooltip** is the `_IRO` key the diplomacy screen names it by; the original's own
  lookup of a good's name was not read.
- **The top and weak technology lists** take the chosen country's own ability, where the original takes the higher
  of it and that of the country sharing a theory with it.
- **The automation arrows** are the other country screens', and greying under AI control is not built.

## Not established

- What raises the `intel` alert that also opens the screen.
- The coup tooltip's body, as above.
