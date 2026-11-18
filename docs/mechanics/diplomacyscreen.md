# The diplomacy screen

What `country_diplomacy` shows of what two countries hold of each other (`diplomacy.md`), and of where
each stands on the alignment triangle (`alignment.md`): the list of countries with its tabs and sorts,
the triangle's markers and the faction boxes, the chosen country's panel, and every tooltip on them.
Diplomacy's second piece, in the order set on 2026-10-12. Its decision list is `events.md`,
section 12; what it shares with the other country screens is `countryviews.md`.

Sources: `reversing/findings/FINDINGS-diplomacyview.md` - D the list half and E the panel half, read in
the diplomatic state wave, and F the triangle, the faction boxes and the tooltips left over, read when
the screen was built - all on 2026-10-12. Nothing in it was checked against a running game.

"The player" is the country being played; "the chosen country" is the one the panel shows. Figures are
thousandths and every division cuts toward nought unless a line says it rounds. FFP(v, n) is a figure
written to n places, cut.

## 1. When things happen

- **The screen is made** with the player chosen. **Opening it**: where exactly one province is
  selected on the map and has an owner, that owner is chosen; **where the country list is empty, the All
  tab is pressed** - so the first opening shows All, and after that the tab and sort last used stay;
  then the markers, the faction boxes, the panel, the decision list, and the panel back to Info. **The
  country rows are not made again.**
- **Every frame while it is up**: the faction boxes (section 4), the drift list, the markers where their
  time has come (section 4), every visible row's resource icons (section 3), the surrender bar, the
  panel (section 5) and the decision list's rows. **A row whose country owns no province any more is
  taken off the list** and the list and the markers made again.
- **A row's threat, name and flag are set when it is made and never again.**

## 2. The tabs, and the two kinds of button beside them

Each empties the country list, fills it, makes its rows - in the list's order - and makes the markers
again. No button shows that it is pressed.

| button | lists | order |
| --- | --- | --- |
| `majors_button` | every country owning a province that is `major = yes` or whose provinces' IC - before any bonus of its own - is above 100 | the countries' order |
| `neighbours_button` | the **player's** neighbours that own a province | the neighbour list's |
| `unaligned_button` | every country owning a province that is in no faction | the countries' order |
| `self_button` ("Selected") | the chosen country alone, owning a province or not | |
| `all_button` | every country owning a province | **by name** |

**A faction button** stands for each faction (`common/ideologies.txt`), from (56, 75) in steps of
(101, 0) - `diplomacy_sort_faction_start` and `_offset` - a `diplomacy_faction_sort_window` whose
`faction_symbol_big_button` is given the sprite `GFX_sort_<faction>` and the faction's name; it lists the
faction's members that own a province, **by name**.

**A continent button** stands for each continent of `map/continent.txt` but `lake` and `greenland`, in
the file's order, a `continent_button` with the continent's name, from `continent_offset` (57, 133) in
steps of `continent_start` (100, 0), to the next row 20 down at x 57 when x reaches `continent_end`'s x
(356) - the file's two names are the wrong way round, as its own comments say. It lists every country
owning a province **whose capital is on that continent, by name**.

**By name** is the names' bytes compared as unsigned numbers - case and all, accented letters after `z`
- and equal names keep the countries' order.

## 3. The country list

### 3.1 Sorting

**One direction flag serves all nine sort buttons**, cleared only when the screen is made; **each press
turns it over and then sorts**. The flag set means:

| button | key | the flag set | the flag clear |
| --- | --- | --- | --- |
| `sortby_name_button` | the name, as above | Z to A | A to Z |
| `sortby_threat_button` | the threat the player feels from the country - the row's figure | highest first | lowest first |
| `sortby_energy` / `metal` / `raremet` / `oil` / `supplies` / `fuel` / `money_button` | the country's day's balance of the good (3.3) | the largest surplus first | the largest deficit first |

So the first press of any gives the flag set, and Name then Threat gives Threat lowest first. **A sort
makes the rows again and leaves the list as it was**: the next tab, faction or continent, or a country
falling off the list, undoes it; and it does not touch the markers.

### 3.2 A row, `diplomatic_country_entry`

| element | shows |
| --- | --- |
| `country_button` (a checkbox) | **nothing ticked, the chosen country's row neither**; a click ticks it and chooses the country |
| `country_icon` (a button) | the country's flag; a click chooses the country |
| `name` | the country's name |
| `threat` | FFP(the threat **the player feels from this country**, 1) - no sign, no colour |
| `icon_energy` ... `icon_money` | no figure: the icon alone, **white where the country's day's balance of the good is above 0, and (1.0, 0.5, 0.5) - a light red - at 0 and below**, again every frame |

### 3.3 The day's balance of a good

The day's change the top bar's ledger shows for it (`topbar.md`, section 14): what came in less what
went out, **with supplies and fuel taken without what convoys brought in**.

### 3.4 Choosing a country

A row's checkbox or flag, a marker, a faction member's flag (not REB) and opening the screen choose a
country: its marker is brought to the front, the drift list is made again, the panel is filled, and
**the panel goes back to Info** - the relation and goal windows hidden, the status list shown. **The
country list is not touched**, so "Selected" does not follow a later choice until pressed again.

## 4. The triangle

### 4.1 The markers

**One marker a country of the country list but the chosen one, in the list's order, and then one for the
chosen country** - or for the player where nothing is chosen - which is made last. A marker is an
`alignment_button` with the country's flag, put in the window `alignment` at **`alignment_origo` (805,
465) + (x / 1000, y / 1000)**, x and y the country's place on the triangle, **each cut toward nought,
and y added as it is**: the triangle stands on its point, democracy's corner (0, -146) above the origin
and the other two below. A click chooses its country.

**The markers are made again** by every tab, faction and continent button, on opening, when a country
falls off the list, and **when the clock has passed the screen's next time for them**, which is then set
to the clock + `ALIGNMENT_INTERVAL` whole days (24 in Their Finest Hour) - not by a sort.

### 4.2 The faction boxes

A `faction_box_window` for each faction, from `faction_start` (700, 79) in steps of `faction_offset` (0,
60): its `faction_symbol_button` given the faction's `icon` as a frame of `GFX_icon_factions`
(`ideologies.txt`: 2 the Axis, 3 the Allies, 1 the Comintern), its `name` the faction's localised tag
("Axis"), **its `victory_progress` the faction's share of the world's victory points in whole percent**,
and in its `faction_box` **one `faction_member_entry` a member, in the order the members joined, the
leader first**: its `faction_button` the member's flag, which chooses it unless it is REB. Each frame the
bar is set; the members are made again where they have changed.

## 5. The panel

### 5.1 `country_info`

| element | shows |
| --- | --- |
| `country_flag` | the chosen country's flag |
| `faction_symbol_icon` | hidden where the chosen country is in no faction; else the faction's `icon` as a frame of `GFX_icon_factions_small` |
| `label_nation` | the chosen country's name |
| `ruler_label` | **nothing**: no code fills it, so it keeps the file's empty text |
| `ruler_value` | the chosen country's ruling ideology: `<ideology>_<TAG>` where that has a text, else the ideology's |
| `label_relation_value` | the chosen country's relation with the player, whole, cut, no sign or colour - 85 for the player's own |
| `label_threat_us_value` | FFP(**the threat the chosen country feels from the player**, 2) |
| `label_threat_them_value` | FFP(the threat the player feels from the chosen country, 2) |
| `label_neutrality_value` | FFP(the chosen country's **effective** neutrality, 0) + "%" |
| `label_our_neutrality_value` | for the player's own country FFP(its effective neutrality, 0) + "%"; otherwise its colour + FFP(effective, 0) + "%§W/" + FFP(the ceiling for war on the chosen country, 0) + "%" - **§R where the effective neutrality is above the ceiling**, so war cannot be declared, else §G: "§R80%§W/45%" (`diplomacy.md`, section 6) |
| `label_highest_threat_value` | FFP(the threat the chosen country feels from its most-threatening country, 1) |
| `highest_threat_flag` | the most-threatening country's flag |
| `surrender_progress` | a bar, green then red: green is what is left, 100 - ceil(progress / 10) |

The labels beside them keep the file's texts. The three toggles change no button's look:
`toggle_diploview_button_country` shows the status list, `_relation` fills and shows the relation list,
`_goals` fills and shows the goal list.

### 5.2 The status list, `diplostatus_list`

The chosen country's lines, **one row a line, wrapped at the row's text box**, the lines in turn §W, §Y,
§W, ... from §W, a wrapped piece taking its line's colour. **Only countries owning a province are
counted**, the countries walked in their order. A list is "A, B and C" (`AND`). The lines, in this
order, each where it has anybody to name:

1. `ATWARWITH` "At war with $LIST$." - **a line a war**, the wars as the walk meets them, each naming
   **the whole side** the chosen country is not on, owning a province or not; a country is marked `*`
   after its name where it stands **first** on the enemy side of any of the chosen country's wars;
2. `TRUCEWITH`; 3. `FACTION_MEMBER_OF` "$NAME$ is a $FACTION$ member."; 4. `INFLUENCEFROM`;
5. `ALIGNINGTOWARDS`, **naming factions, not countries**; 6. `NAPWITH`; 7. `ALLIEDWITH` "Allied with
   $LIST$."; 8. `LEADVASSAL` "Has the following puppets: $LIST$."; 9. `AMVASSAL` "Is a puppet of
   $LIST$."; 10. `GUARANTEED` "Independence guaranteed by $LIST$."; 11. `GUARANTING` "Guaranteeing the
   Independence of $LIST$."; 12. `CD_ISEMBARGOED`; 13. `CD_ISEMBARGOING`; 14. `CD_GIVESACCESS`;
   15. `CD_HASACCESS`; 16. `CD_DEBT`; 17. `LEND_LEASE_TO_LIST`; 18. `LEND_LEASE_FROM_LIST`; and 19. the
   key `ON_MISSION` - **which has no text in any file** - + "'" + a mission's title + "'".

Each `$LIST$` is itself looked up as a key before it is put in.

### 5.3 The relation list

Every country owning a province but the chosen one, a `domesticpanel_relation_entry` each:

- `name`: `"@" + tag + name` - the flag and the name, no space between;
- `value`: v, the chosen country's relation with it, whole and cut, **coloured by f = (v + 200) / 400 as
  (1 - f, f, |0.5 - f|)**: (1, 0, 0.5) at -200, (0.5, 0.5, 0) at 0, (0, 1, 0.5) at +200.

**Two flags of their own**: by relation, set when the screen is made, best first while it is set - the
toggle sorts without turning it, `sortby_relation_button` turns it and then sorts, so its first press
gives worst first; by name, clear when made - `sortby_nation_button` turns it and then sorts, so **its
first press gives Z to A**. The sort puts each country in from the back, behind the first it does not
come before, so equals keep the countries' order.

### 5.4 The goal list and the drift list

**The goal list** is empty unless the chosen country is at war, and then lists the goals of its wars,
`goals_by_us` and `goals_at_us`. **The drift list** names each country that influences or aligns the
chosen one, or that it influences or aligns, with the faction's `align_towards_*` figure.

## 6. Tooltips

Nothing else on the screen has one: not a tab, a sort button, a toggle, nor a row's checkbox, flag,
name or threat.

| element | text | delayed |
| --- | --- | --- |
| a row's resource icon | `DIP_NO_SURPLUS` "$NAME$ is in deficit of §Y$VAL$§W $RES$ each day." with VAL = -b where b < 0, else `DIP_SURPLUS` "...producing a surplus of..." with VAL = b; **VAL is b / 1000 in single precision to two places, rounded**; RES `ENERGY_IRO` ... `MONEY_IRO` | |
| `highest_threat_flag`, `label_highest_threat_value`, `label_highest_threat` | `DIP_HIGHEST_THREAT_TOOLTIP`, COUNTRY the most-threatening country's name, VAL FFP(the panel's figure, 1) | `DIP_HIGHEST_THREAT_TOOLTIP_DELAYED` |
| `label_neutrality_value` | the sources of the chosen country's `neutrality_change` (`modifiers.md`, section 8) | |
| `label_our_neutrality_value` | for the player's own country, the sources of its `neutrality_change`; otherwise **the ceiling's lines** (6.2) | the player's `neutrality_change` sources |
| `surrender_progress` | `SURRENDER_TITLE`, VAL = progress / 1000 x 100 to two places | `SURRENDER_DETAILS` for the player, `SURRENDER_DETAILS_THEY` otherwise, OCCUPATION and UNITY to one place |
| a relation row | `RELATION_CHANGE_VALUE` + " " + §G (§R where negative) + the month's drift / 1000 x 12 to two places, rounded + §W | **its breakdown** (6.3) |
| a marker | **its country's drift tooltip** (section 7) | section 7 |
| a member's flag | `FACTION_MEMBER_OF`, NAME the member, FACTION its faction's name | |
| a box's `faction_symbol_button` | the faction's name | **its rules and modifier** (6.1), then "\n" + the faction's victory conditions met / all where there are any |
| a faction sort button | the faction's name | its rules and modifier |
| `victory_progress` | `FACTION_PROGRESS_DESC`, WHO the faction, PERCENT FFP(its share x 100, 2), OUR FFP(its points, 0), TOTAL FFP(the world's, 0) | |

### 6.1 A faction's rules and modifier

**Its rules**: for each of the rules, in this order - `limited_war`, `allience_wide_guarantee` (so
spelled), `free_resource_gifts`, after a fourth, `NONE`, that no file key is known to set - that the
faction's `rule` block sets to `yes`, a line of its localised name (`RULE_LIMITED_WAR` "Limited war", ...) + ": §G" + `YES` + "§W", a
newline between lines. Then "\n", then **every value of its `modifier` block**, a line each as a
modifier writes them (`modifiers.md`, section 8). "Limited war: §GYes§W\nTerritorial Pride: ..."

### 6.2 The ceiling's lines

For the player P and the chosen country C, each a line:

1. `WARNE_THREAT` "Mutual Threat is §Y$VAL$§W." - the larger of the two threats between them, FFP 0;
2. `WARNE_CLAIMS` "Territorial Claims: §G+$VAL$§W." - `NEUT_REDUCTION_AT_CLAIMS` - where the ceiling's
   claims hold;
3. `WARNE_CONTINENT` "Different Continent: §R-$VAL$§W." - `NEUT_INCREASE_DIFF_CONTINENT` - where their
   capitals' continents differ and C is not P's neighbour;
4. `WARNE_FACTION` "Member of $NAME$ : $VAL$§W." where P's faction's neutrality is not 0, VAL "§G+" (§R
   where negative) + FFP(it, 0).

### 6.3 A relation's breakdown

For the chosen country C and the row's country R: **where C's relation with R is above 100 and the
month's drift is not negative**, `RELATION_CAPPED` alone. Otherwise a line each, in this order, of a
colour - "§G+" above 0, "§Y" at 0, "§R" below - the figure, "§W: " and the text:

| line | where | figure |
| --- | --- | --- |
| `DIPRELCH_DEPENDENCY` "Puppet-Master Relationship" | one is the other's puppet | +3.00 |
| `DIPRELCH_WAR` "War" | they are at war | -1.20 |
| `DIPRELCH_NBOR_PEACE` "Neighbours at Peace" | R is C's neighbour and neither is at war | +0.60 |
| `DIPRELCH_GUARANTEE` "Guarantees" | either guarantees the other | +0.96 |
| `DIPRELCH_HASACCESS` "Transit Rights" | C has access through R | +0.30 |

The figures are each term's month (`diplomacy.md`, section 3) / 1000 x 12 in single precision, two
places, rounded - the term, not what the pair's month comes to.

## 7. The drift tooltip

A marker's tooltip, for its country K.

**The text starts with the drift** - the vector its next step would add, **before it is kept inside the
triangle** - as "(" + "+" where x > 0 + FFP(x, 2) + " / " + "+" where y > 0 + FFP(y, 2) + ")\n":
"(+1.34 / -0.52)".

**The resources line**, kept for the end: the strategic resources of the provinces K controls, each
once, in the order its provinces meet them, as "A, B and C"; where there is one and K is in no faction,
`RESOURCE_BONUS_ALIGNMENT_TT` with them.

**K in a faction**: the whole text is `DIP_ALIGNED_DRIFT` "$NAME$ is a $FACTION$ member and will not
drift." - the drift line with it.

**K in none**: for each faction with members, its group's seven terms (`alignment.md`, section 5, threat,
revanchism and repulsion nought from 200 away), and their total t, give an entry:

- **its text**: `FACTION_DRIFT_INSTANT` "$NAME$ is drifting §Y$VAL$§W towards $FACTION$", VAL FFP(t, 2);
  and where K is within `FACTION_STRAT_BONUS_DIST` of the group (its square below the define's) the
  resources line becomes `CAN_GIVE_STRAT_BONUS_BUT_NONE` with no resources, else `CAN_GIVE_STRAT_BONUS`,
  WHO the faction's leader;
- **its delayed half**: "(" + the faction's name + ")", then each term that is not nought, a line each in
  the order `FACTION_DRIFT_RELATIONS`, `_PROXIMITY`, `_IDEOLOGY`, `_REVANCH_NEW` (with FACTION and
  COUNTRY), `_THREAT`, `_REPULSION`, `_INFLUENCE`, VAL FFP(the term, 2) - the three taken off written as
  their figure behind the texts' own "§R-".

**The entries are sorted by t, highest first**, of equals the earlier faction first, and **the first's
text is replaced** by `FACTION_DRIFT_TOP_INSTANT` "$NAME$ is mostly drifting towards §G$FACTION$§W with a
value of §Y$VAL$§W", **VAL t / 1000 in single precision to two places, rounded**. Then each entry's text
and delayed half are added on in that order. Where the text is still empty, `NO_DRIFT` - **which it
never is**, the drift line or `DIP_ALIGNED_DRIFT` being written first. Last, the resources line.

## 8. What looks wrong and is kept

1. A row's threat, name and flag are never refreshed.
2. One direction flag serves nine sort buttons; a sort is undone by the next remaking.
3. Neighbours are the player's; "Selected" does not follow a later choice.
4. At a balance of exactly 0 the icon is red and its tooltip says "producing a surplus of 0.00".
5. Majors takes any country whose provinces come to more than 100 IC.
6. `ruler_label` is never filled.
7. `DIP_HIGHEST_THREAT_TOOLTIP` says "Their threat value to us" of how threatened the chosen country is
   by its own worst threat.
8. The relation list's first press of `sortby_nation_button` is Z to A.
9. `ALIGNINGTOWARDS` names factions; `ON_MISSION` has no text.
10. A marker's drift line is the drift before the clamp; a member's tooltip says only that it will not
    drift.
11. The relation's breakdown names five causes, at their constants; the +2000 of an overlord and a
    puppet nobody plays shows nowhere, and `RELATION_CAPPED` stands where nothing moves the pair at all.
12. Of the drift tooltip's figures the top line's is rounded and every other cut; `NO_DRIFT` cannot show.
13. The continent buttons' two position names are swapped in the file.

## Ours

- **What does not exist yet answers nothing**: there are no truces, debts, lend-lease or spy missions,
  so their status lines are never written; **the drift list is not built**, though influence and
  alignment are (`agreements.md`); there are no war goals, so the goal list is empty; the ceiling's
  claims hold for nobody, casus belli not being built. Influence, alignment, pacts, embargoes and
  access have their status lines and the breakdown its access line since 2026-10-13.
- **There is no AI**: `automation_setting` reads `OPT_NORMAL` and the arrows do nothing
  (`countryviews.md`) and `trade_automate_checkbox` does nothing. The action list holds the
  agreements' first half (`agreements.md`, section 7).
- **The panel is filled every frame**, where the original fills it on every frame on which a figure it
  watches has moved, which is nearly every frame; the status list is made again only where its lines
  have changed, which keeps its scroll as the original's putting it back does.
- **REB has no most-threatening country** (`diplomacy.md`, *Ours*), so for it the flag is blank and the
  tooltip names nobody.
- **The order of equals under the nine sort buttons** is the countries' order; the original's was not
  read.
- **Opening the screen chooses the selected province's owner only where one province is selected**,
  as read; with nothing selected the chosen country stays.

## Not established

- Who calls the screen's rebuild, and whether the list then stays empty until a button is pressed.
- Why the original refills the panel when the highest threat the player feels has moved, rather than
  the chosen country's.
- The order of the game state's faction list (the file's, inferred) and of a faction's members at a new
  game.
- The order of equals under the nine sort buttons.

## War goals and influence

*From `reversing/findings/FINDINGS-diplomacyview.md`, E.5 and E.6.* `CDiplomacyPanelLists`. The Wargoals tab shows `goals_by_us` and `goals_at_us`, each icon of the goal's type with the flag of the other country: the goals in the wars of the chosen country, by its enemies in order. Its tooltip is the goal's name and, delayed, its setup text.

`drift_list` has a row for each country with an influence and one for each with an alignment between it and the chosen - so a country may have two the same - saying `+` and the faction's `align_towards` in green where it is the chosen country's doing and the other's faction's name where it is the other way, and `--` with no faction or modifier to speak of. A row chooses its country. *Ours:* that the alignment runs the way the influence does is inferred from the modifier's being the aligner's.

## The picture under the decisions

The file lays an icon `cgm_diplo_bg` under Automate Trade and the decision list. It is a custom game's, and the original hides it in any other game [exe], so it is never drawn here, there being no custom games (`reversing/findings/FINDINGS-diplomacyview.md`).

