# The province view

The window a selected province opens at the screen's lower left: its name, flags and terrain, what the player knows of
it, its figures, its neighbours, the cores on it, its weather, its buildings with a button to build each, its
underground, the covert operations that may be run in it and the allies asked to take it. Read out of the original on
2026-10-15 (`reversing/findings/FINDINGS-provinceview.md`, sections A, B and C; the revolt-risk figure
`reversing/findings/FINDINGS-occupation.md`, section B.6). `confirmed` unless marked.

The window is `province_view` in `interface/province_interface.gui`, with the sub-windows `underground` and `cov_ops`
and the entry windows `building_member`, `adj_member`, `prov_claims_entry`, `allied_coop_item` and
`cov_ops_mission_entry`. P below is the played country.

## 1. When it is up

**The view follows the selection** - nothing opens or closes it by itself. It is up exactly while one or more provinces
are selected, and shows the province most recently added, or after a removal the first one still selected.

- **A plain left click** on the map: where a unit is being placed, the click places it and selects nothing; otherwise
  the selection is cleared and what is under the pointer is selected. A land province opens the view on itself, or
  moves it there; **clicking it again keeps it**. A unit clears the provinces, so the view closes. **A sea zone selects
  nothing and clears the selection**, so the view closes.
- **Shift and a left click**: on a selected province, it is taken out of the selection - the view moves to the first
  province left, or closes; on any other province, it is added. **Only provinces join provinces**: with units selected,
  shift-clicking a province does nothing. A sea zone clears everything.
- **No province is ever selected with a drag box**, which takes units only.
- **Escape**, with anything selected, clears the selection.
- **`close_button`** - in the view and in `underground` - clears the selection.
- **Every selected province is lit on the map**; the view shows only the last selected - an account, on
  2026-10-15, of the original's.
- A province the original does not simulate opens nothing; which those are was not read, and none is known of
  but the sea's, 2026-10-15 - which a click does not select anyway. Here every land province opens the view.

**The view does not follow the province itself**: what the refresh writes (section 2) is written when the province is
selected and stays until it is selected again, though it changes hands meanwhile. The weather, the buildings, the
underground and the covert-ops points are made again every frame (section 3).

## 2. What it shows of one province

Written as the province is selected, in this order:

| element | what it shows |
| --- | --- |
| `text_title` | the province's name; `underground`'s `text_title` too |
| `owner_flag` | the owner's flag |
| `occupier_flag` | the holder's flag, **hidden where the owner holds it** |
| `terrain` | `gfx/interface/prov_pictures/provimg_<id>.dds` where it exists, in `GFX_terrainimg_plains`' frame; otherwise the sprite `GFX_terrainimg_` + the terrain's key |
| `text_espionage` | what P knows of the province, 0 to 9, a bare number |
| `text_partisans` | the revolt risk (`revolt.md`, section 7) - **"?" below 9** |
| `text_manpower` | **"?" below 9**; "-" where the province has no manpower; else "§Y" + `(manpower + local_manpower) x max(0, 1 + local_manpower_modifier + the owner's global_manpower_modifier)`, truncated, never below nought, to two places, cut |
| `text_leadership` | **"?" below 9**; "-" where it has no leadership; else "§Y" + `(leadership + local_leadership) x max(0, 1 + local_leadership_modifier)` the same way - **no country term** |
| `text_oil`, `text_metal`, `text_energy`, `text_raremat` | "§W0" where the province's most production of the good is nought; else **"§Y?§W" below 9**; else "§R" where it produces less than its most and "§G" where not, then **the day's yield** - what it produces times the holder's technology for metal, energy and rare materials, times one and the province's and the holder's modifiers (`stockpile.md`) - to one place, **rounded** |
| `strategic_resource` | the sprite `GFX_<key>_small` of the province's strategic resource; hidden for none |
| `adj` | a row a way out of the province, **in the order of the province's edges** (`movement.md`), the sea's included: `adj_text` the neighbour's name, `adj_icon` `GFX_provinceview_strait` for a crossing by sea, `_river` for a river, `_impassable` for an impassable way, otherwise `_ocean` where the neighbour is sea and `_passable` where not |
| `claims` | a flag for each **core** on the province, in its list's order, the owner's own included |

The figures' 9 is fixed; nothing in the files moves it.

**Several provinces selected**: `text_title` is `PROV_VIEW_MULTI` "Multiple Selected($NUM$)", NUM how many; hidden are
`day_night`, both flags, `text_espionage`, `text_partisans`, `text_manpower`, `text_leadership`, the four resources'
figures, `strategic_resource`, `temperature_txt`, `wind_status`, `adj` and `claims`. **The terrain picture is the last
single province's**, and `weather_icon`, `wind_icon` and `ground_status_icon` stay up for the province added last.

**Every selection** closes the covert-operations panel and the allied-objectives panel (section 6).

**The flags**: a click on `owner_flag` opens the diplomacy screen; on `occupier_flag`, the occupier's capital is
selected - the view moves there - and the diplomacy screen opens.

## 3. Every frame

While the view is up, every frame:

**The weather strip**, from the province's weather (`movement.md`, section 5):

| element | what it shows |
| --- | --- |
| `weather_icon` | frame 7 a snowstorm, 6 snow, 5 a storm, 4 rain, 3 overcast, 2 partly cloudy, 1 clear - section 3.1 |
| `ground_status_icon` | frame 2 where any of it is frozen, else 3 where any of it is mud, else 1 |
| `day_night` | frame 2 at night (`landcombat.md`, the night), else 1 |
| `wind_status` | a bar at min(wind speed, 30) / 30, in whole percent, cut - **the 30 fixed** |
| `temperature_txt` | "§G+" above nought, "§Y" at nought, "§R" below, then the temperature to one place and "C" |

### 3.1 The weather's picture

    precipitation > GFX_SNOW_LIMIT (0.1) and temperature < -5:   wind > GFX_SNOW_STORM_LIMIT (15.0) ? snowstorm : snow
    precipitation > GFX_RAIN_LIMIT (0.1):                         wind > GFX_STORM_LIMIT (15.0) ? storm : rain
    cloud cover > GFX_CLOUD_LIMIT (0.8):                          overcast
    cloud cover > GFX_PARTIAL_CLOUD_LIMIT (0.3):                  partly cloudy
    otherwise                                                     clear

*Inferred* where the reading names the figure: cloud cover for the two cloud limits, and "above" for every limit. The
-5 is fixed.

**Also every frame**: each building row (section 4), the `underground` window (section 5), `cov_ops_points` and the
covert operations' buttons (section 6), and the allied-objectives box when its count changes.

## 4. Buildings

**The rows** are made once, the first time the view is filled, and only their province changes after. The buildings
are the file's, `common/buildings.txt`, in its order, behind an empty first place; with n the count and that first
place, `half = (n - 1) / 2`, truncated: places 0 to `half` go in `buildings_left`, the rest in `buildings_right`, and a
building gets a row **only where `show_for_province = yes`** - **the ones without still count towards the split**.
Their Finest Hour: left air base, naval base, coastal fort, land fort, anti-air; right radar station, nuclear reactor,
rocket test site, industry, infrastructure. **The rows are the same in every province**: a landlocked one shows the
naval base, disabled.

**A row** (`building_member`):

- `building_icon`'s sprite is `GFX_provinceview_` + the building's key;
- **its keyboard shortcut**: the first character of the building's name, as it is written, that no row before it - the
  left column first - has taken; the label is the name with that character in yellow, "§Y" ... "§W". Their Finest
  Hour's: A, N, C, L, n, R, u, o, I, f. Where every character is taken, none.
- **enabled** where P holds the province and may build one more there (`buildinglines.md`, section 6); with several
  provinces selected, where any selected province P holds passes that;
- **pressed** - or its key - orders one level in every selected province P holds where it may go
  (`buildinglines.md`, section 2). **A right click does nothing; a queued level cannot be cancelled here.**
- **its pips**, `plupp_0` to one fewer than `max_level`, with K what P knows of the province:

        K >= 6, or infrastructure and K >= 1:
            built   = whole levels built
            queued  = the size of P's lines of this building in this province
            pip i (from 1) beyond built + queued: hidden
            beyond built: grey (frame 3)       a level queued
            beyond the working level: orange (frame 2)   built and not working - damaged, or not yet repaired
            else: green (frame 1)
        infrastructure, K = 0, under two levels built:    every pip hidden
        otherwise:   the first pip, green, where anything is built - and hidden; the rest hidden

  A pip is green only where its whole level works: built 3 working 2.5 is two green and one orange.

**A row's tooltip** - over `building_icon`: with K below 6, the building's name and nothing else; otherwise the label,
and under it the first that applies:

1. the building is not yet available to P: `PV_NB_TECH` "You do not have the technology to construct this building.",
   then "\n (§R*) " and the name of the technology that makes it available;
2. P does not hold the province: `PV_NB_HOSTILE_TERR` "This province is not controlled by you.";
3. built and P's queued levels reach `max_level`: `PV_NB_MAX_SIZE` "This building has already reached its maximum
   size.";
4. **the province has a port and the building wants one**: `PV_BUILD_ONLY_IN_PORT` "This is only possible to be built
   in a coastal province.";

and then the shortcut line where that half is otherwise empty. Over a pip: `PV_BUILD_NO_YET` "Not yet built in this
province." where nothing is built; `PV_BUILD_W_DESC` "This building is built up to level §Y$MAX$§W." where all of it
works; otherwise `PV_BUILD_DESC` "This building has been damaged and is currently at §Y$CURR$§W level. It is built up to
level §Y$MAX$§W, but will take time to repair. Further combat may reduce it even further." - CURR the working level to
three places and MAX the built to none, rounded - and then, where **the holder** has a line of it in the province,
"\n" + `PV_CURR_CONS_MORE` "Currently constructing more.". **No tooltip gives a cost, a time or what the building does.**

## 5. The underground

**The `underground` window is up** for the province the view shows where it has an underground and either P's or one
P could place (`undergrounds.md`, section 2) - with several selected, the first selected province that passes - and
hidden otherwise. For that province:

- `underground_status` a bar at the strength in whole percent, rounded half to even;
- `text_supression` "§Y" + `UNDERGROUND_DETECT_CHANCE` x the province's suppression, to no places, + "%§W" - with the
  define at 2, **twice the suppression**;
- `underground_mission`'s frame the action + 1 (3 with no underground), **enabled only where the underground is P's**;
  **a left click steps it 1, 2, 3, 1** - partisans, spread, build up - **and a right click back**, and each posts the
  new action, for every selected province with an underground of P's;
- `spawn_partisan` enabled where it may raise partisans (`undergrounds.md`, section 3), `spawn_underground` where it may
  spread; each posts its command at once, for every selected province where it may.

**Tooltips**, of the view's province: `underground_status` `UNDERGROUND_STATUS` "Underground strength:
§Y$CURRENT$§W/§Y$MAX$§W" to one place; `spawn_partisan` `UNDERGROUND_OWNER_DESC` "Underground owned by §Y$COUNTRY$§!"
where it is not P's, else `SPAWN_PARTISAN` "Spawn partisan unit" with, under it, `MOBILIZE_PARTISANS` "Mobilize nearby
partisans" at `SPAWN_PARTISAN_LIMIT` or `UNDERGROUND_NOT_STRONG_ENOUGH` "Not strong enough. §Y$LIMIT$§W is needed and is
currently at §Y$CURRENT$§W"; `spawn_underground` the same with `SPREAD_UNDERGROUND` "Expand underground resistance
network" and `MOBILIZE_UNDERGROUND` "Organize a nearby underground base" against `SPAWN_UNDERGROUND_LIMIT`;
`underground_mission` `UNDERGROUND_OWNER_DESC` where it is somebody else's, else by the action
`UNDERGROUND_ACTION_PARTISAN` "Spawn partisan unit as soon as we can", `_SPREAD` "Spread underground network as soon as
we can", `_NONE` "Do nothing and build up strength"; `supression_icon` `SUPRESSION` "Suppression". **`text_supression`
says nothing.**

## 6. Covert operations and allied objectives

**`cov_ops_points`**, always up: P's covert-ops points in the province's holder (`espionage.md`, section 11), whole.

**`open_cov_ops`** opens the `cov_ops` panel, or closes it, and closes the allied-objectives panel. The panel lists
`common/covert_ops.txt`'s operations in its order, those whose `is_valid` holds with P as FROM in the province - made
as it opens, so an operation that comes to hold appears only when it is opened again. A row's `selectbutton` says the
operation's name, is enabled where `is_valid` holds and P has its cost in points and `is_selectable` holds, and pressed
sends it (`espionage.md`, section 11); the panel stays up. Its tooltip, over the whole row: `<key>_desc`, and under it
the cost - `COV_OPS_COST` "Covert ops cost: §Y" + cost + "§!" and a newline, or where P has fewer points
`COV_OPS_COST_LACKING` "Costs §R$COST$§! covert ops points. We only have §Y$CURRENT$§! " with no newline - then, while the
button is disabled, `is_selectable`'s account of itself, then `COV_OPS_EXECUTE` "\n\nEffect:\n" and the effect's.

**`set_cooperation`** opens the allied-objectives panel, or closes it, and closes `cov_ops`. It lists P's allies - its
alliances and its faction's members - taking part, **leaving out one in another faction than P's**, each with a flag,
its name and the box `objective`, ticked where P has asked it to take this province. Ticking it asks; clearing it takes
the asking back - the command `set_allied_objective` (ally, P, province, on or off). The rows are made as the panel
opens.

**The `allied_cooperation` box** shows the flags of the allies P has asked to take this province, in the order asked.

**An allied objective** is kept by both: the asked country holds the province and who asked, the asker the province and
whom. Leaving a faction clears every objective either way. **Nothing reads it here**: the original's AI takes it as a
nudge (`reversing/findings/FINDINGS-aifields.md`, section 1), and the AI is not built.

## 7. The other tooltips

| element | main | under it |
| --- | --- | --- |
| `terrain` | the terrain's name | its movement cost, and its attack, defence and attrition where not nought (`tooltips.md`, section 7) |
| `oil`, `metal`, `energy`, `rare` | `CRUDE_OIL_IRO`, `METAL_IRO`, `ENERGY_IRO`, `RAREMAT_IRO` | |
| a resource's figure | below | |
| `strategic_resource` | the resource's name | each of its modifier's values on a line |
| `weather_icon` | `WEATHER_SNOW_STORM` "Blizzard", `WEATHER_SNOW` "Snow", `WEATHER_STORM` "Storm", `WEATHER_RAIN` "Rain", `WEATHER_CLOUD` "Cloudy", `WEATHER_PARTIAL_CLOUD` "Partial Clouds", `WEATHER_CLEAR_SKY` "Clear Sky" | |
| `ground_status_icon` | `WEATHER_FROZEN` "Frozen Ground", `WEATHER_MUDDY` "Muddy Ground", `WEATHER_DRY` "Dry Ground" | |
| `wind_icon`, `wind_status` | `WEATHER_WIND` "There are winds at $SPEED$km speed here.", SPEED the wind to one place | |
| `temperature_txt` | `WEATHER_TEMPERATURE` "Temperature" | |
| `owner_flag` | `PV_OWNER` "Province owned by $NAME$" | |
| `occupier_flag` | `PV_OCC` "Province occupied by $NAME$" | |
| a `flag` of `claims` or `allied_cooperation` | `PV_HAS_CLAIM` "$NAME$ has a claim on this province." | |
| `espionage`, `text_espionage` | `INTEL_NONE` "No intel (0)", `INTEL_STRUCTURES` "Basic Intel (1)", `INTEL_UNITS` ... (2), `INTEL_2` "Intel lvl 3" ... `INTEL_7` "Intel lvl 8", `MAX_INTEL_LEVEL` "Full Intel (9)" | |
| `icon_manpower`, `text_manpower` | `PV_MANPOWER` "Manpower" | |
| `icon_leadership`, `text_leadership` | `PV_LEADERSHIP` "Leadership" | |
| `partisans_icon` | `PV_RR` "Revolt Risk" | |
| `text_partisans` | the revolt risk's account (`revolt.md`, section 7) | |
| an `adj` row, a plain way | `PV_PASSABLE` "Normal" | `PV_PASSABLE_DESC` |
| the same, to the sea | `PV_OCEAN` "Ocean" | `PV_OCEAN_DESC` |
| the same, a river | `PV_RIVER` "River Crossing" | `PV_RIVER_DESC` |
| the same, by sea | `PV_STRAIT` "Strait" | `PV_STRAIT_DESC` |
| the same, impassable | nothing | |

**A resource's figure's tooltip**, at any intelligence: `BASE` "Base:" + " §Y" + what the province produces to two
places + "§W\n"; for metal, energy and rare materials where the holder's technology adds to the good, `FROM_TECHNOLOGY`
"From Improved Technology" + ": §G+" + that x 100 to two places + "%§W\n"; the sources of the good's `local_` modifier
and then of `local_resources` in the province; "\n"; the sources of the good's `global_` modifier and then of
`global_resources` in the holder.

**No tooltip**: `text_title`, `close_button`, `day_night`, the backgrounds and overlays, `covert_ops_icon`,
`open_cov_ops`, `cov_ops_points`, `set_cooperation`, `coop_label`, `cov_ops_label`.

## What looks wrong and is built so

- The view is written when the province is selected and not again: a province that changes hands keeps its old name,
  flags, figures, ways and cores until it is selected anew, and its covert-ops points are against the old holder.
- With several selected, the terrain picture is an earlier province's and three weather icons stay up.
- The figures need full intelligence; their tooltips need none. A province producing nothing reads "§W0" whatever is
  known.
- The resources are rounded, everything else cut; the colour weighs what is produced against the most, and the number
  is the day's yield.
- The claims box shows cores, the owner's among them, under "has a claim".
- The intelligence keys are one out: `INTEL_2` is "Intel lvl 3".
- A building row's tooltip says "only in a coastal province" where the province **has** a port.
- The rows are the same everywhere; a right click on one does nothing; their shortcut letters are matched with case.
- The pips count P's lines, their tooltip the holder's.
- `text_supression` is twice the suppression and says nothing over it.
- The underground window's figures are of the first selected province with one, its title and tooltips of the view's.
- The covert operations are listed as the panel opens; pressing one checks nothing and leaves it up; a disabled row
  shows `is_selectable` whatever disabled it, run on after the lacking line.
- The allied-objectives panel leaves out allies in another faction, and its ticks are read as it opens.

## Ours

- ~~**Weather is not built**: every province's weather is nought, so the strip shows a clear sky.~~ Built 2026-10-15
  (`weather.md`): the strip shows the province's weather, and its picture is chosen as the map's is.
- **A lowercase shortcut letter is the key of its capital** (*not established*: section 4's 'n', 'u', 'o', 'f').
- **The custom game's parts are not built** - `cgm_underground_build`, the right click and Ctrl on a building - there
  being one start (`CLAUDE.md`, *Settled decisions*).
- **The diplomacy screen a flag opens** chooses the province's owner as it opens for any selected province
  (`diplomacyscreen.md`); which country the original's opens on was not read.
- **A flag in the allied-objectives box or panel says "has a claim"**, as a claims entry's does; whether the
  original's rows reach that tooltip was not settled.
- **Several provinces are selected only with Shift on provinces**; the original's selection holds one kind, and ours
  holds units apart from provinces as before.
- **The province readout under the top bar**, scaffolding with the supply and the country's figures, is put away
  behind a switch of **the debug menu, opened with Shift and 4** - chosen on 2026-10-15, beside the
  original's console on Shift and 3.

## Not built

- What an asked ally is shown of an allied objective - the outliner's row, alert 15, the map's marker - and what its AI
  makes of it.
- The province pictures, `provimg_<id>.dds`: neither install has one; the path is asked and passed over.

## Not established

- That the glue's first handler is a left press (the close button and the flags).
- What a click picks where a counter and a province overlap (`mapunits.md` has ours).
- How a disabled `building_icon` is drawn, its sprite having one frame.
- That a pip hidden for an earlier province stays hidden below intelligence 6.
- Which figure each weather limit is compared with (section 3.1).
- Which provinces the original does not simulate, and so opens no view on (its template's `simulated` byte) - none
  known of but the sea's.
- What `%.0f` makes of an exact half - the suppression figure and a pip's tooltip are rounded half away from nought here.