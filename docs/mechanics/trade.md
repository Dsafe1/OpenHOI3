# Trade: routes, their price, their day and the AI's trading

The agreements' second half begins here (`agreements.md` is the first: the offer, the AI's answer, the
pacts). A **trade route** is an agreement that one country hands another goods each day and is paid
for them in money. It is made by an offer of the kind `trade_action`, through the machinery of
`agreements.md`, sections 1 to 3, with what differs said here. Debt, licences, lend-lease and shared
technology are `aid.md`.

Source: `reversing/findings/FINDINGS-tradeaid.md` (sections A and B, read 2026-10-13), with
`reversing/findings/FINDINGS-trade2.md`, `reversing/findings/FINDINGS-convoys.md` (*Trade routes*) and
`reversing/findings/FINDINGS-supplyconvoys.md`. Figures are thousandths. The goods are in their pool's
order - supplies, fuel, money, crude oil, metal, energy, rare materials - and money is the price of the
other six. The defines are `common/defines.lua`'s: the diplomacy block, `MAX_DAILY_TRADE` in the
economy block, and the block `goods_cost`.

## 1. A route

A route has a **`from`** - the country that proposed it - and a **`to`**, and two blocks of seven
goods (`confirmed`):

- **`trade_from`**, what `from` hands `to` each day;
- **`trade_to`**, what `to` hands `from` each day.

**One block holds goods and the other money**, the money being the price of the goods (all 582 routes
in four reference saves are so). A route has a start date, and day by day whether it is
**inactive** and why (section 6); and a **convoy**, where the two need one (section 5).

**Setting an amount** (`confirmed`), for an offer being made - by the dialog's sliders or by a script:

- a positive amount of a good puts it in `trade_to`: **the actor buys** it;
- a negative amount puts its size in `trade_from`: **the actor sells** it;
- nought clears the good from both blocks; and a positive amount in one block clears that good from
  the other, so one good never flows both ways;
- then, **unless both countries have the rule `free_resource_gifts`** (section 8), both money entries
  are worked out again from nothing:

      trade_to[money]   = the sum over the goods of price(good, trade_from[good])
      trade_from[money] = the sum over the goods of price(good, trade_to[good])

**The price** of an amount a of a good, between `from` and `to` (`confirmed`, and it reproduces both
money entries of all 582 saved routes):

    f     = (200000 + to's relation towards from) * 1000 / 400000           0 to 1000
    d     = f * 500 / 1000
    price = (goods_cost[good] * a / 1000) * (1000 - d) / 1000               each division cut

**A good costs its full `goods_cost` at relation -200, three quarters at nought and half at +200.**
Nothing else enters. Their Finest Hour's costs: supplies 0.25, fuel 0.75, crude oil 0.75, metal 0.1,
energy 0.05, rare materials 0.2, money nothing. **The price is fixed when the amounts are last set**:
the +15 of accepting and every later change of relation leave it as it was.

**A route is good** (`confirmed`) where both countries have `free_resource_gifts`; otherwise only where
**it is no barter** - not some good of each block above one thousandth - **it has a price** - one
block's money at least one thousandth - and its convoy, if it has one, starts and ends in land one of
the two holds.

## 2. The offer

**Valid** (`confirmed`, `FINDINGS-trade2.md` section 4): both countries own a province; then, to make
one, no war between them, and **each a puppet only of the other or of nobody** - a puppet may trade with
its own master and nobody else; to cancel one, a route of the pair matching it in both countries and
both blocks exactly.

**Selectable** (`confirmed`): the two are different countries; to cancel, always; to make one, no war
between them, the actor no puppet, the recipient no puppet or the actor's own, influence at least
`TRADE_INFLUENCE_COST`, **where they need a convoy (section 5) a harbour each** - a province of its own
with a port and a naval base - and no embargo between them.

**Answered** (`confirmed`): an offer to trade, yes; a cancellation, no - **a cancellation takes effect
when it is made**. A diplomat on his way does not grey a cancellation.

**The AI's answer** is `agreements.md`'s, with the score `DiploScore_OfferTrade` (section 9), and two
differences (`confirmed`): **before the Lua is asked** the score is 100 where both sides are one
country, and 0 where the offer is not valid, where it would need a convoy that cannot be made, or where
the recipient's AI is already trading the same good on an inactive route of the pair (more than one
thousandth of it in the same block); and **a country accepts at most one trade an hour** - a further
trade it would accept stays on its list, still a proposal, for the next hour's draw, and only the rest
of the list is emptied.

## 3. What an offer does

`confirmed`. Their Finest Hour's figures: `TRADE_INFLUENCE_COST` 3, `TRADE_RELATION_CHANGES` 15,
`TRADE_CANCEL_RELATION_COST` 15, `TRADE_CANCEL_INFLUENCE_COST` 1.

- *proposed, yes*: influence `- TRADE_INFLUENCE_COST`, never below nought, whatever the answer.
- *accepted, yes*: where the route is not good, nothing. Otherwise the route is made, dated the
  offer's; where the two need a convoy, one is made for it (section 5) - **and where none can be, the
  route stands anyway**, to be destroyed the next day; relation `+ TRADE_RELATION_CHANGES`.
- *declined*: the messages only.
- *no, any stage* (cancelling): the matching route ends; relation `- TRADE_CANCEL_RELATION_COST`.
  **No influence is charged**, though the texts show `TRADE_CANCEL_INFLUENCE_COST`.

The messages, told to the actor, the recipient and anybody else in that order: accepted
`ACTRAWITHUS` / `WETRADEAG` / `OTHERTRADEAG`; declined `DETRAWITHUS` / `WEDETRA` / `DETRAWITHOTHER`;
cancelled `WEBRKTRADE` / `BRKTRADEUS` / `BRKTRADEOTH`. **A played country is not told an accepted trade
while its player has set automate-trade** (or handed his foreign policy to the AI, not built), nor a
declined one while automate-trade is set.

## 4. How a route ends

| how | what |
| --- | --- |
| cancelled by either side | section 3; **an AI's cancellation always names the route's `from` as the one breaking it**, whichever of the two asked |
| war between the two | every route of the pair ends when the war starts, silently |
| an embargo | every route of the pair ends when it is imposed (`agreements.md`, section 5) |
| its convoy is needed and missing | the route ends in the next day's trade, silently; nothing is given back |

**A route never ends of itself**, and staying inactive does not end it.

## 5. The convoy

**Whether the two need one** (`confirmed`): **not** where they are land neighbours, where their acting
capitals are on one continent, or where the areas of their capitals are joined over land; otherwise -
and where either capital has no area - they do.

**Whose it is and where it runs** (`confirmed`): the **buyer's** - `to` where `trade_to` holds money,
else `from`. Its end is the harbour of the other country's acting capital's area nearest to the
buyer's capital area's first harbour - its largest naval base, `convoys.md`'s "first harbour" - and
its start the harbour of the buyer's acting capital's area nearest to that end, each by the straight
line between the harbours' sea provinces. Both ends must be harbours with a naval base. **That the
convoy can be made** is the same test. It starts with no transports and is manned like a resource
convoy (`convoys.md`): its transports wanted are its path's length times `CONVOY_PATH_LENGTH_MULT` and
**`CONVOY_TRADE_WEIGHT_MULT`**, scaled by its end's naval base, at least one; it is ranked by what its
day carries, and it is idled where its start area produces none of crude oil, metal, energy or rare
materials (*inferred*).

**It ends** in the daily look at a country's convoys where no route of the pair carries it any more, or
an end is held by a country at war with its owner or by rebels. A cancelled route's convoy stops being
a trade convoy at once and goes the next day.

## 6. The day

`confirmed`. **Each country's day runs its routes after what its provinces produced reaches its
stockpile and before its IC is worked out.** For each other country and each route between them, in
the order the routes were made:

1. **the convoy**: where one is needed and missing, the route is marked to end after the walk. Where
   its path is shorter than two provinces, it has no transports, or its way is not usable, the route
   is **inactive**, the reason `TRADE_INACTIVE_CONVOY` naming the buyer. Both countries' days do this.
2. **only in `from`'s day**, the exchange. With e the convoy's efficiency - 1000 without a convoy, and
   not held to 1000 - for each good in turn:
   - `from` gives `trade_from[good] * e / 1000` and gets `trade_to[good] * e / 1000`, and `to` the
     other way, each out of and into its stockpile;
   - **debt** (money only): where the payer is at war with anybody and allows itself debt with the
     other (`aid.md`), its payment goes onto the debt instead of out of its stockpile, the other still
     being paid in full - and then the next test is off for the rest of that route's goods that day;
   - **where `from`'s stockpile is below nought afterwards**, the route is inactive,
     `TRADE_INACTIVE_NO_RES` naming `from` and the good, and nothing of the day's exchange stands;
     then the same for `to`. So **a buyer who cannot pay stops the route**, money being a good here;
   - otherwise both countries' traded-away and traded-for figures are credited good by good - so a
     route that fails on a later good has already counted earlier goods that never moved - and the
     convoy's day carries what was given and got;
   - after the seventh good the route is active and the stockpiles are kept.

A route is inactive on any day it cannot run and active again on the first day it can. Nothing of
inactivity is kept in a save.

## 7. The offer window

`TradeDiplomacyDialog`, opened by the row "Offer Trade Agreement" (`confirmed`):

- `LeftShield`, `RightShield`: the actor's and the recipient's flags; `Title` the row's text;
- **a slider for each good but money** - `supplies_slider`, `fuel_slider`, `crude_slider`,
  `metal_slider`, `energy_slider`, `rare_met_slider` - whose left button sells and right buys, each
  with its words, `SELL` "Sell" and `BUY` "Buy", as its file declares them. A
  slider's place s, 0 to 1000 in thousandths of its length, is an amount a day of
  `-MAX_DAILY_TRADE * (500 - s) / 1000` below its middle and `MAX_DAILY_TRADE * (s - 500) / 1000` from
  it: **50 a day either way at the ends, a tenth a step** in Their Finest Hour. The window opens with
  every slider in the middle; `reset_<good>` puts one back;
- `value_<good>`: `TRADE_DAY` "$VAL$/Day", to two places;
- `Description`: the offer's text, `TRADENEWDESCTWO` (section 10);
- `chance_string`: the word for the AI's likely answer (`agreements.md`, section 7);
- `debt_desc`: `IS_BOUGHT_WITH_DEBT`, shown only where the actor allows itself debt with the recipient
  and is at war;
- `AgreeButton` "Accept", **pressable only where the route is good and its convoy could be made**, with
  the tooltip: the row's tooltip; or `TR_CANT_CONVOY` naming the buyer; or for a barter
  `NO_TRADE_RULE` with `$RULE$` the rule's name; or for an offer with no price, nothing.
  `DeclineButton` "Decline".

**Each frame**, where a slider has moved, every good's amount is set again (section 1) and the
description and the chance made again. Accept dates the offer now and posts it. The window closes when
the screen is hidden or the offer stops being valid.

**Where it stands** (`confirmed`, `FINDINGS-tradeui.md` B.3): its upper left corner at **the screen's
middle plus `diplo_request_pos`**, (10, -100) in the files - not where its own file puts it. The
diplomacy screen's other request dialog stands there too. A screenshot of the original gave it first,
within three pixels.

## 8. The rule `free_resource_gifts`

A country has it where its faction is real and the faction's rules give it - in the base game the
Comintern's. **Between two countries that both have it**, any route is good and setting an amount leaves
money alone, so they may trade goods for goods, or for nothing.

## 9. The AI's trading

**The AI trades by the install's Lua** (`confirmed`): **each country once a day, at the hour its place
in the countries' list gives it** - the list the original keeps, its null country first and then the file's, cut into
24 runs of a 24th of their number (102 countries: four an hour, the last hour the ten left), the hour of the day picking
the run, the rebels and a country owning nothing passed over (`confirmed` on the listing and the running game,
`reversing/findings/FINDINGS-liveread.md`, section 104; until 2026-11-16 this said each hour, which the record had too, and
ours rolled for every country every hour - about 180 draws an hour where the original makes 4 to 8, and a script's
trading run some 25 times an hour where the original runs it less than once). Every country with an AI - the player's
own only while his automate-trade is on, or his foreign policy handed over - while a game is running, draws on the game's
generator: **one in six, `ProposeTrades(ai, tag)`**; otherwise a second draw, **one in ten,
`EvalutateExistingTrades(ai, tag)`** - so one day in twelve. A Lua error ends that country's turn. The scripts make an offer with `CTradeAction(actor, recipient)`, set its amounts with
`SetTrading`, test it with `IsValid`, `IsSelectable` and `GetAIAcceptance` - which asks the
recipient's `DiploScore_OfferTrade`, re-entering the Lua - and post it with `ai:PostAction`, which is
an offer as in `agreements.md`, section 1: **the script's own construction hour is its date**. They cancel
a route through `ai:EvaluateCancelTrades(amount, good)`: every route on which the country hands over
that good, at most the amount still to be cut, is cancelled, and then the smallest route of more than
what is left, if anything is.

**`DiploScore_OfferTrade(ai, actor, recipient, observer, action, tradedFrom, tradedTo)`**: the observer
is the recipient; `action` is the offer itself; `tradedFrom` and `tradedTo` are fresh copies of the two
blocks a day, `trade_from` and `trade_to`, as single-precision numbers **in the order money, fuel, crude
oil, metal, energy, rare materials, supplies**. A score that is not a number fails; a failure is 0.

What else the scripts are handed is `lua.md`, section 4: the API built for them (`FINDINGS-tradeaid.md`,
section B3). **The AI makes at most one offer to a player about every 26 days**: `GetSpamPenalty` is
ten for each day from now to 24 days past its last diplomat's arrival there, and the script keeps an
offer only where the score less it is above 50.

**The player's automate-trade** answers the trades made to him and lets the Lua trade for him: section 12.

## 10. Texts

- **The row**: "Offer Trade Agreement", `TRADETITLE` (the prefix `TRADE`, a cancellation's `CANCELTRADE`),
  listed at peace after influence and only where the offer is valid. Its tooltip, the first that holds:
  `ACT_NO_SELF`, `ENROUTE`, `WEWAR`, `SUBJECT`, `DIP_NO_INFL` (the cost whole), `CDIP_EMBARGO` with the
  recipient's name, `TRADENO_PORTS` where they need a convoy and either has no harbour, else `TRADEDESC`;
  a cancellation's `CANCELTRADEDESC` after the influence.
- **The offer's text**, `TRADENEWDESCTWO` "We offer $COUNTRY$ to $TYPE$ $SELL$ per day for $BUY$ per
  day.", COUNTRY the recipient. Where the actor pays nothing, SELL is what it gives, BUY what it gets and
  TYPE `TRADE_BUY`; where it pays, BUY is what it gives, SELL what it gets, and TYPE `TRADE_TRADE` if the
  recipient pays too, else `TRADE_SELL`. A list of goods is each non-nought amount "§Y0.40 Metal§W "
  joined with ", " and "and ", or `TRADE_NOTHING`.
- **Inactive**: `TRADE_INACTIVE_CONVOY` "$COUNTRY$ have not assigned enough convoys.",
  `TRADE_INACTIVE_NO_RES` "$COUNTRY$ does not have enough $RES$.".
- **The messages** (`confirmed`, `FINDINGS-tradeui.md` A.6), who is told which in `agreements.md`,
  section 8. Every one has the player's country's own values, then ACTOR, **ACTORS - the actor's name
  again, which `WEDETRA_5` asks for** - RECIPIENT and MESSENGER. A list of goods as above fills two
  more:

  | message | ACT_TRADE | REC_TRADE |
  | --- | --- | --- |
  | `ACTRAWITHUS`, to the actor, and `OTHERTRADEAG` | the route's `from` block | its `to` block |
  | `WETRADEAG`, to the recipient | its `to` block | its `from` block |
  | `BRKTRADEUS`, to the one whose route was broken | what the player gives on the route | what he gets |

  The declined messages and the other two of a cancellation have neither. `ACTRAWITHUS_6`, `WETRADEAG_4`
  and `BRKTRADEUS_6` say "We will supply them with $ACT_TRADE$ per day in exchange for $REC_TRADE$ per
  day." and "We will no longer receive $REC_TRADE$ for $ACT_TRADE$".

## 12. Automate trade

The diplomacy screen's checkbox `trade_automate_checkbox`, "Automate Trade" (`confirmed`,
`FINDINGS-tradeui.md` A.7), with no tooltip. While a player has it ticked:

- **the trades offered to him are not put to him** but kept on his list, and **answered at his country's trading hour,
  once a day** (section 9 - the answers are the first part of that country's turn):
  every proposal that makes a route is scored as the AI scores one (section 2) and rolled, the roll
  `(r1 * r2) % 100` of two draws of **the C runtime's `rand()`, seeded to 1 at every hour** - `x = x *
  214013 + 2531011`, `r = (x >> 16) & 0x7FFF` - so the rolls are the same every hour: 47, 0, 56, 24, 68,
  25, 87, 51... **A roll below the score accepts, the first only; a later trade it would accept is kept
  for the next hour; any other roll declines.** There is no cut at 30 here. Whatever on the list is not
  a proposal that makes a route is dropped;
- **then the install's Lua trades for him** as for a country nobody plays (section 9);
- **he is not told a trade accepted or declined** - `ACTRAWITHUS`, `WETRADEAG`, `DETRAWITHUS`,
  `WEDETRA`. A cancellation is told as ever.

## 13. The list of trades

The production screen's `trade_list`, under `PW_TRADE_ROUTES` "Trade Routes", `PW_EXPORT` "Export" and
`PW_IMPORT` "Import" (`confirmed`, `FINDINGS-tradeui.md` A.2 to A.5):

- **It holds every route of the player's**, active or not, either side: the other countries in their
  order, and each one's routes in the order they were made. **It is made again only when the country
  shown or the number of its routes changes**; otherwise each line's marker is set each frame.
- **A line**, `production_trade_item`: `name` is "@" and the partner's tag, a space and his name - the
  flag drawn in the text, then the name; **an icon for each good**, in the goods' order, a good the
  route's `from` hands over first and else one its `to` does, `<good>_trade_icon` (`supplies`, `fuel`,
  `money`, `oil`, `metal`, `energy`, `raremat`) each in a `production_trade_goods` window - **in
  `purchase_box`, the Export column, what the player hands over, and in `sales_box`, the Import column,
  what he gets**. Money is a good like the rest, so the price is the buyer's Export icon. **No figure is
  on the line.**
- **`inactive_icon`**, while the route is inactive: at the start of the Import column where the player
  is the one the inactive reason names, else of the Export column, **and 32 to the right** - on the
  column's second icon.
- **The tooltips**: a goods icon `PW_EXPORTING` "Exporting $NUM$ $TYPE$ to $COUNTRY$" where the player
  gives it, else `PW_IMPORTING` "Importing $NUM$ $TYPE$ from $COUNTRY$" - NUM the good's amount a day to
  two places, TYPE its name, COUNTRY the partner's; the marker `TRADE_IS_INACTIVE` with the reason
  (section 10) as its delayed half; anything else on the line nothing.
- **`cancel`** opens the `DefaultConfirm` dialog, another at each press: `CANCELTRADETITLE` "Cancel Trade
  Agreement" and `CANCELTRADENEWDESC` "Do you wish to cancel our agreement with $COUNTRY$?\nThis will
  stop the transfer of $SELL$ for $BUY$." - COUNTRY the partner, **SELL the route's `from` block and BUY
  its `to` block whichever side the player is on**, every `§W` turned to the dialog's black `§b`. Its
  "Ok" posts the cancellation, the player the actor and the partner the recipient (section 4); "Cancel"
  closes it.

## 14. An offer to the player

A trade offered to the player - and alliance, NAP and transit rights, `agreements.md`, section 10 - is
put to him as an icon in the bar under the top bar (`confirmed`, `FINDINGS-tradeui.md` B.1 and B.2). A
trade's icon is frame 3, "A Trade Offer" in its tooltip, whose delayed half is `TRADE_TOOL_REQDESC`
"$COUNTRY$ would like $TO$ each day in exchange for $FROM$ each day." - TO what the player gives, FROM
what he gets. Its dialog, `DefaultDialog`, has **no sliders**: its title `TRADETITLE` "Offer Trade
Agreement", the proposer's own words; its text `TRADEREQDESC` "$COUNTRY$ is offering us a Trade
Agreement. \n We would give them $TO$ each day in exchange for $FROM$ each day." - COUNTRY the proposer -
then, where the two need a convoy, `CONV_AT_TRADE` " $CONV$ would be responsible for the convoy needed
for this trade." naming the buyer, and where the proposer may run a debt with the player and is at war
with anybody, a space and `IS_BOUGHT_WITH_DEBT`; every `§W` the dialog's black. **It stays open two
months from the offer while the two are not at war**, and declines when it closes by itself.

## 15. Kept as read, though they look odd

1. An AI's cancellation names the route's `from` as the one who broke it, whoever asked.
2. `TRADE_CANCEL_INFLUENCE_COST` is shown and never charged.
3. The price stays as it was when the offer was made.
4. A route whose convoy cannot be made is accepted, and ends the next day; nothing is given back.
5. The exchange runs only in `from`'s day: a `from` owning no province never trades.
6. "Not enough" is asked after the exchange, of both stockpiles, money included, `from`'s first.
7. The traded figures are counted good by good, before a later good fails.
8. Paid on debt, the stockpile may go below nought for the rest of the day, and the seller is paid
   money nobody gave.
9. The convoy's efficiency is not held to 1000.
10. Two countries on one continent need no convoy, whatever lies between: Britain and France trade over
    land.
11. A script's offer keeps the hour it was made in, not the hour it was posted.
12. The four traded figures: the two "sans allied supply" are credited with the other two, the same.
13. Automate-trade's rolls are the same every hour, from 47, and it has no cut at 30.
14. A trade line has no figures; the columns are named the other way round in the file, `purchase_box`
    the Export column; the list is made again only when the number of routes changes, so one ending as
    another starts leaves a stale line; the inactive marker stands on a column's second icon.
15. The cancel dialog reads backwards for a route's `to`; each press of a line's cancel opens another.
16. An offer's dialog is titled with the proposer's words, "Offer Trade Agreement"; its debt line asks the
    proposer alone.

## 16. Not built

- **Debt** - `aid.md` - so nobody allows debt and the debt arm never runs.
- **What becomes of a route when either country ends** was not read.
- A trade's line in the top bar's tooltips beyond the figures.
- **Handing foreign policy to the AI**, which ticks the Automate Trade box whatever it holds and answers
  everything for the player.

## Ours

- **The countries trade side by side, as the original's do, but always the same way.** The original runs
  the hour's trading on its worker threads, each with a Lua state of its own (`FINDINGS-tick.md`, the TBB
  section; `FINDINGS-luahost.md`), taking countries as threads come free - so which state runs a country,
  and in what order offers are posted, varies from run to run. Here (2026-10-13, `CAiWorkers`): the
  hour's draws are made first, in the countries' order; the scripts then run on **eight states of their own**
  whatever the machine, a country always on the same one - its number modulo eight - and in the countries'
  order there; what they post reaches the outbox, and what they report the log, **in the countries' order**.
  A script only reads the state and posts, so this is the countries' order exactly: on a new game, ten days
  of it make the same offers, answers and routes as one state running the countries in turn. **Each state
  has a `math.random` of its own**, the C runtime's `rand()` seeded 1, as a new thread's is in the original;
  the base game's trading draws on none, BlackICE's does. **A script's draws on the game's generator** -
  `CAIIntel`'s, which the base game's trading does not reach - are made on a generator of the run's own,
  seeded from the game's seed, the hour and the country, where the original's threads share the game's.
  **A player's automate-trade is answered in its place**: the scripts of the countries before it run, then
  it, then the rest, since answering changes the player's list of offers. And the trading comes **after the
  hour's answers to offers** - which of the two the original's hour runs first was not read. On the
  development machine it took an hour from 30 ms to 9 (`OpenHOI3.SaveCheck profile`).
- **A cancelled route's convoy delivers nothing** on the day the convoys' look removes it; whether the
  original's convoy, carrying money and no longer a trade convoy, delivers once first was not read.
- **A good is named in a trade's text by the key the diplomacy screen names it by** - `METAL_IRO` and
  the like, which say the same words; the key the original reads was not.
- **A good's line in the window shows the amount as set, below nought for a sale**; whether the
  original shows the size alone was not read.
- **Automate Trade is a command**, `CSetAutomateTradeCommand`, and the game state's: the original's
  checkbox writes its local player's own flag, which the hour's trading then reads on every machine.
  Every change to the game state is a command here.
- **An offer is put to the player in the hour's pass**, where the original's screen looks at his list
  every frame; and the answers under automate-trade come in the same hour, where the original's come
  the hour after.
- **No sound** plays for a new offer: the original plays `diplomaticoffer`.

## Not established

- What becomes of a route when either country ends.
- Whether the original's AI trading also runs for countries owning no province; here every country
  nobody plays is asked.
- **What it costs**: with nobody playing, a new game's hour of trading by the install's scripts takes
  about 80 ms here (`OpenHOI3.SaveCheck trade`), most of it the Lua itself; the original spreads it
  over worker threads.
