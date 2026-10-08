# The top bar: what goes in each element

What the original's top bar writes into each of its elements, and when, written as rules
this project can implement. No addresses and no offsets. Each rule says how sure it is -
`confirmed`, `likely`, `inferred` in the fact base's vocabulary - or **measured**. Where
this project chooses its own behaviour the paragraph is marked **Ours**.

Source: `reversing/findings/FINDINGS-topbar.md`, read for this. The strings quoted as the
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

The other icons - the pictures beside the figures, the background - are found by nothing
and are there to be looked at.

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
          + income from debt + made by converting + tribute received

    red     if out > in + traded for
    green   else if out < in
    yellow  otherwise

**Red is a loss even with what is bought, yellow a loss that buying covers, green a gain
without it.** `tribute sent` is held as a negative, which is why it is taken away.

**A government in exile shows `-` for fuel and for supplies.**

The original's: `§G3804`, `§G1906`, `§G951`, `§R1870`, `§G462`, `§G391`, `§G591`.

*Ours:* of the thirteen ledgers only four exist yet - produced at home, used, and the two
sides of converting - so the colour is those four. With no trade there is no `traded for`,
and a country that takes exactly what it makes is yellow. There are no governments in
exile.

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

*Ours:* two of the six shares spend nothing until there is a build queue and lend-lease,
and reinforcement and upgrades spend only what their units take (`production.md`,
`upkeep.md`), so much of a country's IC shows as wasted, in red. That is true, and it is
the bar saying so.
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

*Ours:* the ratio is not worked out, so the box shows `-` as the original does for a
figure that does not apply. The units and the officers they bring a country exist since
2026-10-08 (`units.md`, section 7); what is missing is how the original turns the two into
the ratio, and where it caps it - a savegame has Germany behaving as though at 140% on a
history that gives it half as many officers again as it needs. The format and the colours
are built and tested for when there is one.

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

## 14. Ours

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
5. **The skin.** The bar's background is the sprite `GFX_topbar_bg`, whose texture is
   `skins/topbar_generic`; the same folder holds a `country_bg` for each of many tags, and
   the original looks `GFX_topbar_bg` up by name from its code. Whether it swaps the bar's
   picture by country was not read. The country screens do swap theirs, from the same
   folder (`countryviews.md`, section 2), which makes it likely.
6. **Tooltips.** The top bar's is a 41-key builder that was found and not read
   (`reversing/findings/FINDINGS-uinumbers.md`, section 7); none is shown here.
7. **Where the flag is drawn.** The file puts `player_flag` at -2, -1, over the left end
   of the bar's art, and that is where it is. Whether the original clips or shades it
   differently is a matter of looking at the two side by side.
