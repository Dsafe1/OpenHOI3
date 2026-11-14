# The top bar: what goes in each element

What the original's top bar writes into each of its elements, and when, written as rules
this project can implement. No addresses and no offsets. Each rule says how sure it is -
`confirmed`, `likely`, `inferred` in the fact base's vocabulary - or **measured**. Where
this project chooses its own behaviour the paragraph is marked **Ours**.

Source: `reversing/findings/FINDINGS-topbar.md`, read for this, and for the tooltips
`reversing/findings/FINDINGS-topbartooltip.md`. The strings quoted as the
original's own are from `reversing/findings/FINDINGS-guilive.md`, section 6: what its top
bar held in a running game, playing Ireland on 1 January 1936. Every format below
reproduces the one of those it is for, and the tests hold them.

How an interface file becomes something on screen is `interface.md`. Colours are written
here as `§R`, `§G` and `§Y`: the escape for red, green and yellow.

## 1. The window

`confirmed` The bar is the window `topbar` of `interface/topbar.gui`, which is the whole
screen (`fullScreen = yes`); its art is 1024 wide and sits in the upper left. The code
finds these by name:

| kind | names |
| --- | --- |
| text boxes | `energy_number`, `metal_number`, `raremat_number`, `oil_number`, `fuel_number`, `supplies_number`, `money_number`, `ic_number`, `DateText`, `dissent_number`, `national_unity_number`, `diplomacy_number`, `manpower_number`, `leadership_number`, `espionage_number`, `CountryName` |
| icon | `player_flag` |
| buttons | `topbarbutton_diplo`, `topbarbutton_prod`, `topbarbutton_tech`, `topbarbutton_politics`, `topbarbutton_intelligence`, `topbarbutton_theatre`, `topbarbutton_statistics`, `swm_open`, `topbar_menubutton`, `topbar_outlinerbutton`, `button_speedup`, `button_speeddown`, `speed_indicator`, `pause_bg` |

The other icons - the pictures beside the figures - are found by nothing and are there to
be looked at.

**The bar's picture is the country's own where the install has one.** `confirmed` (read
2026-11-14, `reversing/findings/FINDINGS-topbar.md`, *The skin*): as the game is entered, once,
the file `gfx/interface/skins/topbar_` + the played country's tag + `.dds` is looked for, and
**only where it is there** the icon `topbar_bg` is given it, drawn through the sprite
`GFX_topbar_bg` as before; otherwise the icon keeps what that sprite names, `topbar_generic`. By
the tag alone - not the faction, not the government. Their Finest Hour has 46 such files, `GER`,
`ENG`, `SOV` and `USA` among them, Siam among the minors; Ireland has none and keeps the generic
picture. The same step gives the country screens theirs (`countryviews.md`, section 2).

- **Nothing sets it again**: the step's one caller is the entering of the game, so a played
  country changed in play would keep the first one's picture - listed in `docs/enhanced.md`.
  *Ours:* the console's `tag` makes every screen again (`console.md`), so here the bar follows.
- **The original does not ask whether the window has `topbar_bg`**: a mod with the file and no
  such icon would fail. *Ours:* the icon is asked for only where the file is there, as the
  original asks, and one the window lacks is named in the log.

Built 2026-11-14 (`CTopBarText.SkinFile`, `CTopBar`) and looked at with `--country=GER`, `ENG`,
`SOV`, `USA`, `SIA` and `IRE`: the five with a file each drawn in its own, Ireland's the generic
green, the main checkout's the generic green for all.

**Every figure is set again on every pass**, and a box set to what it already shows does
nothing. The date is the exception: it is worked out only when the hour has changed. The
flag and the name are set only when the country played changes.

## 2. The seven resources

`confirmed` throughout. Each is a colour and the country's stockpile.

**The number** is what the country holds, with no decimals, cut off and not rounded.
**Above 99999 it is shown in thousands with a `K`**: divided by a thousand, then cut. So
99999.000 is `99999`, 99999.001 is `99K` and 123456.789 is `123K`.

**The colour** is from the day's ledger for that good:

    out = sent to units - repaid away - tribute sent + used + used in converting
          + convoyed out + traded away
    in  = produced at home + convoyed in (not for supplies or fuel) + sent back by units
          + handed over by governments in exile + income from debt + made by converting
          + tribute received

    red     if out > in + traded for
    green   else if out < in
    yellow  otherwise

**Red is a loss even with what is bought, yellow a loss that buying covers, green a gain
without it.** `tribute sent` is held as a negative, which is why it is taken away.

**A government in exile shows `-` for fuel and for supplies.**

The original's: `§G3804`, `§G1906`, `§G951`, `§R1870`, `§G462`, `§G391`, `§G591`.

**`repaid away` is held above nought and taken away all the same** (`reversing/findings/FINDINGS-tradeaid.md`
adds the day's repayment to it as it is), so money repaid on a debt makes the colour greener, not redder - the
original's, kept and listed in `docs/enhanced.md`. A debt is repaid in money alone.

**Handed over by governments in exile** is the pool the reading of the bar left unnamed: each day a government in
exile hands its supplies and fuel to whoever controls its acting capital, and they are counted here for that country
(`war.md`, section 14; `reversing/findings/FINDINGS-surrender.md`, C.5). So only supplies and fuel ever have any of it.
`confirmed`

Every ledger is there since 2026-11-14 - trade (`trade.md`), debt (`aid.md`) and tribute (`stockpile.md`, section 7)
since 2026-11-13, what exiles hand over since 2026-11-14 - and the same sum is the Lua's daily balance and income
(`lua.md`, section 4.2).

## 3. IC

`confirmed` Three whole numbers and two colours:

    {§R if wasted > 0, else §G} wasted / {§R if before the cap > used, else §G} used / before the cap

- **wasted** is the IC the country has given a share that the share does not need: what
  each of the five shares but lend-lease was given beyond what it spent, added up, and
  the fraction dropped.
- **used** is the IC the country has to spend today (`ic.md`).
- **before the cap** is what it would have with resources enough.

So the first number is red whenever anything is going to waste, and the second whenever a
shortage has cost IC. The original's: `§R10/§G21/21`.

*Ours:* lend-lease spends nothing, and production, reinforcement and upgrades spend only
what their queue and their units take (`production.md`, `buildqueue.md`, `upkeep.md`), so
much of a country's IC shows as wasted, in red, until its sliders are set to what is
needed. That is true, and it is the bar saying so.
How the original rounds each share's spare IC before adding them is not read; here the
five are added in thousandths and cut once.

## 4. The date

`likely` The hour, `:00`, the day, a comma, the month as a word, the year:
`0:00 1, January 1936`. The original hands a formatter the pattern `h :00 w d , w mw w y`;
the formatter was not read, and this is a fit on the one string. The month's name is the
localisation of its English name.

## 5. Dissent

`confirmed` The number is the country's dissent, **to one decimal above 1.000 and to two
at or below it**. The colour is which way a day would move it with the consumer goods
share where it stands (`production.md`, section 3): **red if it would rise, green if it
would fall, yellow if neither.** The original's: `§G0.00`.

## 6. National unity

`confirmed` The number is national unity with no decimals and a `%`. The colour is the
sign of what is pushing it:

    the strategic bombing impact + the allies' impact + the convoy impact
        + the country's national unity modifier

green above nothing, yellow at nothing, red below. The original's: `§Y80%`.

*Ours:* national unity is what the country's history gives it, 1 where it gives none
(**measured:** 101 of 101 against the savegame, to the nearest whole number), and nothing
moves it. The three impacts are strategic warfare and do not exist; the modifier is
whatever the country's laws and ministers give under `national_unity`, which is the key
that fits the original's name for it - `inferred`.

**Measured, and not built:** in the savegames unity is already a thousandth or two below
the history's figure after two days, and goes on moving by about that much a day -
Germany 90 to 89.998, Denmark 65 up to 65.106 by 20 April. `spy_lower_national_unity` is
-0.1 and `spy_raise_national_unity` 0.15 in the static modifiers, so this is spies at
work, and a day's change looks like a hundredth of the modifier. Nothing here has spies.

## 7. Diplomatic influence, and its lost colour

`confirmed` The number is the whole part of the country's diplomatic influence, and
**nothing else**. The original works out a colour for it - green, yellow or red on
whether the day's influence from leadership is more than its upkeep - and then appends
that colour to the text national unity has just used, and sets the box from another. The
colour never reaches the screen; the original's string is a bare `9`. That it is a slip
and not a decision is inference; either way a faithful bar has no colour here, and this
one has none.

A switch somewhere in the original makes every country's influence read 100. What sets it
is not known, and nothing here has it.

## 8. Manpower

`confirmed` The country's manpower with no decimals and no colour. The original's: `115`.

## 9. The box named for leadership shows the officer ratio

`confirmed` `leadership_number`, beside the icon `GFX_icon_leadership`, is **the fraction
of the officers its units want that the country has**, as a whole percentage:

    red     under 50%
    yellow  under 90%
    green   otherwise

The original's: `§G108%`.

The ratio is the officers the country has over what its brigades, ships and wings call for,
never above the define `MAX_OFFICERS`, and worked out again every hour: `organisation.md`,
section 1. A country with no units has all it wants and shows `§G100%`. `confirmed`

Built 2026-10-08. Germany, whose history gives it half as many officers again as it needs,
shows `§G140%` here - the ratio its brigades in the original's start save are held with
(`organisation.md`, section 7). The original's own first screen was not looked at for it.

## 10. Spies

`confirmed` The whole spies the country has trained and not yet sent anywhere, with no
colour. **More than 99 is written `99+`.** The original's: `0`.

## 11. The flag and the name

`confirmed` as to when: only when the country played changes. `player_flag` is a masked
shield (`interface.md`, section 5) and is handed the country; `CountryName` is the
country's name. What the original does to make either was not read.

## 12. Time

`confirmed` except where marked.

- **`speed_indicator`'s frame is 1 while the game is paused, and the speed plus 2 while it
  is not** - so with five speeds, frames 2 to 6 of its six.
- **`pause_bg`, the bar behind the date, is frame 1 while paused and 2 while not**, with
  the tooltip `PAUSE_RESUME` or `FE_PAUSED_TEXT`.
- **`speed_indicator` and `pause_bg` share one handler**, which is why clicking either
  stops and starts time. `button_speedup` and `button_speeddown` have one each. What the
  handlers do was not read; that they are pause, faster and slower is `inferred` from the
  names and from playing the original.
- The two speed buttons are disabled under a condition that was not identified - the
  shape of it is "this player may not set the speed", as a multiplayer client may not.

*Ours:* the three are wired as pause, one speed faster and one slower, and the keys that
did the same before the bar existed still do: Space or Pause, plus and minus.

## 13. The other buttons

`confirmed` Diplomacy, production, technology, politics, intelligence, theatres,
statistics, strategic warfare, the menu and the outliner each have a handler; the first
five are not attached while a tutorial runs, and seven are disabled in a scenario.

*Ours:* `topbarbutton_prod` and `topbarbutton_tech` bring the production and technology
screens up and take them down (`countryviews.md`, section 4). The other screens do not
exist, so their buttons can be pressed and do nothing. There are no tutorials and no
scenarios.

## 14. Tooltips

Read 2026-10-11 (`reversing/findings/FINDINGS-topbartooltip.md`). The bar answers by the
element's name; how the box is shown is `tooltips.md`. `confirmed` throughout unless marked.
`VAL`-style figures are written as the original writes them: a colour, the number, `§W`;
`§G+` is green with a plus. "The sources of a kind" is `modifiers.md`, section 8.

| element | text | delayed |
| --- | --- | --- |
| each resource's picture: `icon_energy_small`, `icon_metal_small`, `icon_raremat_small`, `icon_oil_small`, `icon_fuel_small`, `icon_supplies_small`, `icon_money_small`, and `icon_ic_small` | `ENERGY_IRO`, `METAL_IRO`, `RAREMAT_IRO`, `CRUDE_OIL_IRO`, `FUEL_IRO`, `SUPPLIES_IRO`, `MONEY_IRO`, `IC_IRO` | the same with `_DRO` |
| `icon_dissent`, `icon_diplomacy`, `icon_national_unity`, `icon_espionage`, `icon_leadership`, `icon_manpower` | `DISSENT_IRO`, `DIPLOINF_IRO`, `NATIONAL_UNITY_IRO`, `ESPIONAGE_IRO`, `OFFICERS_IRO`, `MANPOWER_IRO` | the same with `_DRO` |
| each resource's number | its ledger, below; **nothing for fuel and supplies of a government in exile** | |
| `ic_number`, `dissent_number`, `diplomacy_number`, `national_unity_number`, `leadership_number` | below | |
| `manpower_number` | below | below |
| `espionage_number` | `FREE_SPIES` alone | |
| the seven `topbarbutton_*` | `DISABLED_IN_SCENARIO`, when disabled in a scenario | |

Everything else on the bar - the date, the name, the flag, the speed buttons, `pause_bg`, the
menu, outliner and strategic warfare buttons - has no tooltip but its shortcut line. While the
strategic warfare window is open and over it, that window answers instead.

**A resource's ledger.** First

    change = what came in - what went out      (section 2's two sums, with traded for)
    RES_CHANGE with VAL = (change < 0 ? §R : §G+) + change to two places + §W

then one line for each of these that is not nothing, in this order, each its key with `VAL`:
`RES_PROD` (made at home, `§G+`), `RES_CONVOYED_IN` (not supplies or fuel, `§G+`),
`RES_TRADED_FOR`, `RES_INC_DEBT`, `RES_FROM_PUPPETS` (each `§G+`), `RES_CONVERTED` twice - first
what converting used, negated, `§R`; then what it made, `§G+` - `RES_TRADED_AWAY`,
`RES_REPAID_AWAY`, `RES_CONVOYED_OUT` (each `§R` and negated), `RES_SENT_TO_MASTER` (**`§G+` and
negated, green though it is a loss**), `RES_INTO_NET` and `RES_USED` (`§R`, negated),
`RES_SHIPPED_BACK` (`§G+`). **What governments in exile hand over is in the change and has no line of
its own.** Each key ends in its own newline. Last, a newline and the sources
of the good's global kind - `global_energy`, `global_metal`, `global_rare_materials`,
`global_crude_oil`, `global_fuel`, `global_supplies`, `global_money`.

*Ours:* the ledgers section 2 has, all of its lines since 2026-11-13, debt's and tribute's among them;
`RES_INTO_NET` is what left the stockpile for units and what was made away from the capital, the two pools this
project keeps of what the original keeps as one.

**IC.** `IC_NUMBER_IRO` with `WASTED`, `BASE` and `AVAILABLE` - section 3's three figures, whole;
where technology adds to IC, `FROM_TECHNOLOGY` + `": §G+"` + it x 100 to two places + `"%§W\n"`;
the sources of `global_ic`; and where the country is lent IC, a line of what it gets and one per
lender. *Ours:* there is no lend-lease.

**Dissent.** `DISSENTV_IRO` with `CHANGE` - `§R` rising, `§Y` still, `§G` falling, and the day's
change **rounded** to two places, where every other figure here is cut; then, where they say
anything, `"\n"` + `DISSENTV_IRO2` + the sources of `dissent`; and where the country has any
dissent, `"\n"` + `DISSENTV_IRO3` + every value of the static `dissent` times its dissent, each
under the kind's own name (`modifiers.md`, section 8).

**Diplomacy.** `DIPDETAILS_IRO` with

    PRODUCED = the diplomacy share in thousandths, cut, x leadership / 1000
               x LEADERSHIP_TO_DIPLOMACY / 1000                      to two places
    UPKEEP   = what influencing others costs a day                    to two places
    DAILY    = (PRODUCED - UPKEEP < 0 ? §R : = 0 ? §Y : §G+) + it     to two places

*Ours:* there is no diplomacy, so the upkeep is nothing.

**National unity.** `§Y` + unity to three places + `"%§W\n"`; then a list - for each of the
three strategic warfare impacts, convoys, allies and bombing, that is not nothing, `STW_CONV`,
`STW_ALLIES` or `STW_BOMB` + (`§G+` above nothing, else `§R`) + it to three places + `"§W\n"`,
then the sources of `national_unity` - and where the list is under three characters
`NO_CURR_EFFECTS_ON_NU`, else `STRMONTHLY_CHANGE` + `"\n"` + the list; last, where they say
more than two characters, `"\n"` + `EFFECT_SIZE_MOD_NU` + `"\n"` + the sources of
`national_unity_effect`. *Ours:* none of the three impacts exists.

**Officers**, `leadership_number`. `OFFICER_NUMBER2_IRO` with `OFFICERS`, `DIVISIONS` (brigades,
ships and wings) and `NEED`, whole, and

    DAILY = the officer share in thousandths, cut, x leadership / 1000 x LEADERSHIP_TO_OFFICERS
            / 1000 x (1000 + officer_recruitment) / 1000                   to one place

then, where they say more than two characters, `"\n"` + the sources of `officer_recruitment`.
**The day's officers here are worked in thousandths, cut at each step, where the day's pass
works them in the other kind of number** (`numbers.md`); the two can differ by a thousandth.

**Manpower.** Text: `MANPOWER_DETAILS_IRO` with `NEED`, what bringing every brigade back to
strength would take, and `USED`, what reinforcing took today - to two places where the need is
under 10, to one at or above. Delayed: `MANPOWER_DETAILS_DRO` with `MONTHLY`, a month's gain to
one place, and

    BASE = (most manpower / 2) / (global_manpower_modifier + 1 + technology's manpower gain)

worked in single-precision floating point and written to three places, rounded; then the
sources of `global_manpower`; then, where technology adds to manpower, a newline where anything
came before and `FROM_TECHNOLOGY` + `": §G+"` + it x 100 to two places + `"%§W\n"`; then the
sources of `global_manpower_modifier`, with no newline between them and what came before.

**Spies.** `FREE_SPIES`, which says "Free Spies", and nothing else.

**What is shown of a figure that is not built** is what the original shows of a zero: a line
skipped, or `§Y0.00`.

*Ours:* a table's `\n` is a line break wherever localisation is read, which `RES_CHANGE` and its
fellows need; where the original turns it into one was not read.

Built 2026-10-11 (`CTopBarTooltips`, `CModifierText`) and tried with `--click`: the energy ledger,
IC with a minister's and a law's lines, and manpower's delayed half.

**Kept as read, though they look odd:**

1. **Tribute sent to a master shows green, with a plus**, though it is a loss; and **what exiles hand
   over is counted in the change with no line saying so** (`docs/enhanced.md`).
2. **Dissent's change is rounded** where every other figure is cut.
3. **Manpower's delayed half has a blank line before the technology line** wherever a source
   after the last flat-manpower one has nothing of it - the line break that source leaves, and
   the one the technology line asks for - and **none at all between the two lists** when there
   is no technology line.

## 15. Ours

- **The country played is Germany** unless the game is started with `--country=TAG`.
  There is no screen to choose one on.
- **What each element says is worked out in `OpenHOI3.Interface`**, with no engine in it,
  and tested against the original's own strings. The client finds the elements and sets
  them.
- **An element the window does not have is skipped and named in the log.** A mod that
  renames `ic_number` loses its IC figure, as it would in the original.

## What is not established, in one place

1. **What the thirteen button handlers do.**
2. **The date formatter**, and so whether the pattern can produce anything this reading
   of it does not.
3. **How spare IC is rounded** share by share.
4. **What moves national unity each day.** The savegames show it moving and fit spies; the
   routine has not been read.
5. ~~**The skin.**~~ Read 2026-11-14: it follows the country's tag (section 1).
6. **`pause_bg`'s own tooltip.** The bar gives the button `PAUSE_RESUME` or `FE_PAUSED_TEXT`
   (section 12), and nothing read asks a button for it, so whether it is ever shown is not
   known. Here it is not.
7. **Where the flag is drawn.** The file puts `player_flag` at -2, -1, over the left end
   of the bar's art, and that is where it is. Whether the original clips or shades it
   differently is a matter of looking at the two side by side.
