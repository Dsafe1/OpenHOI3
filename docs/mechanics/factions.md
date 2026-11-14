# Factions: joining one, leaving one, its rules, its progress and the victory conditions

Diplomacy's last piece, after war and peace (`war.md`), read out of the original in the faction wave
(`reversing/findings/FINDINGS-factionplay.md`, sections A to C) on top of what was already known
(`reversing/findings/FINDINGS-faction.md`). The membership itself - a country's faction, the order its
members joined in, the leader being the first of them - came with the alignment triangle and with war
(`alignment.md`, `war.md` section 13). Figures are thousandths unless said otherwise; the defines are the
diplomacy block's.

## 1. A faction and its members

A faction is an ideology group's (`common/ideologies.txt`): its name, its corner of the triangle, its
`modifier`, its `neutrality` and its `rule` block. **Its members are an ordered list, and the first of them
leads it** - leadership is nothing but position (`FINDINGS-faction.md` §3). A country in no faction holds
the null faction, which is no real faction.

**Rules.** A faction's `rule = { ... }` names any of three rules, each `yes` or `no` (`FINDINGS-factionplay.md`
B.3): `limited_war`, `allience_wide_guarantee` (so spelled) and `free_resource_gifts`. A key that is no rule
is reported. In Their Finest Hour the Axis has `limited_war`, the Allies `allience_wide_guarantee`, the
Comintern `free_resource_gifts`. **A member holds exactly its faction's rules and a non-member none** - the
original copies them onto the country as a game is entered, every month, on a join and on every leave, which
comes to the same thing at every moment anything reads them. What each does is section 5.

**What a member is given** besides (B.5): the faction's `modifier` in its own (`modifiers.md`, section 4,
step 3), the triangle's place (`alignment.md`), the war ceiling's term (`diplomacy.md`), friendship for
movement and supply, and the strategic resources of section 6.

## 2. Joining: `AddCountry`

`CFaction::AddCountry(faction, country, joinOngoingWars)` (`FINDINGS-faction.md` §1.1, §1.2), every way a
country comes into a faction in play:

1. out of its old faction where it has a different one;
2. nothing more where the faction is no real one (the country holds the null faction), or where it is
   already the country's;
3. **at the back of the members**;
4. with `joinOngoingWars`, **the war sweep**: every war with somebody on each side, in the game's order of
   wars, that the country is not already in - and, for a subject, only a war its overlord is already in:
   - where the faction's **leader is an attacker and the war is not limited**, the country joins the
     attackers, and its relation with every defender falls by `WARDEC_WAR_DIPLOMACY_HIT` (100);
   - else where the leader is a defender, it joins the defenders, and its relation with every attacker
     falls the same;
5. the faction's modifier and rules are the country's from now on.

**Who passes `joinOngoingWars`**: the join and invite (section 3), `join_faction`, `create_vassal` and every
other making of a puppet (`war.md`, section 13) - every scripted or commanded join. A declaration of war
puts its victim into the enemy's faction without it, having put it in the one war itself (`war.md`,
section 5). The history's members come in without it.

## 3. Join Faction and Invite to Faction

One action serves both (`FINDINGS-factionplay.md` A): **Join Faction** - the actor asks to join the
recipient's faction - and **Invite to Faction** - the actor asks the recipient to join the actor's. The
country that moves is the **joiner**; the other, whose faction is joined, the **owner**.

**The rows** (after military access, before alignment): **Invite to Faction** where the player leads a real
faction and the chosen country is in none; **Join Faction** where the player is in none and the chosen
country leads a real one.

**Valid** - tested when the offer is made, at the answer, and every frame the player's dialog is open (A.3):
neither is a government in exile; for a join, the recipient leads a real faction and the actor is in none;
for an invite, the actor leads a real faction and the recipient is in none. **Nothing else is asked again
when the answer comes**, but the war test of the apply.

**Greyed** on the first of these that holds (A.4), the tooltip saying why:

| # | holds | tooltip |
| --- | --- | --- |
| 1 | actor and recipient are one | `ACT_NO_SELF` |
| 2 | a diplomat is on his way to them | `ENROUTE`, DATE |
| 3 | **the recipient** is a puppet | `DIP_THEY_PUPPET`, OVERLORD |
| 4 | influence below the cost - `JOIN_FACTION_INFLUENCE_COST` to join, `INVITE_FACTION_INFLUENCE_COST` to invite, both 0 | `DIP_NO_INFL`, INF to 0 places |
| 5 | join: the actor's effective neutrality above `JOIN_FACTION_NEUTRALITY` (25) | `DIP_TO_NEUTR_THREAT`, VAL to 2 places, **LIM `INVITE_FACTION_NEUTRALITY`** |
| 6 | invite: the recipient's above `INVITE_FACTION_NEUTRALITY` (25) | `DIP_THEY_TO_NEUTR_THREAT`, **LIM `JOIN_FACTION_NEUTRALITY`** |
| 7 | join: the actor not within `FACTION_JOIN_DIST` (50) of the recipient's faction | `FACTION_DISTANCE`, VAL the define to 2 places, FACTION, VALC the distance to 2 places |
| 8 | invite: the recipient not within it of the actor's faction | `FACTION_DISTANCE_2`, the same |
| 9 | join: the actor at war with a member of the recipient's faction; invite: the recipient at war with a member of the actor's | the plain description |
| - | otherwise | `JOINFACTIONDESC` / `INVITEFACTIONDESC` |

"Within" is the straight line on the triangle from the country's place to **the faction's corner** (not its
leader's place), **under**
the distance (`alignment.md`). The tooltip's distance is worked in single precision: the square in
thousandths, over 1000, its square root.

**The dialog** asks `JOINFACTIONNEWDESC` (FACTION the recipient's) or `INVITEFACTIONNEWDESC` (COUNTRY the
recipient, FACTION the actor's), then `DIP_INFLUENCECOST` with **the alliance's costs** -
`JOIN_ALLIANCE_INFLUENCE_COST` (5) to join, `LEAVE_ALLIANCE_INFLUENCE_COST` (1) to invite - to two places,
though the action charges the faction's; and the chance line, always.

**The AI's answer** is `DiploScore_InviteToFaction`'s, the recipient's AI answering, as any offer's
(`agreements.md`): **for a join the two are passed the other way round**, so the function always has the
faction's owner first and the joiner second - (owner, joiner, observer the recipient). **The player asked**
gets `INVITEFACTIONREQDESC` (FACTION the inviter's) or `ASKFACTIONREQDESC` (FACTION the player's), Accept and
Decline, among the offers made to him (`trade.md`, section 10); it declines itself two months after the
offer, or as soon as it is no longer valid.

**The apply** (A.6), for the proposal and for the answer alike, first: **where the joiner is at war with a
member of the owner's faction, nothing at all happens** - no charge, no message.
- **Proposed: nothing.** No influence is taken.
- **Accepted:**
  1. the actor pays the cost - the faction's, 0 - its influence held at no less than nought;
  2. `AddCountry(owner's faction, joiner, joinOngoingWars)`;
  3. every country whose overlord is the joiner, in the countries' order, the same;
  4. **every alliance of the joiner with a member** of the faction as it now stands ends, saying nothing;
  5. one message, by who the player is: the actor of an invite `FACTIONINVITEACCEPT`; on a join, any member
     of the faction `FACTIONJOINACCEPT`; anybody else but the recipient `FACTIONJOINACCEPTOTHER`; the
     recipient nothing. Variables `JOINER`, `FACTION` (the owner's faction) and `COMMENT` -
     `FACTIONCOMMENT_<faction>`'s text.
- **Declined:** the actor, where he is the player, is told `FACTIONJOINDECLINCE` (FACTION the recipient's) or
  `FACTIONINVITEDECLINCE` (the actor's), with `BAD_COMMENT` and `COMMENT_BAD` both
  `FACTIONCOMMENTBAD_<faction>`'s text.

## 4. Leaving

**Nobody chooses to leave a faction in a game of this kind** (B.1): the original's leave button is its custom
game's, which this project does not have (`docs/compatibility.md`, the one start). A country leaves:

| how | and besides |
| --- | --- |
| `leave_faction = <faction>`, a member of it | it first leaves every war in which the side it is on is led by the faction's leader |
| owning no province any more | nothing (`war.md`, section 11) |
| annexed, made a puppet, a leader in exile coming home | `war.md`, sections 13 and 14 |
| joining another faction | (section 2) |

Leaving takes it off the members - the next becomes leader where it led - and gives it the null faction, and
with it its faction's modifier and rules go. **Nothing else**: its wars, relations, threat and influence stay;
its place on the triangle stays where membership had put it, the leader's, and drifts from there at its next
step; its puppets stay in the faction.

**`join_faction = <faction>`** puts the scope's country in by `AddCountry` with its wars, where it is not a
member already.

## 5. What the rules do

- **`limited_war`**: the player's action list offers Declare Limited War (`war.md`, section 10). The AI's
  limited wars wait for the AI.
- **`allience_wide_guarantee`**: a guarantee given by a member is **also given by every other member of its
  faction**, each a guarantee of its own "by the faction", from the same hour; the guarantee's dialog adds
  `ALLIES_GUARANT`. A guarantee by the faction is called to arms like any other when the guaranteed country is
  attacked (`FINDINGS-war.md` A, corrected). It takes the place of a guarantee between the two already, either
  way round. And **when a member answers a call to arms, it calls every member of its faction that is not among
  the war's defenders** - whichever side it joined - against the same enemy, each call made accepted and run on
  the spot, so each joins where it may (`war.md`, section 6; a call to itself, or to one already in the war,
  does nothing).
- **`free_resource_gifts`**: two members' trade is goods for goods or for nothing (`trade.md`).

## 6. The faction's strategic resources

A country is given another's strategic resources (`modifiers.md`, section 6) by two roads of the faction's:
a major, or a country with more than 100 base IC, by any member of its faction; and **a faction's leader at
war, by any country outside the faction whose place on the triangle is within `FACTION_STRAT_BONUS_DIST`
(100) of the faction's corner** - not of the leader - strictly under, the squares compared in thousandths.
Either way the two must not be at war with each other, and their capitals must be on one continent or each
must have a naval base beside one of the coast regions `map/coastlines.txt` names (`modifiers.md`, section 6).

## 7. A faction's progress

**Progress** is, each hour, the victory points of every province **held** by a member - the controller's
faction, not the owner's - in thousandths (C.1): every faction's is set to nought, then each province adds
its points to its holder's faction. **The null faction's is never set to nought** in a game whose every
ideology group has a faction, so the provinces of countries in no faction add to it every hour without end,
and it wraps as a 32-bit figure.

**The share** (C.2) is a percentage in thousandths, cut twice:

    total  = the points of every province of the map, sea and land, x 1000
    share  = progress x 1000 / total, cut         (progress itself where total is 0)
    share  = share x 100000 / 1000, cut

So it moves in tenths of a percent: 115 of 1172 is 9.800. **`faction_progress = N`** holds where the
country's faction's share is at least N; a country in no faction asks the null faction's. The faction box's
bar is the share in whole percent, cut; its tooltip `FACTION_PROGRESS_DESC` has WHO the faction, PERCENT the
share to two places, OUR the progress whole and TOTAL the world's whole.

## 8. The victory conditions

**What they are** (C.4): each faction's section of `common/faction_aims.txt` is a list of named conditions,
each a `trigger` block and maybe `primary = yes`. A key that is neither is reported. Their Finest Hour's axis
has 33, allies 22 and comintern 30, fifteen of each primary.

**The chosen** (C.5): a new game chooses each faction's: walking its list in order, each condition is chosen
where it is primary, and each primary spends one of `NUMBER_OF_VICTORY_CONDITIONS` (15) - the walk stopping
when none is left. Their Finest Hour has fifteen primaries a faction, so each faction has its fifteen. Where
a faction has fewer primaries than the count, the original fills the rest with draws of a generator of its
own seeded from the wall clock; that is not built (*Not built*). A chosen condition starts unmet, with no
date.

**The test** (C.6) is the last thing of every day: each faction's chosen conditions in turn, each trigger
asked **with no country in scope**; a condition **is met while its trigger holds** - one that stops holding
stops counting - and is given today's date when it starts to. Then, unless the game is finished, the first
faction in the file's order with at least `VICTORY_CONDITIONS_TO_WIN` (12) met finishes the game. No winner is
kept, only that it is finished.

**What the player sees** of them: the faction box's tooltip ends "\n" + met + "/" + chosen (C.7).

## 9. The game's end

**A game finished by the conditions**, and **the clock passing the defines' `end_date`** (1948.1.1, strictly
after) though that marks nothing finished, put up the original's `VCConfirm` dialog once: "Victory!" or
"Defeat!", "§Y$FACTION$§W has achieved their victory conditions. Do you want to continue playing or resign
the current game?", Resign and Continue (C.8). **Built since 2026-11-08: `endgame.md`.**

## Kept as read, though they look odd

1. The join dialog prints the alliance's influence costs; the action charges the faction's, nothing.
2. The neutrality tooltips print the other direction's limit (both 25).
3. A row greyed by a war with a member says only its plain description.
4. On an accepted join every member is told it was "accepted into" the faction, the leader who accepted
   among them; on an accepted invite the other members are told as if it were somebody else's faction.
5. A proposal costs nothing; the cost falls on the proposer when it is accepted.
6. The joiner's puppets follow it by their overlord alone.
7. A join ends the joiner's alliances with its new fellows, its own puppets' among them, and nobody else's.
8. An answer that finds the joiner at war with a member does nothing and tells nobody.
9. The join distance and the strategic-resource distance are measured to the faction's corner, not to its leader.
10. Progress counts by holder, not owner, and the null faction's is never reset.
11. The share is cut to a tenth of a percent before it is a percentage.
12. A victory condition is met only while it holds.
13. Of two factions reaching the count on one day, the first in the file wins.

## Ours

- **A faction's rules are read off its group** wherever they are asked, where the original copies them onto
  each member as a game is entered, monthly, on a join and on a leave: the same answer at every moment either
  is asked.
- **A guarantee by the faction replaces a guarantee the member already gives the country.** The original
  makes it anyway and points the pair at the new one, the old staying in its list; here a pair has one
  guarantee.

## Not built

- **The custom game's leave and join** (B.1): there is no custom game.
- **The AI inviting, and limited wars by the AI**: the foreign minister is not built.
- **The victory conditions' random fill**, for a faction with fewer primaries than the count: the original
  draws on a generator seeded from the wall clock. Their Finest Hour never needs it; a mod that does gets
  only its primaries, reported.
- **`release_vassal`** and the liberation and puppet commands that also join a faction.

## Not established

- Whether a message variable holding a text key (`COMMENT`) is shown as the key's text: taken so here, as the
  war goal's and the government's are (`messages.md`).
