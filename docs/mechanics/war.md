# War: declaring one, who joins it, war goals and casus belli, peace and the capitulation

Diplomacy's fifth piece, after the agreements (`agreements.md`, `trade.md`, `aid.md`), read out of the
original in the war and peace wave (`reversing/findings/FINDINGS-war.md`, sections A to D) and the
surrender wave (`reversing/findings/FINDINGS-surrender.md`). It is built in two pieces, a cut the
maintainer took on 2026-10-13 ("Ok split it"): **declaring a war and who joins it** - sections 1 to 10 -
and then **peace and the capitulation**, sections 11 to 13. Figures are thousandths unless said
otherwise; the defines are the diplomacy block's.

## 1. A war

A war is two ordered lists of countries, the attackers and the defenders; the first of each **leads its
side**. Besides them it holds (`FINDINGS-war.md` A.8):

- its **name**, made when it is declared (section 7) or given by the history;
- whether it is **limited** (section 5);
- the hour it was last declared into, **`action`**;
- **the war goals**, filed by the country that holds them, each list in the order goals were added
  (section 3);
- **the call map**: for each country, whether a call to arms to it is waiting (section 6). It is not
  saved.

Two countries are at war when one is an attacker and the other a defender of one war. **A war is never
taken off the game's list**, not even when a side empties (`FINDINGS-war.md` C.13): leaving one (below)
takes a country off its side and nothing more. **So a country is at war only while it is at war with some
country** - the original's own flag, which `UpdateAtWarAndEnemies` makes pair by pair (section 4) - and
not merely while it stands on a side of a war: Italy, alone in its war once Ethiopia has capitulated, is at
peace. Until 2026-10-20 ours asked the second, and Italy stayed at war for good (`moving.md`, section 21).

**Joining a side** (`CWar::AddAttacker` / `AddDefender`): nothing where the country is there already;
otherwise it goes **to the front** of the attackers where it leads a real faction, or where it is let
take the lead (only a call to arms does) and the side's first is a subject; to the front of the defenders
only where it is let take the lead and the side is empty, or its first is nobody's played country or a
subject; otherwise to the back. Then, for each country on the other side, the two are now at war (their
influence and trade routes end, `agreements.md` section 6, `trade.md` section 8), and both are brought
up to date as **section 4** says. **A country put at the front of the attackers** has the player who led
told `ALLYTAKEOVER` "$ACTOR$ assumed leadership in our war against $TARGETLIST$." and itself, where
played, `ALLYTAKEOVERWE` "We assumed leadership in the war against $TARGETLIST$."; TARGETLIST is the
other side's names joined by ", ".

**Leaving a side** (`CWar::RemoveAttacker` / `RemoveDefender`, C.9): the country comes off its side;
every war goal it holds and every goal against it is deleted (`CWar::RemoveWarGoalsOf`); then for each
country left on the other side the two are at war no more - which, like their going to war, ends their
influence and trade routes - and both are brought up to date. Being at peace by then, the pair gets no new
`last_war`. Peace (section 11), the `end_war` effect and, to come, the capitulation take countries out so.
`end_war = TAG` takes TAG out of the war it shares with the scope, and then each of TAG's puppets out of
that war.

**The history's wars** (`history/wars`) are read with their goals: a `war_goal` block dated on or before
the start is filed under its actor (`casus_belli`, `actor`, `receiver`, `country`, `region`), with no
test of the goal. Whether a history war is limited (`limited_war`) is read too. The base game has one
war on the start date, Italy against Ethiopia, with Italy's `puppet` goal on Ethiopia.

## 2. Casus belli types

`common/cb_types.txt` (Their Finest Hour's is in `tfh/common`): one block a type, and `peace_order`, a
list of type names. A type's keys and what reads them (`FINDINGS-war.md` B.3, the table):

| key | what it is | read by |
| --- | --- | --- |
| `sprite_index` | the frame of the goal's icon | the declare-war dialog |
| `is_valid` | conditions, in the **target's** scope with THIS the holder | whether it may be used (below), whether a goal of it is valid |
| `allowed_countries` | conditions on a third country the goal names | the goal's tests and the dialog's country list |
| `allowed_regions` | conditions on a region the goal names | the goal's tests and the dialog's state list |
| `allowed_province` | conditions on a province the goal takes | the goal's availability; its enforcement (piece 2) |
| `threat` | threat spread when the goal is enforced | piece 2 |
| `always` | usable without being held | whether it may be used |
| `handles_peace`, `po_*` | what enforcing the goal does | piece 2 |
| `on_add` | effects run when a goal of it is added, in the actor's scope | section 3 |
| `on_completion` | effects run when the goal is enforced | piece 2 |
| `is_civil_war` | answered to Lua only | - |
| `is_valid_join`, `prerequisites`, `is_triggered_only`, `mutual`, `months`, `truce_months`, `peace_cost_factor`, `penalty_factor`, `war_name`, `country_desc`, `prov_desc` | read and never used | - |

A type's **rank** is its place in `peace_order`, from nought; a type the list leaves out ranks nought and
is reported. **Type 0 is the null type**, which no test sees as real: the types are numbered from 1 in
the file's order. `po_concede_defeat`, which the file's comment names, is not a key the original knows
and is reported as one.

**A casus belli held** is one country's against another: its type, and an end hour or none. It comes from
the history's diplomacy (none in the base game) and from two effects (section 9), **which always make the
null type** - so **holding one made in play changes nothing**: every test below asks for a real type.

**Whether a country may use a type against another** (`DiplomacyStatus_CanUseCasusBelliType`): it holds
one of that type against the other, or the type is `always`; and the type's `is_valid` holds in the
other's scope with THIS the country. **Whether it has a casus belli on another at all**
(`HasUsableCasusBelli`): some real type, in the file's order, it may use. In Their Finest Hour every type
but `civil_war` is `always`, so this comes down to "some type's `is_valid` holds".

## 3. War goals

A war goal is a type, its actor, its recipient, and - where the type asks - a third country and a region.

**Whether a goal can still be chosen** (`CWarGoal_IsAvailable`, B.2): not where the type is not real;
not where `allowed_countries` has conditions and no third country is set; where the type needs a region
and none is set, where some province of some region passes `allowed_regions` in the province's scope
with THIS the actor and the war, if any, has no such goal with that region; otherwise, where
`allowed_province` has conditions, where one of the recipient's provinces passes it in the province's
scope, THIS the actor and FROM the recipient - and, at war, the goal is not already among the actor's;
otherwise where the war, if any, has no such goal. **"Such a goal"** is one of the same type, actor,
recipient, third country and region.

**Whether a goal is valid for a war** (`CWarGoal::IsValidForWar`, A.10): the type is real; the recipient
owns a province; where `allowed_countries` has conditions, the third country passes them; where
`allowed_regions` has conditions, a region is set and one of its provinces passes; and the war, if any,
has no such goal.

**Adding a goal to a war** (`CWarGoal::AddToWar`): only where the type's `is_valid` holds for the
recipient with THIS the actor; filed under the actor; and the actor may not add another against the
recipient **until a month on** (`WARGOAL_ADD_COOLDOWN`, in months: the next hour is a month on, the hour
of the day kept, and the test is strictly before). **A declaration and the Add war goal action** then run
the type's `on_add` in the actor's scope and **spend one casus belli the actor holds on the recipient of
the goal's type**, silently - where there is one; the `war` effect's further goals and `add_wargoal` are
only added.

**The text of a goal being set up** (`CWarGoal::BuildSetupText`): `<type>_setup`, ACTOR and RECIPIENT
as "§Y name §W"; where the text asks for THIRD and none is set, the whole text is
`WARDIALOG_SELECT_COUNTRY` (which has no text); where it asks for STATE and none is set,
`WARDIALOG_SELECT_STATE` "Select a §Yregion claim§W."; otherwise "§W" and the text; nothing for a null
type.

## 4. At war, the truce and mobilisation

Whenever a country joins or leaves a side, **each of the two brought up to date**
(`CCountry::UpdateAtWarAndEnemies`, A.9): for every country it is at war with, its record towards that
country stamps **`last_war`, the hour now**. The same runs for every country as a game is loaded.
**Nothing else stamps it** - not the hour, not the war's end (`FINDINGS-war.md` C.11).

**A truce** with another country: not at war with it, and `last_war` no more than 2400 hours ago and not
in the future (`last_war <= now < last_war + 2400`). So a truce is the hundred days after the last
change of membership in a war between the two, and **a war that runs a hundred days unchanged leaves
none**. A new game stamps nobody: its history's wars have no truce until something changes.

**Mobilisation**: a country brought up to date that is at war and **not yet mobilised** is given
`initial_mobilization` - a static modifier, in Their Finest Hour `reinforcement_bonus = 1.0` - for
`MOBILIZATION_BONUS_DAYS` (30), and is mobilised. One that is at war with nobody is taken off it, and
is no longer mobilised, **only where nobody plays it**.

## 5. Declaring a war

Declaring is an offer (`agreements.md`) whose kind is a declaration: **`value` set is a war, unset a
limited war**. It is never answered. It carries the war goal it is declared with.

**Whether the row may be pressed** (`CDeclareWarAction::IsSelectable`, read whole): not on itself; the
recipient owns a province; the actor has `WARDEC_INFLUENCE_COST`; no diplomat on his way; not at war with
it, not allied to it; the recipient is not `REB`; no truce; not one real faction; **where the recipient is
a puppet, the actor is at war with its overlord**; **where the actor is a puppet, the recipient is its
overlord**; not the actor's own puppet; every NAP it would break - with the recipient, the recipient's
faction leader, its overlord, and each of its allies - may be broken (`agreements.md`, NAP); the actor's
effective neutrality is not above the ceiling for war (`diplomacy.md`, with `NEUT_REDUCTION_AT_CLAIMS`
added where the actor may use `aquire_all_cores` on the recipient and its goal is available); and **no
land unit of the actor stands in a province the recipient holds**.

**Whether the command does anything** (`IsValid`): not at war, not allied, not one real faction, and **its
war goal valid for the war** (section 3) - so a declaration with no real goal does nothing. The `war`
effect does not ask (section 9).

**What a declaration does** (`CDeclareWarAction::Apply`, A.2), in order:

1. **Where the actor is in a real faction**: the first country it is at war with that leads a real
   faction of its own, where the recipient's faction is not real or is that one - and the war between
   them is kept. Where the recipient is in no real faction, **it is put into that country's faction**.
2. The alliance and the NAP between the two end - no message, no relation of their own.
3. *(An undeclared war between them would end; not built.)*
4. **The war**: the kept war, the recipient joining **the side opposite the actor**; otherwise **a new
   war**, the actor its first attacker and the recipient its first defender, put on the game's list.
5. **The goal** is added (section 3).
6. The war's `action` is now.
7. **A new war only**: its name is made (section 7), and a limited declaration makes it limited.
8. Relation between the two: `-WARDEC_WAR_DIPLOMACY_HIT` (-100), new or kept.
9. **A new war only**: threat - the recipient's capital's neighbourhood fears the actor by
   `WARDEC_BELIGERENCY` (33), as threat spreads over the map (`diplomacy.md`). **Joining a kept war
   spreads none.**
10. The actor's influence: `-WARDEC_INFLUENCE_COST` (nought in Their Finest Hour), held at nought.
11. **Every other country, in the countries' order**, as section 6 says: the recipient's puppets and the
    actor's join at once; the recipient's guarantors are called and join at once.
12. Military access both ways is cleared; the guarantee between the two goes; the debt permission
    both ways is cleared (`aid.md`, section 1).
13. **Units**: for the two, every idle unit standing on ground with an enemy looks at once for a battle,
    and one alone in an enemy's province takes it, as an hour's look does (`landcombat.md`).
14. **The message** (section 8).
15. The recipient's overlord is called; the factions join; the allies are called (section 6).

**A limited war** is a new war declared with `value` unset. Once limited: the declarer's allies are not
called, its faction does not join, every call the war's allies are sent is a call that asks (`value`
set), and the screen's call to arms can be chosen only in it (section 6). A limited declaration that joins
a kept war leaves the war as it was. The defending side comes in as in any war.

## 6. Who joins, and who is asked

At once, with each joiner on the side named:

- **the recipient's puppets** - every country whose overlord is the recipient and which is not already
  on the recipient's side - on the recipient's side;
- **the actor's puppets**, the same, on the actor's side;
- **the recipient's guarantors** - every country that guarantees the recipient, not yet listed and not at
  war with the recipient - by **a call to arms made already accepted** and carried out on the spot, so
  they join as an accepted call does (below), and are listed whether it did anything or not;
- **the factions** (`BringInFactions`): on the attacking side, the members of the attacking country's
  real faction but the two and those already attacking - **only where the war is not limited**; on the
  defending side, the members of the defending country's real faction but the two and those already
  defending. A member joined so that is played is told `FACTIONWAROFF`.

Asked, by **a call to arms offered** and executed at once - which puts it on the called country's list
of offers:

- **the recipient's overlord**, where the recipient is a puppet and its overlord is not yet listed;
- **the allies** (`CallAllies`): for the actor against the recipient **only where the war is not
  limited**, then for the recipient against the actor - every country not listed and not in the war,
  that has an alliance with the caller and is not at war with the enemy; **the call's `value` is the
  war's limited flag**.

**A call to arms** is an offer whose kind is a call, with a third country, the enemy (`Versus`). `value`
unset is the screen's call; set, a call made by a declaration or the AI's.

- **Valid** (`IsValid`): the recipient is not at war with the enemy; the caller is; the enemy is not in a
  real faction the recipient shares; the recipient is on neither side of the caller's war; and, for a
  proposal, no call to the recipient waits in the war's call map.
- **Selectable** (the row): not on itself; the caller at war with the enemy; the enemy in no real faction
  the recipient shares; **the war limited**; `CALLALLY_INFLUENCE_COST`; both sides non-empty; the
  recipient no subject and the caller first of its side, or the recipient the caller's puppet and the
  caller in the war, or the recipient another's puppet, the caller first of its side and the puppet's
  overlord already on it; and the recipient has no NAP with a real country on the side the caller
  fights.
- **Proposed**: the war's call map marks the recipient; **`value` unset only**, the caller pays
  `CALLALLY_INFLUENCE_COST`, held at nought.
- **Accepted**: the mark cleared; the recipient's alliance, guarantee and NAP with the enemy end; it joins
  **the caller's side**, let take the lead where the side's first owns fewer provinces than it, but not
  where it is a subject, and always where it is the caller's overlord; access both ways between it and
  the enemy cleared; joining the attackers and not the caller's puppet, it is given a casus belli of the
  null type on the enemy, which at once with every other on that record is taken off it again; relation
  between the enemy and it, `-WARDEC_WAR_DIPLOMACY_HIT`. Messages: the caller, `MILALLHONOUR`; the
  recipient, `WEMILALLHONOUR` for a call with `value` set, otherwise `FACTIONWAROFF` (joined the
  attackers) or `FACTIONWARDEF` (the defenders); anyone else, `OTHERMILALLHONOUR`.
- **Declined**: the mark cleared; relation, `ALLIANCE_REJECT_RELATION_CHANGE` (-50); the caller is given
  a casus belli of the null type on the recipient; **the alliance between them ends**, and only where
  there was one: the caller `MILALLDISHONOUR`, the recipient `WEMILALLDISHONOUR`, anyone else
  `OTHERMILALLDISHONOUR`.

**The AI's answer** is the install's `DiploScore_CallAlly(ai, actor, recipient, observer, action)`, the
recipient's AI answering, as every offer is answered (`agreements.md`, section 2). **A call to the
player**: one with `value` unset is accepted for him unasked; one with `value` set puts the call to arms
on his bar of offers (kind 6) while the caller is at war with the enemy and he is on neither side, and
otherwise is dropped.

**The call to arms' dialog** (`CAskJoinWar`, `DefaultDialog` as every offer's): the title is the
action's; the description `CALLREQDESC` with COUNTRY the caller's name and LIST the names of the side it
fights, then one line by what ties the player to the caller - `WCRD_ATT` "This is a war that they have
started." where the caller attacks (or is on neither side); otherwise, where the caller is allied to the
player `WCRD_DEF`, where it is the player's puppet `WCRD_VAS` (which has no text), where the player
guarantees it `WCRD_GUA`, else `WCRD_WARNING` with WHO the enemy. Then, where the enemy has allies,
`CW_WARNALLIES2` (ENEMY its name, COUNTRY the allies); and only where the caller attacks: where the enemy
has guarantors `CW_WARNGUARANTEED2`, and where it is a puppet not allied to its overlord
`CW_WARNOVERLORD2` with COUNTRY the overlord's name and " (@TAG)". The lines follow one another with
nothing between. Buttons `ACCEPT` and `DECLINE`. It closes, declining, after two months, or as soon as
the call is no longer valid.

## 7. The war's name

`AGRESSION_WAR_NAME` "$ORDER$War of $FIRST$ Aggression", FIRST the first attacker's adjective. ORDER is
empty for the first such war, and for the nth "n" + the suffix + " ", the suffix `TH`, `ST`, `ND` or `RD`
by n's last digit, 11 to 13 taking `TH`: n is the least for which no war on the list - fought or
finished - has the same name. **No casus belli's `war_name` is read.** `NORMAL_WAR_NAME` is the revolt
war's, not built.

The original compares the names as text. Here a made name is held as its parts (`CWarName`) and two are compared part
for part, which comes to the same; a save holds the text, and the loader reads a name a declaration made back into its
parts, so a war declared after a load is named against the loaded ones (`savegame.md`, section 10).

## 8. Messages

Only the player is told. **A declaration**: the declarer is told `WEDECLWAR` "We declared war on
$DEFENDER$." (log), with the `declarewar` sound; the recipient `DECLWAR` "$AGRESSOR$ declared war on us!"
(popup and log; the popup reads "WAR!", $MONARCHTITLE$, "$BADWORD$ in", "$AGRESSOR$ declared war",
"upon us! Prepare for battle...", $LIM$), with `declarewar_onus`; anyone else `DECLWAROTHER`
"$AGRESSOR$ declared war on $DEFENDER$." (log). AGRESSOR is the actor's name, DEFENDER the recipient's,
MESSENGER `MESSAGE_HEAD_MARS`, BADWORD `MESS_BADWORDS1`, `2` or `3` by the actor's place in the countries'
order - its index, from nought - modulo 3 plus one, and LIM `DECLWAR_LIMITED` "This war is a limited
war." where the war is limited **and the declarer is in a real faction**, empty otherwise.

**`FACTIONWAROFF`** to a faction member brought in: ACTOR the declarer, VICTIM the recipient, FACTION the
faction's name, COUNTRY the member's adjective.

## 9. Script

- **`war = TAG`** (with `attacker_goal`, `defender_goal`, `limited`): nothing where the scope's country is
  already at war with TAG, or TAG is `REB` - the goals neither; otherwise a declaration with `value` the
  opposite of `limited`, carrying the first attacker goal (actor the scope, recipient TAG), **carried out
  without asking whether it may be**; then, where the two are at war, the other attacker goals and every
  defender goal (actor TAG, recipient the scope) are added. No tag, or one nobody knows, is **the
  controller of the scope's province**. `call_ally` is read and does nothing.
- **`add_wargoal = TAG`** (`war_goal = { ... }`): where the scope is at war with TAG, the goal, actor the
  scope and recipient TAG, is added.
- **`casus_belli = TAG`**: the scope is given a casus belli of the null type on TAG, every time, never on
  itself. **`add_casus_belli = TAG`**: TAG is given one on the scope.
- **`end_war = TAG`** takes TAG and then each of its vassals out of the war it and the scope share, each
  leaving as section 1 says.
- **`surrender_progress = N`**: the scope's surrender progress (section 12), 0 to 1000, a hundred times over,
  is at least N in thousandths - so N is on a scale of 0 to 100.
- Conditions: **`war_with`** as before; **`truce_with = TAG`** a truce (section 4); **`casus_belli =
  TAG`** whether the scope has a casus belli on TAG (section 2), THIS read straight; **`has_wargoal =
  TAG`** at war with TAG and holding a goal against it in that war, whatever its type; **`has_cb = {
  type = T actor = A }`** whether a country at war with the scope holds a goal of type T against it -
  the actor's war first, then every enemy owning a province: `actor` restricts nothing.

## 10. The screen

**The rows** (`agreements.md`, section 7), at peace with the chosen country: **Declare War**, where there
is no alliance and they are not in one real faction; **Declare Limited War**, the same and where the
player's faction has the `LIMITED_WAR` rule. That the row asks the faction's rule is the reading's
inference - it reads a byte of the country's that stands where that rule's would - and **the maintainer's
screenshot of the original, 2026-10-13, shows the row**, greyed under Declare War, for a player leading a
faction (its list has Invite to Faction); which faction was not said. At war: **Peace** (section 11) and
**Add war goal**, in that order, after the technology row and before the alliance. And **A Call to Arms!** towards
an ally or a member of the player's real faction, against the first country the player is at war with
in a limited war that the chosen country is not at war with.

**Declare War's tooltip**, the first that holds: `ACT_NO_SELF`; `HAS_TRUCE_WITH` "Have truce with " and
the recipient's name; `ENROUTE`; then, of the NAP it would break - the first not breakable among the
recipient's, its faction leader's, its overlord's and its allies', or else the recipient's own - with
none: `NO_WAR_ALLIED` where they are in one real faction or allied, `DIP_NO_INFL` (INF to two places),
`DIP_WAR_NEUTR` with NEUT the effective neutrality, THEM the recipient's name and THREAT the ceiling,
both to two places, `NO_WAR_MILITARY_ACCESS` where the actor has access into the recipient, else
`ACT_WARDESC`; with one: `DECLARE_WAR_NAP_LIMIT_DATE` while it is unbreakable, with the date it becomes
breakable, `DECLARE_WAR_NAP_LIMIT_TROOPS_BALANCE` in a window whose balance is not reached, else
`DECLARE_WAR_NAP_BREAKABLE`. A limited declaration adds "\n" and `LIMITED_WARDESC`. **Add war goal's**:
`DIPL_NOT_AT_WAR` (no text) with no war, `CAN_NOT_ADD_WARGOAL_UNTIL` with the date while it waits,
else nothing; it is also greyed while a diplomat is on his way, with no line saying so. **The call to
arms'**: `ENROUTE`, `DIP_NO_INFL` (two places), where the caller leads its side and the recipient has a
NAP with a real country on the other side `CAN_NOT_JOIN_WAR_NAP` with the first such, in the countries'
order, otherwise `CALLDESC`; its dialog is the standard one, `CALLNEWDESC` and the cost line.

**The declare-war dialog** (`DeclareWarDialog` in `interface/wardialog.gui`, `FINDINGS-war.md` B.2), for
Declare War, Declare Limited War and Add war goal: its title the row's; the two shields; `Description`,
the goal's setup text, made again every frame; three panes in one place - `wargoal_setup` with the goal
list, `wargoal_state_select` and `wargoal_country_select`, each with its list and a `cancel_select`.

- **The goal list**: every real type in the file's order whose `is_valid` holds in the recipient's scope
  with THIS the player, that the player holds or that is `always`, whose goal - the type, the player, the
  recipient - is available, and, where `allowed_regions` has conditions, for which some province of a
  region `useForPeace` passes `allowed_regions` with THIS the player and FROM the recipient. A row,
  `wargoal_item`, is `select_goal` with the type's name and `wargoal_icon` at frame `sprite_index`.
- **Choosing a goal** sets it, the setup pane hidden. Where `allowed_countries` has conditions the
  country list opens: every country for which they pass in the recipient's scope with THIS the player
  and FROM the candidate. Else, where `allowed_regions` has conditions, the state list: every
  `useForPeace` region, in the map's order, with a province passing in its scope with THIS the player and
  FROM the recipient and whose goal with it is available, by the region's name. Else the setup pane comes
  back. Choosing a country opens the state list where regions are needed, otherwise the setup pane;
  choosing a region goes back to it; `cancel_select` sets the null type and goes back.
- **`AgreeButton`** "I do" can be pressed exactly while the declaration with that goal is valid (section
  5); it posts it **accepted** and closes. **`DeclineButton`** "Rather not" closes. **No cost, threat or
  warning is shown.**

## 11. Peace

Read in `FINDINGS-war.md` C; what changing hands does in `FINDINGS-surrender.md` D.

**The offer has no terms.** It is an offer of the kind peace, actor and recipient and nothing else, made
from the Peace row: it costs no influence, sends the diplomat for seven days as every offer does, and
**the recipient's AI answers it** at the next hour (`agreements.md`, section 2) by the install's
`DiploScore_PeaceAction`, which weighs strength through `CAIIntel` (`lua.md`, section 7): over 30 and under
a draw of the game's generator, it is accepted. **The dialog's word for that answer rolls the game's
generator too**, as the original's does - here on a copy, put back after, so that what a screen shows
never moves a game's rolls (*Ours*). **Only a person offers peace**: no script of the original's makes one, so a war between two
countries nobody plays ends only by a capitulation (section 13) or by script. An offer to a player is put
to him by the original's own dialog - which only another player's offer could bring, and there is only one
player here.

**Valid** - its command does anything - where the two are at war. **Its row may be pressed** where it is
not to itself, no diplomat is on his way, they are not both in real factions, they are at war, and each of
the two - the actor first - may speak for its side:

- one in a real faction it does not lead, on a side of two or more, may not where its faction's leader is
  at war with the other, nor where it is not the first of its side;
- one in no real faction must be the first of its side;
- so a faction's leader may negotiate from anywhere on its side, and a faction's member alone on its side
  may too.

**Its tooltip**, the first that holds: `ACT_NO_SELF`; `ENROUTE`; `PEACE_FACTION` "You are both in factions,
and factions fight until they surrender"; `PEACE_NOT_LEADER` "You can only negotiate with the leader of
your enemy"; otherwise `PEACEDESC` "Offer peace with minimal border adjustments." It does not ask whether
they are at war. **The dialog** is the standard request (`PEACETITLE` "Peace") with the word for the AI's
answer and `PEACENEWDESC`: "Do you wish to request peace with $COUNTRY$. $SELF$?\nIf they accept, the war
will end. $RESULT$", SELF being `PEACENEWALLY` "They are negotiating for their allies." where the recipient
is the first of either side, else `PEACESELF` "They are negotiating for themselves.".

**What changes hands.** The negotiators are the actor and the recipient, the attacking side's first - the
actor, unless it defends. For each country of one side, in the side's order, each province it owns and does
not hold, in the provinces' order, is listed once **for each of its cores** held by the other side's
negotiator or by a country of the other side: where the province is held by the negotiator it goes to the
negotiator; held by another country of that side, to **the first such core holder**, who need not hold it;
held by anyone else, nowhere. **RESULT** is `PEACENEWWITHE` "All territory will revert to its prewar owner."
where nothing is listed, otherwise the attackers' losses and then the defenders': for each country gaining,
in the order first met, for each country losing to it, in its side's order, `PEACENEWPROVUS` "§Y$NATION$§W
will cede §R" where the loser is on the actor's side, else `PEACENEWPROVTHEM` (the same, green), the
provinces as "A, B and C" (`AND`, "and "), "§W " and `PEACETO` "to §Y$REC$§W", and a line's end. A
province listed twice is named twice.

**Accepted**, in order: the provinces change hands - the defenders' losses first, then the attackers' - each
to its new owner with consequences, not peacefully; **the first attacker and each defender give back what
they hold of each other**, with consequences, while the war still stands; then the war ends - **where both
sides have two or more, every defender and then every attacker leaves it; otherwise the lone attacker leaves
if there is one alone, and the lone defender** - so either way every attacker-defender pair is at peace, and
a side of two or more facing one country stays in the war, alone; and each played country that made the
offer or is still among the attackers is told `PEACEACCEPT` "Peace! / $MONARCHTITLE$ / §Y$RECIPIENT$§W /
have ACCEPTED / an end to the war with $ACTOR$.", log "$RECIPIENT$ has accepted the peace offer from
$ACTOR$.". **Declined**: the actor's player is told `PEACEDECLINE` "War Continues! ...", whose log line is
the original's English about debt. **Nothing else**: no relation, threat, neutrality or influence moves, no
war goal is enforced - leaving deletes them - casus belli are kept, and nobody gets a new truce, the pair
being at peace before they are brought up to date (section 4).

**Changing hands with consequences** (`Province_SetController`, `FINDINGS-surrender.md` D.2), as far as it
is built: where the controller is not the same and somebody owns the province, and not peacefully -

- where the new holder is an enemy of the old - at war, or either the rebels - each building there is any of,
  in the buildings' order, takes **a draw of the game's generator** and, under
  `COMBAT_PUSHBACK_CHANCE_FOR_DAMAGE` (50) per cent, loses `COMBAT_PUSHBACK_DAMAGE` (1) of its working level,
  to no less than nought; so **a province a peace gives back is damaged as if retaken**, the war standing;
- the province's production of money and the four resources is nought, enemy or not, until the day's repair
  brings it back (`bombing.md`);
- where it is the old holder's acting capital (section 14), **a quarter of the old holder's money and
  resources** - out of its stockpile - goes to the new holder's stockpile; supplies, fuel and the rest stay;
- and, with consequences, peaceful or not, the old holder's government is seated again at once.

And with consequences or without, before the new holder is written, the new holder's occupation policy towards
the old is set where it has none (`occupation.md`, section 2).

A battle's capture goes through it so (`landcombat.md`, section 7), as do a peace, the day's return and the
effects that move land.

**A change of owner** (`CMapProvince::ChangeOwner`, D.3) sets the controller first with the same flags,
then the owner - never the rebels; where the old owner was a country, the province's nationalism is set to
`YEARS_OF_NATIONALISM` (`occupation.md`, section 5); the modifiers events gave the province go - both whether
or not the owner changed; the old owner, owning nothing now, leaves its faction; **a new owner gets no core**.

**The day's return of occupied land** (`FINDINGS-war.md` C.10), in each province's day after its repair:
a province held by a country its owner is not at war with goes back to its owner, with consequences and
not peacefully - so production stops for the day and a capital is looted - except where either is the
rebels or the owner is a government in exile.

## 12. The surrender progress

How near a country is to surrendering (`CCountry::GetSurrenderProgress`, `FINDINGS-war.md` D.2), 0 to
1000, **1000 being the brink**:

- **1000** for a country holding no province; **0** for a government in exile;
- otherwise, with unity its national unity in tenths of a per cent (truncated), 1000 where that is nought,
  else **the occupied share of its owned cores' victory points**, in thousandths, times 1000 over unity,
  held between 0 and 1000 - so a country surrenders once the occupied share reaches its national unity.

**The occupied share** is 1000 less the held share: of the country's cores that it owns, the victory points
it also holds over all of them - **a core it does not own counts in neither**, and one owning no core is at
0 however much it has lost. **The two sums are counted once a day**, before any country's day, with its
neighbours (`CCountry::RebuildNeighbours`).

`GetSurrenderLevel` hands the progress to Lua, over 1000, and `surrender_progress` asks it (section 9).
No save keeps the progress: each country's `last_surrender` is "1.1.1.0" in every save there is.

## 13. The capitulation, annexation and the puppet

Read in `FINDINGS-war.md` D and `FINDINGS-surrender.md` A and B.

**The surrender** (`CCountry_Surrender`, `FINDINGS-onactions.md` 2.2), late in each country's day, after its
shares and before its convoys are seen to: a country owning a province, at 100% - progress 1000 (section 12),
which, the victory points being counted at the day's head, is reached the day after the land changes hands -
at war, and thirty days or more past its last surrender surrenders to **its victor**, the enemy scoring highest, the
first of equals - 1000, or 2000 for the leader of a real faction; 5000 more where it holds the country's
declared capital; and for each province of the country's it holds 1000, 3000 a victory point and a third of
the province's industry. `on_surrender` fires first (THIS the country, FROM the victor - the base game comments
every entry out), then the capitulation, and the last surrender is now. So a country surrenders the first day
it is at 100%, and **only one still at 100% and at war after its capitulation surrenders again, thirty days on**.

**The capitulation** (`CCountry_Capitulate`, D.3), in order:

1. The war the victor and the country share; none, nothing.
2. The goals the other side holds, each of its countries' in its order, sorted - **by the original runtime's own
   sort, which is not stable** (`CStdSort`) - by the types' `peace_order` ranks, the victor's first of equals.
3. **Every goal against the country that does not annex** is enforced, in `peace_order`, where it passes the
   gate - its type real, the country owning a province, `allowed_countries` and `allowed_regions` holding -
   **then every one that does (`po_annex`)**, in `peace_order` again: annexation always last. In Their Finest
   Hour only `civil_war` and `conquer` annex, and `peace_order` puts `conquer` before `install_communism`,
   `install_democracy` and `puppet` - which are still enforced before it (`FINDINGS-war.md` D.3, steps 4 and 5). A goal enforced makes its actor one who made peace, or one who fights on; a
   goal against the country's allies is not looked at.
4. **No goal passing**, a `conquer` goal of the victor's is made up and enforced, unasked.
5. **Nobody fights on**: the country leaves the war, and each of its vassals in the war is **freed** where some
   goal of the war names it - its alliance and bond with the country end, and access, guarantees and leave to
   run a debt between them; it stays in the war - else **made the victor's puppet** by a `puppet` goal, unasked,
   and leaves the war. **Somebody fights on**: the country stays in the war, and every goal by or against it and
   its vassals is deleted.
6. What it holds of a country no longer at war with it, or of a government in exile whose war with it is this
   one, goes back to the owner, with consequences.

A country made a puppet by a goal has left every war already and freed its own puppets in being made one, so
step 5 finds no vassals of it - the reading's composition, `FINDINGS-surrender.md` B.4.

**A goal enforced** (`CWarGoal::Enforce`, D.4) answers whether its actor made peace - not where its type
`handles_peace` - and does, **in this order whatever the file's**:

- **`po_annex`**: a draw of 80 in a hundred, **taken every time**. Where the country's real faction has a
  **host** - its first member but itself, not the rebels, major or with base IC over 100, owning land, not in
  exile - and the draw holds, or the country is a puppet whose overlord owns land, or it is **Belgium or the
  Netherlands**, the actor takes control - not ownership - of the country's land on the actor's capital's
  continent or beside ground it holds, and **the country goes into exile** (section 14), the goal answering that
  the actor fights on. Otherwise **the country is annexed** - and where it had no host, every government in exile
  of its faction with it.
- **`po_demand_region`**: the region's provinces it owns go to the actor, with consequences.
- **`po_demand_provinces`**: each province it owns that passes `allowed_province` (its scope, THIS the actor) goes
  to the actor.
- **`po_puppet`**: the country is made the actor's puppet.
- The type's **`threat`** spread from the actor's capital; its **`on_completion`** run in the country's scope, THIS
  and FROM the actor; and **the message** - `THEYSURRENDERUS` "We enforced / $WARGOAL$ / on $TARGET$" to the
  actor's player, `WESURRENDER` "$AGRESSOR$ enforced / $WARGOAL$ / on us" to the country's, `THEYSURRENDER` to
  everybody else.
- `po_install_government` and `po_change_government` do nothing, in the original too.

**Annexation** (`AnnexCountry`, `FINDINGS-surrender.md` A) - `inherit = TAG` and a capitulation's - in order:
**everybody is told `ANNEX`** "$ACTOR$ annexed $RECIPIENT$"; the country leaves its faction, the next member
leading where it led; **each province it owns goes to whoever holds it**, or to the annexer where it holds it
itself or the rebels do, with consequences; its building queue is deleted; **every unit it has is taken out of the
game**, its leaders kept; the land it held of others goes back to the owners - an exile's to its faction's leader;
and every other country leaves its wars with it, ends its alliance, bond and guarantee with it and its trade routes,
has its threat both ways fall by 9999, to no less than nought, and forgets it - NAP, embargo, influence, alignment,
relation, access, debt, truce and casus belli. **Nothing marks the country dead**: it keeps its stockpile, manpower,
technology, leaders, capital and laws, and owning nothing is what being dead is. Nobody's cores change.

**The puppet** (`MakeVassal`, `FINDINGS-surrender.md` B) - `create_vassal = TAG`, `po_puppet` and the exile's
swap - in order: the vassal's provinces carrying an overlord's core that either holds go to the overlord,
peacefully; its own cores the overlord's side holds come back to its control; it leaves its faction and **every
war**; **every agreement it has with anyone ends** - alliances, bonds (**its own puppets go free**), NAPs and
embargoes, its aligning and others' influence on it, access, guarantees and debt both ways; it is allied to the
overlord and stands where the overlord stands on the triangle; **where the overlord's faction is real it joins at
the back, and with it every war the faction's leader fights** - beside the leader, as an attacker only where the war
is not limited, its relation with each enemy falling by `WARDEC_WAR_DIPLOMACY_HIT` - so **a puppet joins its
faction leader's wars, not its overlord's**, and with no faction none at all; it becomes a puppet; the relation
both ways is the overlord's raised by 500, held to 200; and it takes the overlord's government. Nobody is told.
`create_vassal` does nothing where TAG is already the scope's puppet or owns nothing.

A faction's members are kept in the order they joined, the history's in the countries' order: the first leads
(`CFactionMembership`).

## 14. The government in exile and the acting capital

Read in `FINDINGS-surrender.md` C and D.5, and `FINDINGS-capitalreaders.md`.

**Two capitals.** The **declared** capital is the history's, and moves only when the country stops owning it: at
the head of its day, the province of its own scoring highest - its manpower in whole men plus one, a hundred times
over for one of its cores - the first of equals; occupation never moves it, and a country owning nothing keeps it.
The **acting** capital is where the government sits, and so **whose pool is the country's stockpile**. It is the
declared one until that province is held by somebody else; then, **at once** where it changes hands with
consequences, and otherwise within the hour:

- held by the country: the government goes back to **the history's first capital** where that differs and is
  held again, otherwise stays;
- lost, the country holding nothing: it sits in its faction leader's seat, where a real faction is led by another;
- otherwise the best province the country owns and holds - victory points and IC, times the provinces of its supply
  area, three times on the seat's continent - the first of equals; failing any, the best it holds whoever owns it;
  failing that, it stays.

A history `capital =` line and the `capital` effect set both. **Which of the two each rule reads** is the original's,
site by site (`FINDINGS-capitalreaders.md`): **the acting capital** for production, IC, supply, depots, convoys,
trade, threat spread, alignment, strategic resources, `overseas`, unit placement, a shattered unit's home,
`capital_scope` and Lua's; **the declared capital** for `capital`, `is_capital`, `continent = this/from` and
`$CAPITAL$`, the diplomacy screen's continents, the neutrality ceiling's and lend-lease's continent tests, a NAP
broken, a trade convoy's reference harbour and the possible vassals.

**Going into exile** (`SetGovernmentInExile`, C.2), from a capitulation's `po_annex` or `form_government_in_exile =
yes`, in order - nothing for one in exile already: its own money is nought and **its stockpile is its own pool from
now on** (`pool_in_exile`), what lay in its seat's pool staying there; **manpower and officers cut to a tenth**; its
building queue deleted; every unit standing on ground held by a country not its friend - itself, its overlord or
puppet, a puppet of its overlord, its faction, an ally, a country fighting beside it, one giving it access - taken
out of the game, **at sea none**; where it led its faction, the member of greatest IC, not in exile, put at the
front; where another leads, **its government sits in that leader's seat**; and where it then does not own its
seat, every trade route it has ends. Nobody is told.

**An exile** never surrenders - its progress is 0; has its occupied land returned by no day; **anything it takes in
battle goes to its faction's leader**, where the faction has two or more and it is not the leader; ground freed whose
owner is an exile stays with whoever freed it; may not offer an alliance or be offered one, give or take leave to run
a debt, lend-lease, or trade unless it owns its seat. Each day **it hands its supplies and fuel to whoever holds its
seat**, into that country's ledger and the seat's pool; every hour where an enemy holds its seat it moves to its
leader's.

**An exile ends**: by `form_government_in_exile = no`; where it leads its faction, at the day's end, leaving the
faction; and by **the puppet swap**, early in its day - where a country not its enemy, of another faction (two
countries in none count as one faction) and with no truce with it holds its declared capital and half of what it
owns, it is made that country's puppet and comes home, everybody told `GIESWAP` "Regime Change / $VICTIM$ is now a
puppet of the $FACTION$." or its kin. **Coming home** sets the government back in the declared capital; nothing
that was cut comes back. `government_in_exile = yes|no` asks it, and Lua's `IsGovernmentInExile`.

`form_government_in_exile` with a FROM first hands the land the scope holds on FROM's capital's continent, or
beside FROM's ground, to FROM where FROM is at war with its owner, and otherwise back to the owner.

## Kept as read, though they look odd

1. A widened war gets no threat, no name and no limited flag; the relation hit and the influence are paid
   all the same.
2. The guarantors' call is made already accepted, while the allies and the overlord are only asked; a
   guarantor whose call did nothing is not asked again as an ally.
3. A call to arms from the screen exists only in a limited war, and costs influence only there.
4. A call with `value` unset is accepted for the player without asking.
5. Declining a call ends the alliance, and only then are the dishonour messages sent.
6. Every casus belli made in play is of the null type and so does nothing; the `casus_belli` condition
   answers by the types' `is_valid`, not by what the `casus_belli` effect gave.
7. A truce counts from the last change of membership, not from a war's end.
8. `DECLWAR_LIMITED` shows only where the declarer is in a real faction; `MESS_BADWORDS4` is never chosen.
9. `AddDefender` has no faction-leader rule, so a faction leader joining the defenders never leads them.
10. A puppet may declare war only on its overlord, and only a country at war with a puppet's overlord on
    the puppet.
11. The declare-war dialog posts the declaration accepted, so no diplomat goes out, and shows none of the
    costs.
12. Peace has no terms, and a province goes to a core holder of the other side who may never have held it.
13. Only the first attacker gives back what it holds of the defenders at the peace; the other attackers'
    holdings go back with the day's return.
14. A province a peace gives back takes the building damage of a capture, the war still standing.
15. A side of two or more facing one country stays in the war, alone, and `PEACEACCEPT` reaches its player
    though he took no part.
16. `PEACESELF` "negotiating for themselves" is shown, and the whole war ends either way.
17. `PEACEDECLINE`'s log line is about debt.
18. The AI's answer to a peace rolls the game's generator once for each of the proposer's units.
19. The surrender progress counts only the cores a country owns; one owning none is never near surrender,
    and one owning land but holding none is always on the brink.
20. Taking a capital loots a quarter of the money and resources, and a change of hands with consequences
    stops a province's production even between friends.
21. Every enemy's goals on a capitulating country are enforced, not the victor's alone, and annexing goals last
    whatever their rank; the made-up `conquer` and the vassals' `puppet` skip the gate and the type's `is_valid`.
22. The exile's draw is taken whether or not there is a host; Belgium and the Netherlands go into exile whatever
    it says; a puppet whose overlord owns land goes into exile rather than being annexed.
23. A `handles_peace` goal still runs every peace option, its threat and `on_completion`, and the country fights on.
24. Every player is told `ANNEX`; any occupier keeps an annexed country's land, an ally of the annexed's included;
    threat falls by a flat 9999; nothing marks the annexed country dead.
25. A puppet joins its faction leader's wars, not its overlord's, and with no faction none; its own puppets go free
    without a word; nobody is told it is one.
26. An exile's units at sea always survive; a factionless exile is never swapped to a factionless holder; the
    puppet swap needs no truce; coming home restores nothing.
27. The government goes back to the history's first capital, not the declared one; a country owning nothing keeps
    its declared capital for good; occupation never moves it.
28. `create_vassal` and `inherit` refuse no country on both sides.

## 15. A war nobody declared

`undeclared_war = { target = ... region = ... }` starts one [exe; `reversing/findings/FINDINGS-scriptgaps.md`, section 4]: the pair's, made with the scope as attacker where they have none, and the region added every time, a region added twice being there twice. With no target it is the controller of the scope's province. It costs nothing, says nothing and has no diplomatic state of its own, and `undeclared_war_with` asks whether the pair has one.

The two are enemies **only in its regions**: fleets and air units there fight, and each other's convoys are raided there. A formal declaration, a call to arms or an annexation ends it; no peace, event or date does. The naval map mode paints the regions orange for the player. Saved as `undeclared_war`, its attackers, defenders and regions.

**Ours**: the original asks this everywhere a province is at hand. Here it is asked where a battle starts, where a convoy is raided and where an air unit or fleet looks for a target. The AI's own war-time gates do not count it, and the land's movement and blockades do not ask it, which matter only to a mod that lists land.

## Ours

- **The war's name is kept as its key, the adjective of its first attacker and its order**, and written
  out when shown; the original writes it out once, when it is made. The order is worked out against the
  names written out, as the original's.
- **A country's adjective** is read from the localisation as the game is made, as its name is.
- **The peace dialog's word for the AI's answer rolls a copy of the game's generator** and puts it back; the
  original's rolls the game's own, so that a dialog looked at changes the rolls that follow.
- **The victory points of a country's cores are counted once a day**; the original counts them also with
  every diplomatic change that rebuilds a country's neighbours.
- **A country's owned provinces are walked in the provinces' order**, at the peace and in the surrender
  sums; the original walks the order the country came to own them in, which was not read.
- **The diplomacy screen's action list is made again each hour** and just after a request is closed; the
  original's is made again by every diplomatic change.
- **A pact is one object for both countries**, so when a country is annexed its NAPs, embargoes, influences and
  alignments go for both sides; the original resets the other country's side alone and leaves the annexed one's
  pointing at them, which a later `release` would find. A guarantee a new puppet had is deleted where the original
  nulls its pointers and keeps the object, which comes back after a save and a load.
- **The surrender's place in a country's day** beside its convoys, the exile's hand-over beside its conversions,
  and the seat's hourly look beside lend-lease are ours: the original's order of those was not compared.
- **The ledger of supplies from exiles is made anew each day before the hand-over**; where the original's daily
  reset of its pools falls was not compared. It is kept and shown nowhere yet.
- **A history goods line is added to the stockpile**, where the original's sets the good: the same where nothing
  was there, different where two countries' histories share a capital.
- **A unit's place a shattered unit goes to** is its country's seat, where the original also takes the depot of
  the unit's province when that depot's holder is no enemy - which comes first was not read.

## Not built

- **The NAP's border balance** (`agreements.md`, section 9): a NAP inside its three windows cannot be
  broken, and the tooltip's balance is nought.
- **Units on ground they may no longer stand on** (`FINDINGS-war.md` A.9): the original marks them and
  works it out again hourly - no attack, no capture, full speed, may pass through. Nothing marks them
  here.
- **Undeclared wars** and the revolt war.
- **The AI declaring war, offering peace or calling its allies** of its own accord - the foreign minister
  is not built. A war starts by the player or by script. For trials: `--war=GER:POL` stays a bare war -
  the two put at war and nobody else, as the savegame tool's trials want them, though they join it as any
  joiner does and so are mobilised (the raid trial sinks 38 transports and 3 escorts since, 37 and none
  before); `--declare=TUR:IRQ` declares as the dialog would with the first goal it offers, whatever the
  neutrality; and `--neutrality=GER:0` lets a country declare on a new game, where **nobody may** - every
  country's effective neutrality is above its ceiling on 1 January 1936.
- **Countries that warned another** (`CW_WARNWARN2`): nothing warns.
- **A peace offer put to the player** (`CAskPeace`, `FINDINGS-war.md` C.7): only another player's offer
  brings it, and there is one player.
- **What else a change of hands does** (`FINDINGS-surrender.md` D.2, D.3): air and naval units based in the
  province sent to the old side's nearest base or deleted, radar,
  trespassers and the province's history. A battle's capture goes through the same setter with consequences
  (`landcombat.md`, section 7). The occupation policy a capture sets and the nationalism a change of owner
  sets are built since 2026-10-14 (`occupation.md`, sections 2 and 5), and what the rebels' factions make of land
  they take and lose since 2026-10-15 (`rebels.md`, section 10) - of the history, the last day the rebels took it -
  and the underground's dissolution since the same day (`undergrounds.md`, section 7).
- **`surrender_inherit`** - annexation's surrender mode, where a neighbouring co-belligerent takes control and a
  unit goes over on an even draw - and `release_vassal`. `release` is built since 2026-10-15 (`rebels.md`,
  section 12).
- **Expeditionary forces**: an annexation sends them home (`expeditions.md`, section 5); a government going into exile does not yet.
- **The cabinet refilled** when a puppet takes its overlord's government; the government's type alone is taken.
- **What else an exile is**: its units drawing on friends' depots for free, its brigades built at its leader's
  technology, liberation, and its partisans turned to militia when it comes home. Organized Revolt, its undergrounds
  raised at once, is built since 2026-10-15 (`undergrounds.md`, section 6).
- **The puppet's tribute** (`FINDINGS-capitalreaders.md` 4), which stops while a puppet's government has fled.
- **A country owning nothing**: the original destroys its units each hour unless it is in exile, and takes it out of
  its wars as a game is loaded.

## Not established

- Who refreshes `action` while a war is fought (`FINDINGS-war.md` A.12).
- The list format of the dialog's COUNTRY lists: the original's helper is unread; names joined by ", "
  here.
