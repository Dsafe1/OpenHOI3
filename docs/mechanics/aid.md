# Aid: debt, shared technology, lend-lease and licences

The agreements' second half after trade (`trade.md`): four ways one country helps another. Read out of
the original in the trade and aid wave (`reversing/findings/FINDINGS-tradeaid.md`, section C) and the
aid screens wave (`reversing/findings/FINDINGS-tradeui.md`, section D). An offer of any of them goes
the way every offer goes (`agreements.md`, sections 1 to 4 and 10); what follows is what is each one's
own. Figures are thousandths unless said otherwise; the defines are the diplomacy block's.

## 1. Debt

**`allowDebts` on X's status towards Y means X may run a debt with Y** (`confirmed`). "Allow Debt" is
asked by the would-be debtor - `ALLOWDEBTDESC` "Request to be allowed to accumulate debt during
wartime when trading with them".

**The offer** (`CDebtAction`), its four arms:

- **Asked** (a proposal that makes): the actor pays `ALLOW_DEBT_INFLUENCE_COST`, held at nought.
- **Accepted**: the actor may now run a debt with the recipient. The actor is told `DEBTALLOWACCEPT`.
- **Declined**: the actor is told `DEBTALLOWDECLINE`.
- **Revoked** (a proposal that ends; the actor is the creditor): the recipient may no longer run a debt
  with the actor, and **the recipient - the debtor - pays `REVOKE_DEBT_INFLUENCE_COST`**, held at
  nought. It is not answered. The recipient is told `DEBTREVOKE`.

Nobody else is told anything. **Nothing here touches the debt itself**: a revoked permission leaves the
debt standing, and it goes on being repaid. **A war between the two clears the permission both ways**,
and not the debt.

**How a debt grows**, only while the debtor is at war with anybody and holds the permission towards
the creditor: a licence's fee goes onto the debt (section 4) and the creditor is paid nothing then; a
trade's money goes onto it instead of out of the debtor's pool, and the creditor is paid all the same
(`trade.md`, section 6). The debt is kept on the debtor's status towards the creditor.

**Repayment**, at the end of each country's day of production, before its trade - only for a country
owning provinces. For each other country O, the daily figure r is the status's own, worked out again
wherever the debt is above nought and the figure is not:

    r = debt * 1000 / (YEARS_TO_REPAY_DEBT * 365000 / 1000)        integer, truncated; -1 where the days are 0

Where r is above nought: **if the debtor is at war with anybody, or its money is not more than r**, the
figure is set to nought and nothing is paid that day - so it is worked out again from the debt the next
day; **otherwise** r money leaves the debtor's pool for O's, and is counted on the debtor's
`repaid_away` and O's `income_from_debt`. **The debt is never lowered** (`confirmed` by a sweep of the
whole image): a debt is repaid a 18,250th a peaceful day, for ever, in Their Finest Hour's 50 years,
and one under 18.250 gives a figure of nought and is never repaid.

**The row** "Allow Debt" / "Revoke Debt" (`ALLOWDEBTTITLE`, `REVOKEDEBTTITLE`), at peace, after the
licence's, where neither is in exile and the player is no puppet: **revoking where the recipient may
run a debt with the player, else allowing**. It may be pressed where - allowing - the player may not
yet run one with them, or - revoking - they may with him; where his influence is at least **both**
costs; and at peace - and, as for the AI's offers, where no diplomat is on his way to them, neither is in
exile, **the actor is no puppet and the recipient no puppet but the actor's own** (`CDebtAction::IsSelectable`,
`reversing/findings/FINDINGS-agreements.md`, B.8). So **a puppet never revokes its master's leave to run a debt with
it**: Yemen and Nepal, Britain's, at 1941.7.16, whose foreign ministers' `HandlePeace` asks it of them with 100
(`FINDINGS-liveread.md`, section 133). Its tooltip: `ACT_NO_SELF`, `WEWAR`, `ENROUTE`, `DIP_NO_INFL` (the cost whole),
then `ALLOWDEBTDESC` or `REVOKEDEBTDESC`. **The dialog's text**: allowing, `ALLOWDEBTNEWDESC` "...when
trading with the $NAME$? ... repaid over $YEAR$ years ... cost us $COST$ influence points." - **NAME
the recipient's faction's name**, YEAR `YEARS_TO_REPAY_DEBT` and COST the allowing cost, both whole;
revoking, `REVOKEDEBTNEWDESC` with NAME the recipient and COST the revoking cost. The AI answers by
`DiploScore_Debt`. **An offer to the player** is icon 11, "Allow Debt", its dialog
`ASKDEBTREQDESC` with ACTOR the proposer, open two months while valid.

## 2. Shared technology

**The offer** (`CShareTechnologyAction`) takes effect when proposed and is never answered; nobody is
told:

- **Share** a theory: the actor pays `SHARE_TECH_INFLUENCE_COST`, and **the receiver uses the actor's
  level of that theory wherever it is above its own**. Where the receiver already had a sharer for
  that theory, **that sharer stops sharing every theory with the receiver** first.
- **Stop**: the actor stops sharing every theory with the recipient, free.

**Only theories can be shared.** The receiver's level of a shared theory is `max(own, sharer's own)`
everywhere a theory's level is read - research speed, the build-cost discount, the screen - following
the sharer's as it changes. An event's `practical` on a shared theory adds to `max(own, shared)` and
stores it as the receiver's own. **The sharer pays `SHARE_TECH_LEADERSHIP_COST` leadership a day for
each theory it shares** (`leadership.md`).

**Each day** a share is dropped where the receiver owns no province or the sharer's own level is not
above the receiver's own; dropping one stops every theory that sharer gives the receiver.

**The row**, among expeditions and technology, where the two are allied or in one real faction: "Stop
sharing" where the player shares with them, else "Share Technology" where some theory puts them below
him. Its dialog, `ShareTechnologyDialog`: section 6.

## 3. Lend-lease

**What a country holds**: what it gives - each receiver with a weight and an effectiveness - and what it
receives from each giver, and the lend-lease received and not yet made IC.

**The two offers**:

- **Asking** for lend-lease (`CRequestLendLeaseAction`): the actor asks, the recipient would give. Asking
  costs **`ALLOW_DEBT_INFLUENCE_COST`**. Accepted, the recipient gives to the actor; the actor is told
  `LENDACCEPT`; declined, `LENDDECLINE`. A cancellation ends the recipient's giving to the actor and
  tells the recipient `LENDOFFERREFUSED`, written from the giver's side - ACTOR and RECIPIENT turned
  round.
- **Offering** it (`COfferLendLeaseAction`), free: accepted, the actor gives to the recipient; the actor
  is told `LENDOFFERACCEPT`; declined, `LENDOFFERREFUSED`. Stopping ends it and tells the recipient
  `LENDREVOKE`, turned round likewise.

To start giving appends the receiver with **weight 500 and effectiveness nought**; to stop takes it off
both lists and scraps the receiver's lend-lease convoy from that giver, its ships back in the receiver's
pool.

**Who may**, the first that holds: either in exile, `ACT_LENDLEASE_ANY_GOI`; the giver's effective
neutrality above `LEND_LEASE_NEUTRALITY_LIMIT`, `ACT_LENDLEASE_OUR_NEUTRALITY` or
`ACT_LENDLEASE_NEUTRALITY`; the receiver at war with nobody, `ACT_LENDLEASE_THEM_ONLY_AT_WAR` or
`ACT_LENDLEASE_ONLY_AT_WAR`; the two at war, `WEWAR`. Then the way: **over land** where they are
neighbours or the giver is among the receiver's controller-neighbours, `LEND_LEASE_ALLOWED_NEIGHBOUR`;
**at once** where the two capitals are on one continent, `LEND_LEASE_ALLOWED_TELEPORT`; **by convoy**
where both have a naval base, `LEND_LEASE_ALLOWED_CONVOY`; otherwise not, `LEND_LEASE_NOT_ALLOWED`.
The giver need not be at war.

**Each hour**, for each giver of each receiver: where it may no longer, the giving is ended and the
receiver told `LENDREVOKE`; over land or at once, effectiveness 1000; by convoy, **the receiver** makes
and owns a lend-lease convoy from the giver's harbour to its own, and the effectiveness is the convoy's
efficiency - the share of transports it has of those it wants. With no harbour, nought. **The convoy
carries nothing.**

**Each day** the giver's lend-lease share - the first of its six - is shared out (`confirmed`,
`FINDINGS-tradeui.md` E.1):

    th     = floor(share * TotalIc * 1000 / 32768)                 thousandths of IC; share in 1/32768
    for each receiver, in the order the giving began:
        part   = weight * 1000 / the weights' sum                  truncated; 0 where the sum is
        amount = th * part / 1000 * effectiveness / 1000           each truncated
        where amount is no more than the IC the giver's stockpile pays for (the least of metal, half its
        energy, twice its rare materials): the giver charged 1 metal, 2 energy and 0.5 rare materials an
        IC, and the receiver credited

**A receiver the stockpile cannot pay for gets nothing that day**, and a later, smaller one may still be
paid. Lend-lease IC a country received itself is passed on at full cost. The weights are as the player set
them, not made to sum to anything; the AI sets each to its receiver's IC.

**The receiver**, in its own day, after its resource cap (`confirmed`, E.2): `ll = floor(income / 1000)`
whole IC is added to its total IC and kept as its **lend-lease IC**, needing no resources - so it lands
whatever the stockpile holds. It is taken out of the givers' receipts in order, the fraction below 1.000
staying with the latest. A country owning no province makes none of it. Ending a giving takes that giver's
part off the income, never below nought.

**The lend-lease IC is free to the other five shares** (`confirmed`, E.3): each share's part of it is its
fraction of `ll`, `floor(share * ll * 1000 / 32768)` thousandths, and is not charged against resources -
consumer goods, supplies and upgrades take it off their one bill, reinforcement uses it up on its first
brigades, and **production** spends it first line by line, unasked, then pays the rest of a line's day as it
always does, a line's progress snapping from 999 to a whole day on that path alone. The lend-lease share's
own part of it is used by nobody. **Reinforcement and upgrades charge whatever the stockpile holds**, as
consumer goods and supplies do; only production and lend-lease ask first.

**A sea route's convoy** (E.4) is the receiver's, made empty, and manned by the receiver's daily sharing
out of its transports like the poorest resource run - first emptied, last filled, idle where the giver's
capital area produces none of crude oil, metal, energy and rare materials. It wants `(the end harbour's
naval base level / 10 x the path's length x CONVOY_PATH_LENGTH_MULT + 1)` transports, and its efficiency
is the share of those it has. So a new sea route sends nothing on its first day.

**That share is multiplied by the convoy's own lend-lease efficiency**, whole when the convoy is made: `confirmed`
(`reversing/findings/FINDINGS-convoys.md`; `reversing/findings/FINDINGS-definesuse.md`, section 10).

    a raid sinking transports:  own -= LL_CONVOY_EFF_IMPACT x (transports sunk x 1000 / transports it had) / 1000,
                                not below nought - the sunk counted no more than it had, before they are taken off
    each day, in its country's convoy pass before the removal tests:
                                own += LL_CONVOY_EFF_REGAIN where below a whole, held to a whole
    the convoy's efficiency = its share of transports x own / 1000

In Their Finest Hour (0.4 and 0.01), a route that loses half its transports keeps 80% of what is left and is whole
again twenty days on. **The original does not save it**, and a convoy read from a save is made whole, so a load gives
every lend-lease route its full efficiency back (`docs/enhanced.md`, *A whole game's replay*). Built 2026-11-18
(`CConvoy.LendLeaseEfficiency`).

**The most a country may give**, `TotalIC * (LOW + (HIGH - LOW) * (1 - min(1, EffectiveNeutrality /
LIMIT)))` - 0.90 at neutrality nought to 0.05 at 60 in Their Finest Hour - **is enforced by nothing**:
it is the slider's marker, the top bar's tooltip and the AI's aim.

**The rows**, at peace and only where valid (the recipient owns a province): "Request Lend-lease" /
"Cancel Lend-lease" and "Offer Lend-lease" / "Stop Lend-leasing". Their tooltips and texts: section 6.

## 4. Licences

**The offer** (`CLicenceTechnologyAction`): the actor buys, the recipient sells, a unit type, `serial`
units a run and `parallel` runs, at `money` a unit.

- **Asked**: the buyer pays `LICENCE_INFLUENCE_COST`, held at nought.
- **Accepted**: the fee is `money x serial x parallel`, each product truncated in thousandths. **Where
  the buyer is not on debt** (section 1) **and has less money than the fee, the acceptance becomes a
  decline.** Otherwise `parallel` orders of `serial` units of the type are put in **the buyer's** queue,
  at its acting capital, **built at the seller's technology levels**, a land type's as reserves; and
  the fee goes onto the buyer's debt where it is on debt, else from the buyer's money to the seller's.
- Only the buyer is told, at the seller's capital: `LICBUYACCEPT`, its ACCEPTED the key `ACCEPTED`
  shown as itself; or `LICBUYDECLINE` with `DECLINED` likewise. NAME the seller's best model of the
  type, NUM `serial x parallel`.

**The row** "Buy Production Licence", at peace, always listed; it may be pressed with no diplomat on
the way and no war, and with `LICENCE_INFLUENCE_COST` of influence. Its tooltip `BUYLICENCEDESC`. Its
dialog, `LicenceDialog`: section 6.

## 5. Kept as read, though they look odd

1. A debt is never lowered, so it is repaid for ever; one under 18.250 is never repaid.
2. Revoking a debt permission charges the debtor; its message `DEBTREVOKE` names the one told as
   RECIPIENT.
3. The Allow Debt dialog names the recipient's faction, not the recipient.
4. Asking for lend-lease costs the debt's define; offering costs nothing.
5. An accepted licence the buyer cannot pay turns into a decline; the licence is built at the seller's
   levels in the buyer's queue, a land type as reserves.
6. The lend-lease cap is enforced by nothing; a receiver whose share is more than the giver's resources
   allow gets nothing that day.
7. A lend-lease convoy belongs to the receiver and carries nothing.
8. When the rules end lend-lease the receiver is told the giver "revoked" it.
9. A new sharer of one theory ends every theory the old one gave, and the day's check drops all of a
   receiver's theories from a sharer when one fails.

## 6. The screens

(`confirmed`, `FINDINGS-tradeui.md` section D.) Every request dialog stands at the screen's middle plus
`diplo_request_pos`, as the others do (`agreements.md`, section 7); an offer of any of these put to the
player is its icon and `DefaultDialog` (`agreements.md`, section 10): debt icon 11 with `ASKDEBTREQDESC`,
licence 10 with `ASKBUYLICENCEREQDESC`, lend-lease 12 and 13 with `ASK_LEND_LEASE_DESC` and
`OFFER_LEND_LEASE_DESC`.

**The share-technology dialog**, `ShareTechnologyDialog`: a row (`share_tech_category_entry`) for each
theory in which the player's own level is above what the recipient reads, in the categories' order - its
icon `icon_<key>`, its name, our level **green** and theirs **red**, both whole. Clicking a row's box
chooses it and un-chooses any other; clicking the chosen one un-chooses it. The description is
`TS_NO_SELECTED` with nothing chosen, Accept greyed; with a theory chosen `TS_SELECTED` - the theory, the
recipient, `SHARE_TECH_INFLUENCE_COST` and `SHARE_TECH_LEADERSHIP_COST` to one place. Accept writes the
theory into the offer and posts it. **The rows are never refreshed while it is open, and it has no
tooltips.** Stopping is the plain dialog with `CANCEL_SHARE_TECH_INFO`, the first theory shared, or `_NO`.

**The licence dialog**, `LicenceDialog`: a row (`prod_share_entry`) for each type listed (section 4), in
the types' order - its counter `GFX_counter_<key>` and **the seller's current model in brackets**,
"(§YName+*§W)", the "+*" where the seller's levels are not that model's exactly; the type's name where
it has none - and the "+*" too where no model is the seller's levels exactly. A row carries no figure. A row's
box is **greyed where the whole order takes more men than the buyer has, freed where it takes fewer, and left as it
is where it takes exactly his** - one unit's men cut to thousandths, then times the units - and Accept likewise; only
one row is chosen. The runs (`paralell_value`) and units a run (`serial_value`) go from 1 to 99 by their
buttons. **The money a unit is the slider's share of the buyer's money.** The description is
`BUYLICENCENONE`, or `BUYLICENCENEWDESC` - runs, units, the type, and the fee to one place; the chance word
is the seller's AI's; `debt_desc` shows where the buyer would pay on debt. Accept is greyed with no type
chosen or the men wanting. A row's tooltip, on every element of it but the counter: `BUYLICENCE_MPCOST`
with one unit's men to one place where the men are wanting, else the type's name. **Its delayed half is the
figures of the seller's version of the type** - the type at the seller's level of each of its technologies - a line
a figure as a technology's text writes them (`technologyscreen.md`, section 9), with no plus signs
(`reversing/findings/FINDINGS-tradeaid.md`, section C.2a; built 2026-11-21).

**The types listed** [exe; `reversing/findings/FINDINGS-tradeaid.md`, section C.2a], in the types' order, made once
as the window opens: a type that is buildable (`is_buildable`), that the buyer is among the users of (`usable_by`, or
the type names none), that the seller may still build at least one of - unlocked, its trigger holding, under its own
cap, so a seller at its cap of an elite brigade does not offer it - and that is no capital ship (`capital = yes`).
**Where the buyer could build one too, the two versions are compared**: each country's is the type at its own level
of each of the type's technologies, with no modifier, law or minister in it, and its combat value is

- a land type's: its soft, hard and piercing attack, a tenth of its toughness and defensiveness together (cut to
  thousandths), and its armour, all times two less its softness;
- an air type's: its air attack and air defence and 0.5;
- a ship's: 0.5 and its sea attack and sea defence, times its hull.

**The type is left out only where the buyer's value is strictly above the seller's** - a tie is listed, and so is a
type none of whose technologies move those figures. A type the buyer cannot build at all is listed with nothing
compared. Nothing asks of war, a pact, the branch or the buyer's men. The 10, the 2 and the 0.5 are the executable's
own; every figure compared is the type's file's and its technologies'. It is the value the AI gives a brigade
(`agents.md`, section 7.3).

**The lend-lease window**, opened by the production screen's lend-lease slider's "Distribute" - which
says `DISTRIBUTE` where the others say their need, and opens the window at every press instead of holding
the slider: a row (`lendlease_country_entry`) for each receiver in the order the giving began - its flag,
its name, a slider of its weight in thousandths of a percent, so a new receiver's 500 is half a percent,
and `actual_IC`, the slider's share of the lend-lease share's IC to three places, cut off. **The sliders
are kept at a hundred percent**: each frame what is missing or over is added in equal parts to every slider
not held, not locked and not moved, a hundredth where the gap is under one, nothing where it is five
thousandths or less. A player's move posts the whole set as the weights, once a second has passed. A row's
cancel asks `CANCEL_LL_PROMPT` with no title, and Ok ends the giving. Locked sliders stay locked when the
window is made again, which it is at every "Distribute" and whenever the receivers change.

## Ours

- **A licence's men a unit is what the order would take**, a land type as reserves; the original's own
  formula for the dialog multiplies the type's men by its own modifier, which is close and not the same.
- **The licence dialog's figures are made again when anything they rest on moves**; the original makes
  them every frame.
- **The licence dialog's runs start at one each**, and its money slider in its middle, as the file says;
  what the original sets them to on opening was not read.
- **`ASKBUYLICENCEREQDESC`'s variables** - NUM the units, TYPE the type, CASH the fee - are what the text
  asks for; `CAskBuyLicence` was not read.
- **While the lend-lease window is open, the production screen keeps its tooltips**; the original's lose
  theirs.
- **`repaid_away` and `income_from_debt` are cleared each day** with the day's traded figures.

## Not established

- Whether `repaid_away` and `income_from_debt` are cleared each day.
- Where in a country's day the shares of technology are looked at.
