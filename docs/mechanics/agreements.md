# The agreements: offers and pacts

Diplomacy's fourth piece in the maintainer's order. An **offer** - one country proposing something to
another - and the **pacts** an accepted offer makes: an alliance, a non-aggression pact, a guarantee,
military access asked and offered, an embargo, influence on a nation and alignment towards a faction.
The cut is this project's, not the maintainer's: **trade and aid** - trade, debt, lend-lease, licences,
shared technology - come after; a declaration of war, peace, a call to arms, war goals and factions
are later pieces of his order.

Source: `reversing/findings/FINDINGS-agreements.md` (an offer's way through the game, the screen's
actions, what each pact does; read 2026-10-13), `reversing/findings/FINDINGS-diploaction.md` (the
action object, its lifecycle, each pact's effect and messages), `reversing/findings/FINDINGS-diplomacy.md`
and `reversing/findings/FINDINGS-luaapi.md` (the AI's score). What two countries hold of each other is
`diplomacy.md`; the screen the offers are made from is `diplomacyscreen.md`; the AI's score is
`lua.md`, section 6. Figures are thousandths; the defines are `common/defines.lua`'s diplomacy block.

## 1. An offer

An offer has a **kind**, an **actor** (who offers), a **recipient**, a **stage** - proposed, declined,
accepted - a **value** - yes for making, no for ending (dissolving an alliance, revoking a pact,
lifting an embargo) - and a **date**, its hour. The kinds built and their keys:

| kind | key | the pact |
| --- | --- | --- |
| alliance | `allianceaction` | an alliance between the two |
| non-aggression pact | `nap` | a NAP |
| guarantee | `guaranteeaction` | the actor guarantees the recipient |
| ask for military access | `milaccess` | the actor may enter the recipient's land |
| offer military access | `offermilaccess` | the recipient may enter the actor's land |
| embargo | `embargo_action` | an embargo, the actor's |
| influence nation | `influence` | the actor's faction influences the recipient |
| align to faction | `influence_leaders` | the actor aligns towards the recipient's faction |

**Making it** (`confirmed`) is a command, executed alike on every machine:

1. where the offer **is not valid** (section 4) nothing happens;
2. a proposal stamps **the actor's diplomat en route** to the recipient: until `date + 168` hours,
   inclusive, the actor's buttons towards the recipient are greyed (section 7);
3. **the offer takes effect** for its stage and value (section 5);
4. it is put on a list of offers: the **recipient's**, if it is a proposal, else the **actor's**.

**Influence is charged on proposing**, whatever the answer (section 5), from the actor's
`diplomatic influence`, never below nought. Guarantee, embargo, influence and alignment have no
answer: **they take effect when they are proposed**.

## 2. The AI's answer

**Every country nobody plays answers the offers on its list every hour** (`confirmed`), in the order
they came, after the hour's other business:

- an offer is answered only where the AI answers its kind: an **alliance** or a **non-aggression
  pact** with value yes; **military access**, asked or offered, while proposed with value yes. Others
  are not answered - an offer to end something, a guarantee, an embargo, an influence, an alignment;
- its score is `DiploScore_<kind>(ai, actor, recipient, recipient)` (`lua.md`, section 6):
  `DiploScore_Alliance`, `DiploScore_NonAgression`, `DiploScore_DemandMilitaryAccess`,
  `DiploScore_OfferMilitaryAccess`;
- **a score of 30 or less declines; otherwise a draw of the game's generator, 0 to 99, below the
  score accepts and any other declines**;
- the answer is the offer itself, its stage set, made again as a command (section 1) - which puts it
  on the actor's list;
- every offer then leaves the list, answered or not.

**An offer on a list for more than 96 hours is dropped** as each country's day begins (`confirmed`) -
which, the AI answering every hour, only a played country's ever is.

**The player's country does not answer** its offers itself: the original shows each proposal to the
player as an icon and a dialog (section 9). Replies on the player's list are dropped.

## 3. The roll

The draw is the game's own generator, as every other roll here. *Ours*: the original draws from the
game state's generator on the AI's parallel workers, so which country draws first is not fixed there;
here the countries draw in their order.

## 4. When an offer is valid, and when a button may be pressed

Two tests the original keeps apart, and they **disagree** in places, which is kept. **Valid** decides
whether an offer does anything when executed; **selectable** greys its button (section 7). S is what
the actor holds towards the recipient; a **real faction** is any but the null faction; influence is
the actor's diplomatic influence. All `confirmed`.

| kind | valid | selectable |
| --- | --- | --- |
| alliance, offering | no alliance, no war between them; the actor not a puppet nor in exile; the recipient not a puppet but of the actor's; not both in one real faction | not the actor itself; no diplomat en route; valid's tests but the faction, and the **recipient** not in exile; **both** effective neutralities at most `ALLIANCE_NEUTRALITY_LIMIT`; influence at least `JOIN_ALLIANCE_INFLUENCE_COST` |
| alliance, dissolving | an alliance, no war between them, **neither at war with anybody**; the actor not a puppet; the recipient not a puppet but of the actor's | not itself; no diplomat; **neither** a puppet; influence at least `LEAVE_ALLIANCE_INFLUENCE_COST` |
| NAP, offering | no alliance, no war; the actor not a puppet; the recipient not a puppet but of the actor's; influence at least `NAP_JOIN_INFLUENCE_COST` - an existing NAP is not asked | not itself; no diplomat; valid |
| NAP, revoking | a NAP, no war; influence at least `LEAVE_NAP_INFLUENCE_COST` | not itself; no diplomat; valid; **the NAP may be broken** (section 6) |
| guarantee | no war, no guarantee between them either way | not itself; influence at least `GUARANTEE_INFLUENCE_COST`; the actor's effective neutrality at most `GUARANTEE_NEUTRALITY_LIMIT`; no war, no guarantee |
| ask for / cancel access | not one real faction; the actor without access to the recipient when asking, with it when cancelling | not itself; no war; influence at least `MILACCESS_INFLUENCE_COST` **when cancelling too**; not one real faction; valid's test; cancelling: **no unit of the actor in a province the recipient owns** |
| offer / revoke access | the recipient without access to the actor when offering, with it when revoking | not itself; no war; influence at least `MILACCESS_INFLUENCE_COST`; valid |
| embargo, imposing | no alliance, no war; the actor not a puppet; the recipient not a puppet **or the actor's own** | not itself; no diplomat; not one real faction; no alliance, no war; the actor not a puppet; **the recipient not the actor's own puppet**; the actor's effective neutrality at most `EMBARGO_NETRALITY`; influence at least `EMBARGO_INFLUENCE_COST` |
| embargo, lifting | an embargo between them | not itself; no diplomat; **no embargo or the actor's own**; no war |
| influence, starting | no war; the actor in a real faction; the recipient with no overlord or the actor; the actor a major or with more than 100 base IC; both own a province | not itself; the recipient owns a province; the actor not a puppet and a major or over 100; no war; the recipient **without** the faction's influence modifier; influence at least `INFLUENCE_INFLUENCE_COST`; the recipient not at war with a member of the actor's faction |
| influence, stopping | as starting, and the recipient **with** the modifier | as starting, but **with** the modifier and no influence asked |
| align, starting | not at war with them; the actor in **no** real faction; neither a puppet; both own a province | not itself; not at war with them; influence at least `ALIGN_INFLUENCE_COST`; the actor without the recipient's faction's influence modifier; the recipient's faction real and **the recipient its leader**; no alignment already; the actor not at war with a member of that faction |
| align, stopping | the actor in no real faction; neither a puppet; both own a province | not itself; influence at least `ALIGN_INFLUENCE_COST`; the actor **with** the modifier and an alignment running |

*A puppet* is a country with an overlord; *a major*, `major = yes` or base IC over 100.

## 5. What each pact does

`confirmed`, arm by arm. A relation change goes through `relation`'s one function (`diplomacy.md`):
it moves both sides by the same amount. **Each arm's three messages are given in one order: the one
told to the actor, the one told to the recipient, the one told to everybody else** - read off each
arm's two tests of the player against the actor and the recipient (2026-10-13), because the keys'
names do not say it: a declined NAP tells the proposer `THEYREJECTNAP`.

**Alliance.**
- *proposed, yes*: influence `- JOIN_ALLIANCE_INFLUENCE_COST`.
- *accepted, yes*: an alliance between the two, dated the offer's date; relation
  `+ ALLIANCE_RELATION_CHANGE`; messages `MILALLACCEPT` / `MILALLWEACCEPT` / `MILLALLINVACCEPTOTHER`.
- *declined*: the messages `MILALLREJECT` / `MILALLWEREJECT` / `MILLALLJOINREJECTOTHER`; nothing more.
- *no, any stage* (dissolving): the alliance ends; relation `+ ALLIANCE_REJECT_RELATION_CHANGE`;
  influence `- LEAVE_ALLIANCE_INFLUENCE_COST`; messages `MILALLWEBAN` / `MILALLBAN` /
  `MILALLBANOTHER`; **then relation -30 more**, a constant.

**Non-aggression pact.**
- *proposed, yes*: influence `- NAP_JOIN_INFLUENCE_COST`.
- *accepted, yes*: a NAP, dated the offer's; relation `+ NAP_RELATION_CHANGE`; `WENAP` / `THEYNAP` /
  `OTHERNAP`.
- *declined*: relation `+ NAP_REJECT_RELATION_CHANGE`; `THEYREJECTNAP` / `WEREJECTNAP` /
  `OTHERREJECTNAP`.
- *no*: the NAP ends; `LEAVE_NAP_THREAT_COST` of threat spread from the recipient's capital
  (`diplomacy.md`, section 5.2); relation `+ LEAVE_NAP_RELATION_CHANGE`; influence
  `- LEAVE_NAP_INFLUENCE_COST`; `WECANCELNAP` / `THEYCANCELNAP` / `OTHERCANCELNAP`.

**Guarantee** - no stage is asked.
- *yes*: the actor guarantees the recipient, dated the offer's; influence `- GUARANTEE_INFLUENCE_COST`;
  `WEGUARAT` / `GUARATOUS` / `GUARATOOTHER`; **no relation change**.
- *no*: the guarantee between them ends, whoever gave it; relation `+ REVOKE_GUARANTEE_RELATION_CHANGE`;
  influence **`+ REVOKE_GUARANTEE_INFLUENCE_COST`** - given back, not charged; `GUARACANCELTOTHEM` /
  `GUARACANCELTOUS` / `GUARACANCELTOTHEM` again. No button makes one (section 7).

**Ask for military access.**
- *proposed, yes*: influence `- MILACCESS_INFLUENCE_COST`.
- *accepted, yes*: **the actor may enter the recipient's land** - one direction; relation
  `+ MILACC_ACCEPT_RELATION_CHANGE`; `ACCMILUS` / `WEACCMIL` / `ACCMILOTHER`.
- *declined, yes*: relation `+ MILACC_DECLINE_RELATION_CHANGE` **only where either country is played**;
  the influence given back; `DECMILUS` / `WEDECMIL` / `DECMILOTHER`.
- *no, any stage*: relation `+ MILACC_DECLINE_RELATION_CHANGE`; the actor's access ends;
  `WECANMIL` / `CANCMILUS` / `CANMILOTHE`.

**Offer military access** - no influence and no define.
- *accepted, yes*: **the recipient may enter the actor's land**; relation +10;
  `OFFACCMILUS` / `WEOFFACCMIL` / `OFFACCMILOTHER`.
- *declined*: `OFFDECMILUS` / `WEDECOFFMIL` / `DECOFFMILOTHER`.
- *no*: relation -20, only where either is played; **access ends both ways**; `WECANMIL` /
  `CANCMILUS` / `CANMILOTHE`.

**Embargo** - no stage is asked.
- *yes*: an embargo, the actor's; relation `+ EMBARGO_RELATION_CHANGE`; influence
  `- EMBARGO_INFLUENCE_COST`; `EMBARGO_THREAT_COST` of threat spread from the recipient's capital;
  `WEEMBARGO` / `THEYEMBARGO` / `OTHEREMBARGO`; and the pair's trade routes end (none yet).
- *no*: the same threat **taken back**; the embargo ends where there is one; `WECANCELEMBARGO` /
  `THEYCANCELEMBARGO` / `OTHERCANCELEMBARGO`; no relation change, no influence.

**Influence nation** - no stage is asked.
- *yes*: nothing, where a member of the actor's faction already influences the recipient; else an
  influence: the recipient is given the actor's faction's influence modifier (its `influence` key in
  `common/ideologies.txt`, `align_towards_axis`) for `DAYS_OF_INFLUENCE_RELATION_CHANGE` days, for
  ever where below nought (-1 in Their Finest Hour); influence `- INFLUENCE_INFLUENCE_COST`;
  `WEINFLUENCE` / `THEYINFLUENCE` / `OTHERINFLUENCE`.
- *no*: the influence ends, the modifier taken off; `WEINFLUENCESTOP` / `THEYINFLUENCESTOP` /
  `OTHERINFLUENCESTOP`.

**Align to faction** - no stage is asked.
- *yes*: nothing, where the actor already has the recipient's faction's influence modifier; else an
  alignment: **the actor** is given that modifier, for `DAYS_OF_INFLUENCE_RELATION_CHANGE` days - the
  influence define, not the alignment's; influence `- ALIGN_INFLUENCE_COST`; `WEALIGN` / `THEYALIGN`
  / `OTHERALIGN`.
- *no*: where the actor has the modifier, the alignment ends and it is taken off; `WEALIGNSTOP` /
  `THEYALIGNSTOP` / `OTHERALIGNSTOP`.

**Neither influence nor alignment moves relation**: what they do is the modifier, which the
alignment triangle's influence term reads (`alignment.md`).

## 6. How pacts end by themselves

**At the head of each country's day** (`confirmed`), for every country owning a province that it holds
a status towards, in this order:

1. **an influence on it ends** when the influencer's faction is its own, it is in any real faction, it
   is a puppet, either owns no province, or its days have run (never, at -1) - silently;
2. **its alignment ends** when the two share a faction, either owns no province, or its days have run
   - with `DIPLOEXPIRE` / `DIPLOEXPIRE_ALIGN` to a played aligner;
3. **a guarantee of it ends twenty years** (175,200 hours) after it was made, silently;
4. **a NAP ends** on the first hour after `NAP_EXPIRY_MONTHS` months from its start (45), silently;
5. **an alliance ends when both are in one real faction** and neither is the other's puppet - with
   `MILALLWEBAN` / `MILALLBAN` / `MILALLBANOTHER`, the country whose day it is the actor.

Then, the same day, **each influence a country runs costs it `INFLUENCE_UPKEEP`**; one it cannot pay
leaves its influence at nought and ends the influence.

**A NAP may be broken** once `NAP_UNBREAKABLE_MONTHS` months have passed from its start; after that,
in each of three windows of `NAP_FORCE_BALANCE_RULE_MONTHS`, only while the breaker's brigades along
the common border stand to the other's at `NAP_BREAK_FORCE_BALANCE_1`, `_2`, `_3` (2, 1, 0.5); after
the third, always. **The balance along the border is not built** - it counts brigades on the fronts of
an area, which the simulation does not hold - so **a NAP may not be broken inside the three windows**
(listed under *Not built*).

**A war between two** ends their influence (and their trade routes, none yet) when it begins
(`confirmed`). What a declaration of war ends besides - their alliance, NAP, guarantee, access and debts
- is the declaration's, a later piece; `--war=` starts a war with none of it.

**The month's drift of relation** gains **+25 where the holder has military access** to the other
(`diplomacy.md`, section 3), before the cap above 100. One-way access moves the pair 0.012 a month,
two-way 0.024.

## 7. The screen's actions

The chosen country's panel lists **a row for each action the player may make towards it**
(`diploaction_list`, rows `diploactionentry` with the button `button_diploaction`), in this order, of
the kinds built (`confirmed`):

1. **Alliance**: "Dissolve this Alliance" (no) where they are allied; else "Offer Alliance" (yes),
   unless both are in real factions, the player is a puppet or in exile, or the recipient is a
   puppet but of the player's.
2. **NAP**, always: yes while there is none, else revoke (no).
3. **Guarantee** (yes), only where there is no guarantee between them or the recipient is its
   guarantor - **so none ever revokes one**.
4. **Ask for / Cancel Transit Rights** and **Give / Revoke Transit Rights**, unless both are in one
   real faction: the value is no where the access exists.
5. **Align to Faction** (yes) where the player is in no real faction, not at war with them, not
   aligning towards them and not a puppet; **Stop aligning** (no) where aligning.
6. **Embargo** (yes) where there is none; **Revoke Embargo** (no) where the player's own stands.
7. At peace only: **Influence Nation** / **Stop influencing** by whether the player influences them,
   where the recipient is in no real faction and not a puppet, starting needing the player a major or
   over 100 base IC - **listed only where valid**.

The original lists more - war, a call to arms, expeditions, technology, peace, war goals, factions,
licences, debt, trade and lend-lease - which are not built (*Ours*).

A row's **text** is `<prefix>TITLE`, the prefix the kind's and value's own: `ALLIANCE` /
`CANCELALIANCE`, `NAP` / `CANCELNAP`, `GUARANT`, `MILACC` / `CANCELMILACC`, `OFFERMILACC` /
`REVOKEMILACC`, `EMBARGO` / `CANCELEMBARGO`, `INFL` / `STOPINFL`, `ALIGN` / `STOPALIGN` (each read
off the kind's text slot). It is **greyed** where the action is not selectable (section 4) or **a
diplomat is en route** (section 1). Its **tooltip** is the action's description: the first of these
that holds, then the kind's own - `FINDINGS-agreements.md`, B.6 and B.7, has them for each kind, word
for word:

| key | when |
| --- | --- |
| `ACT_NO_SELF` | the actor is the recipient (not for guarantee, access, influence, align) |
| `ENROUTE` | a diplomat en route, `$DATE$` the hour he arrives |
| `WEWAR` | at war with them |
| `SUBJECT` | the actor is a puppet |
| `DIP_NO_INFL` | influence below the cost, `$INF$` the cost - whole for alliance, NAP and embargo, two places for the rest |

**Pressing a row opens the request dialog** (`DefaultDiplomacyDialog`): the two flags, `Title` the
row's text, `Description` the kind's dialog text with `DIP_INFLUENCECOST` where it costs, and
`chance_string` - **only where the AI would answer the kind** (section 2) - the word for the AI's
score:

| score | key |
| --- | --- |
| under 1 | `DIPCHANCE_IMPOSSIBLE` |
| under 10 | `DIPCHANCE_VUNLIKELY` |
| under 40 | `DIPCHANCE_UNLIKELY` |
| under 60 | `DIPCHANCE_MAYBE` |
| under 80 | `DIPCHANCE_LIKELY` |
| else | `DIPCHANCE_VLIKELY` |

**Accept** dates the offer now and posts it; **Decline** closes the dialog. The list is made again.
The dialog closes by itself when the screen is hidden or the offer stops being valid. **It stands with
its upper left corner at the screen's middle plus `diplo_request_pos`**, (10, -100), whatever its own
file says (`confirmed`, `FINDINGS-tradeui.md` B.3).

## 8. Messages

Each pact's message is chosen by whether **the player** is the actor, the recipient or neither -
the `WE` / `THEY` / `OTHER` keys of section 5 in that order - and **raised in all three cases**, so a
pact between two others is told too, as its type says (`confirmed`, `FINDINGS-diploaction.md`,
section 17, and `FINDINGS-agreements.md`, section E, for which key is whose). Every arm has a key for
each case; only an alignment's ending, `DIPLOEXPIRE`, is told to a played aligner alone. Its variables,
in order (`confirmed`, `FINDINGS-agreements.md`, section D):

1. the player's country's standard ones, `CCountry::BuildMessageVariables`'s: `COUNTRY` - its name -,
   `MONARCHTITLE` and `TITLE` - both the localised `<government>_ruler` of its government, "Führer",
   "Prime Minister", `nogovernment_ruler` for none - and each post of its government as
   `messages.md`, section 3, has it;
2. `MONARCHTITLE` again;
3. `ACTOR` and `RECIPIENT`, each country's name, and `MESSENGER`, the key `MESSAGE_HEAD_CHAN` ("The
   Foreign Office reports");
4. for an alignment ending, `ACTION` - `DIPLOEXPIRE_ALIGN` - and then `FACTION`, the faction's key,
   which `ACTION`'s text holds; for an influence or an alignment, `FACTION`.

**A text holding another's variable is filled because the variables are filled in order** - that is
how this runtime does it (`messages.md`, section 3); the original's order of filling was not read
(*Not established*).

## 9. Not built

- **An AI's offers other than trade**: no AI proposes an alliance, a NAP or transit rights yet - that is
  the foreign minister's Lua, the AI's - so only trades reach the player (section 10).
- **The player handing his diplomacy to the AI** ("Ai control"), under which his own offers are answered
  like an AI's and the 96-hour drop applies to him.
- **The NAP's border balance** (section 6), the NAP alert, and a pact's save block - there is no save.
- **The kinds of aid, war and peace, factions**: their rows, offers and pacts; trade is `trade.md`.
- The sound `diplomaticoffer` a new offer plays.

## 10. An offer to the player

(`confirmed`, `FINDINGS-agreements.md` A4 and `FINDINGS-tradeui.md` section B.) **Each hour every
offer on a played country's list is put to its player once, and the list emptied**: a proposal that
makes something, of a kind with a dialog - alliance, NAP, asked and offered transit rights, trade -
becomes an icon; under automate-trade a trade is kept back instead (`trade.md`, section 12); everything
else is dropped. A guarantee, an embargo, an influence and an alignment took effect when proposed and
ask nothing.

**The bar**, `diplomessageicons_window` at the position `diplomessageicon_start`, (340, 80) from the
screen's corner: an icon for each offer, `diplomessageicon_entry_window`, **the oldest on the left and
each 40 to the right of the one before** - the icon's width and `diplomessageicon_offset`'s x - as many
as start within the bar's width, ten; one more waits unseen and closes up when one goes. An icon is
`diplomessageicon_button`, `GFX_diplo_message_icons` at the frame of the kind - 1 peace, 2 alliance, 3
trade, 4 asked transit rights, 5 offered, 6 a call to arms, 7 NAP, 8 a faction, 9 an expeditionary
force, 10 a licence, 11 debt, 12 and 13 lend-lease asked and offered - and `flag`, "@" and the
proposer's tag, his flag.

- **Its tooltip** on the button or the flag: `DIPLOICON_TIP` "$TYPE$ from $NATION$. \n If we do not
  respond before $DATE$ we will autodecline." - TYPE the kind's words (`ICON_ALLIANCE`, `ICON_TRADE`,
  `ICON_ASKMIL`, `ICON_OFFACC`, `ICON_NAP`..., **`ICON_PEACE` and `ICON_NAP` in no table, so shown as
  themselves**), NATION the proposer, DATE the hour 168 after the icon came, " 0:00, 8 June, 1936". A
  trade's has a delayed half (`trade.md`, section 14).
- **It blinks from its 120th hour**: a tenth of its alpha for a tenth of each half second, whole
  otherwise. **At its 168th it declines its offer**, shown or not; the game's hours count, so a paused
  game declines nothing.
- **A left click opens its dialog and takes the icon away; a right click declines the offer at once.**

**The dialog** is `eu3dialog.gui`'s `DefaultDialog`, centred where its file puts it, for every kind:
the proposer's flag on the left, the player's on the right, the title the kind's row title - the
proposer's words, "Offer Alliance" - and the text the kind's key with the proposer's name:
`ALLIANCEREQDESC`, `NAPREQDESC`, `MILACCREQDESC`, `MILACCOFFDESC`, a trade's `trade.md`, section 14;
every `§W` in it the dialog's black. **Accept** ("Accept", its Enter taken away) and **Decline**
("Decline", Escape) post the answer, nothing tested. **Any number may be open, one over another.** Each
frame a dialog declines and closes once its time is up: two months from the offer and while it is
valid; an alliance a week, and neither allied nor at war; a NAP neither allied nor at war; asked transit
rights the months alone; a trade not at war.

## Kept as read, though they look odd

1. Guarantee, embargo, influence and alignment take effect when offered and are never answered.
2. Dissolving an alliance moves relation by its define **and then by -30 more**.
3. Revoking a guarantee **gives back** influence; declining asked-for access gives back its cost.
4. Withdrawn offered access ends access **both ways**; `MILACC_CANCEL_RELATION_CHANGE` is read by nothing.
5. No button revokes a guarantee; the guarantee row is absent where the player gives one.
6. The test that greys a button and the test that decides an offer disagree for alliance and embargo;
   only the first greys.
7. The chance words do not match the AI's cut-off: 10 to 30 reads "Unlikely" and is always declined.
8. The influence and align dialogs show no cost; the align dialog's days are the influence define's.
9. A guarantee ends after twenty years whatever its history's end date.
10. An alliance outgrown by both joining one faction ends with the "left the alliance" messages.
11. A right click on an offer's icon declines it with no question; an icon past the tenth is never seen
    and still declines itself after a week; Accept's Enter is taken away and Decline keeps Escape.
12. An offer's dialog is titled with the proposer's words; any number stand one on another.

## Ours

- **The countries answer in their order**, where the original's parallel workers draw in no fixed
  order (section 3).
- **Only the kinds built are listed** on the screen (section 7).
- A pact is kept on its two countries' statuses as the original keeps it, and in one list; the
  history's alliances and guarantees are pacts like any other.

## Not established

- In which order the original fills a message's variables, and so whether `DIPLOEXPIRE_ALIGN`'s
  `$FACTION$` is filled by it as it is here (section 8).
- Who asks a government's ruler title as a woman's or a puppet's (`FINDINGS-agreements.md`, section D):
  a message never does.
- **What variables an alliance's, a NAP's and transit rights' dialogs fill**: only the trade's were
  read (`FINDINGS-tradeui.md` B.1); here ACTOR and COUNTRY are both the proposer's name, which is what
  the texts ask for.
- The `VAL` and `TIME` an influence's and an alignment's messages carry: no text of the base game's uses
  them, so they are not given.
- How the guarantee's tooltip writes its neutrality (`DIP_TO_NEUTR`'s `$NOW$`): two places here, as the
  other neutrality lines are.
