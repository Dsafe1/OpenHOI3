# The foreign minister

Stage 5's second and third pieces: **the foreign minister** of every country nobody plays - its decisions, its calls to
arms, its transit rights, its wars, its pacts, its influence and its alignment. All of it is the install's Lua - Their
Finest Hour's `script/ai_foreign_minister.lua` (`ForeignMinister_Tick`, `ForeignMinister_EvaluateDecision`,
`ForeignMinister_OnWar`), with `ai_diplomacy.lua`'s scores, `ai_support_functions.lua` and the countries' own
`ForeignMinister_*` in `script/country/` - so what this spec sets out is what the engine does around it: when it calls
it, with what, what each method it reaches answers, the proposal it keeps, and the prepared wars it walks and posts.
Read out of the original in `reversing/findings/FINDINGS-foreignminister.md` (the day, prepared wars, readiness, and in
section I everything the tick reaches) and `reversing/findings/FINDINGS-eventfire.md` section 2.4 (the decisions pass);
the minister's object and hour are `reversing/findings/FINDINGS-ai.md`, its first work `FINDINGS-ministers.md` B.1. The
tech and production ministers are `ministers.md`; a decision itself is `events.md`, section 9; what each offer does is
`agreements.md`, `aid.md`, `war.md` and `factions.md`.

**Only for the countries nobody plays**, as the other ministers (`docs/compatibility.md`, entry 6).

**Mod-able or fixed.** What the minister does - which decisions, which pacts, whom it influences, which wars it plans,
every chance and threshold in the scripts - is the Lua's, and so a mod's. When the tick runs, the scope the decisions are
asked in, the proposal kept and its threshold of 50, the walk of prepared wars, the readiness test with its figures, the
pruning and the three special war goals are the engine's, and no define reaches them.

## 1. The minister and its day

As a new game is made every country nobody plays is given a **foreign minister**, first working `draw % 3 + 1` hours
away, a draw on the game's generator - before the production and tech ministers' draws, the original making its five
in the order foreign, production, tech, politics, intelligence (*ours*: after the politics and intelligence ministers'
draws, which were built first). **Its first work does nothing**: the original's constructor sets a byte that the first
work clears and stops on. From then on it works every hour and does anything only in **its hour of the day,
`(number + 13) mod 24`**, the number being the country's place among the countries plus one (*ours*, as the other
ministers'). A country that owns no land and is not in exile is passed over; nothing works while a game is being made.
The hourly step runs after the intelligence minister's and before the production minister's.

In its hour, in order:

1. **`ForeignMinister_Tick(minister)`** - the Lua's day. A Lua error is reported and ends the tick, and the rest of the
   day goes on.
2. **The proposal** (section 3): the best offer the tick proposed, posted where it scored above 50.
3. **The pruning of prepared wars** (section 5), only for a country that is a major or has more than 100 IC of its
   provinces' (`max_ic`, whole IC).
4. **The walk of prepared wars** (section 6).

The original also answers the offers made to the country here; OpenHOI3 answers them every hour, as the original's AI
pass does too (`agreements.md`, section 2).

**Their Finest Hour's tick**: the decisions pass first (section 2); then, each by a draw of `math.random`: allies called
in, one day in six; transit rights asked, one in eight; a war proposed, one in four - the country's own
`ForeignMinister_ProposeWar`, which some countries have; and either peace and pacts, one in ten, or else influence, one
in eight and for a major in a faction; and alignment, one in ten. Each part scores what it might do with the
`DiploScore_*` functions the AI answers offers with (`lua.md`, section 6) and acts on a high score, through
`CAI:PostAction` at once or `CAIForeignMinister:Propose`. Mobilising, laws and spies are the other ministers'.

## 2. The decisions pass

`minister:ExecuteDiploDecisions()`, which every shipped tick calls first:

1. **One scope**: the country, nothing else, its state of chance and its seed **one draw on the game's generator** -
   one draw a pass, not one a decision.
2. The **diplomatic** decisions, in file order (`events.md`, section 9: the political ones are never offered): for each,
   its `potential`, and where that holds its `allow`, both in that one scope - **not** set back to its seed between
   decisions, so a random condition in one moves the state the next is asked in.
3. For each that passes both, **`ForeignMinister_EvaluateDecision(minister, decision, scope)`**. Its answer must be a
   number; it is turned into a whole number **to the nearest, ties to even** (the original's `lua_tointeger`, `lua.md`).
4. **An answer above 0 posts the decision**: `CExecuteDecisionCommand` with its name and a copy of the scope - asked
   again when it runs, its state set back to its seed (`events.md`, section 9). The pass goes on, so one day may post
   several decisions, each judged on the state as it was before any of them ran.

**A Lua error, or an answer that is not a number, ends the pass and the tick that called it**, reported once.

Their Finest Hour's `ForeignMinister_EvaluateDecision`: `math.random(100)`, and where the country has a file with
`ForeignMinister_EvaluateDecision`, that function's answer instead; **below 25 is 0**. With no override that is three
days in four for each decision open to the country. Six countries override it: Germany (dates for its pre-war
decisions, `danzig_or_war` prepared as a war), Italy (Albania and Greece prepared as wars), Japan
(`marco_polo_bridge_incident` prepared as a war on Shanxi and China), the Soviet Union, Finland and Sweden.

## 3. The proposal and the proposed war

**`minister:Propose(action, score)`** keeps the action as the day's proposal where its score is **strictly above** the
one kept and it may be pressed (its kind's own rules, `agreements.md`); otherwise nothing. After the tick, **a kept
proposal scoring above 50 is posted** - one a day at most, the day's best - and the score set to -1, so the next need
only beat that. **One at 50 or under is neither posted nor dropped**: it stays kept until a higher one replaces it. The
first is kept against a score of 0.

**`minister:ProposeWar(tag, score)`** keeps a proposed war where its score is more than 40 above the one kept;
**`ClearWarProposal()`** puts back none and 0; **`GetProposedWarTarget()`** answers the tag, the null tag for none.
Nothing in the engine reads it and no shipped script calls any of the three.

**`minister:PercOccupied(tag)`**: of the provinces a country owns, the share it does not control, in hundredths -
`occupied x 100 / owned` made a single-precision number and rounded to the nearest; 0 where it owns none.

## 4. What the scripts are handed

By the original's names (`lua.md`). New with the foreign minister:

| class | method | answers |
| --- | --- | --- |
| `CAIForeignMinister` | `GetCountryTag`, `GetCountry`, `GetOwnerAI` | the minister's country, its tag, its AI |
| | `ExecuteDiploDecisions`, `Propose`, `ProposeWar`, `ClearWarProposal`, `GetProposedWarTarget`, `PercOccupied` | sections 2 and 3 |
| `CDecision` | `GetKey` | its name, a `CString` |
| | `IsPotential(scope)`, `IsAllowed(scope)` | its `potential`, its `allow`, in the scope handed |
| `CEventScope` | - | the pass's scope, handed on to the two above |
| the actions | `Name(actor, recipient)`, `Name.Create(actor, recipient)` | an offer, proposed, value yes, dated now |
| | `SetValue`, `GetValue`, `GetType`, `IsValid`, `IsSelectable`, `GetAIAcceptance`, `GetActor`, `GetRecipient` | its value; 0, proposed; and as its kind answers (`agreements.md`) |
| `CCallAllyAction` | `(caller, ally, enemy)`, `GetVersus`, `SetVersus` | a call to arms against the enemy |
| `CCountry` | `GetFlags` | the country's flags, a `CFlags` |
| | `GetMaxNeutralityForWarWith(tag)` | the ceiling for war (`diplomacy.md`) |
| | `GetAllies`, `GetVassals`, `GetControllerNeighbours` | iterators: its allies as the `ally` scope has them, the countries it is overlord of, the holders of occupied land beside what it holds |
| | `IsNeighbour(tag)` | owning land beside its own, as `GetNeighbours` |
| | `HasNeighborInFaction(faction)` | a real faction with members, and a neighbour in it |
| | `HasActiveLendLeaseFrom(tag)`, `IsGivingLendLeaseToTarget(tag)` | lend-lease from that country, to that country |
| `CDiplomacyStatus` | `HasEmbargo` | an embargo between the two, either way |
| | `IsBeingInfluenced` | the other country influences the holder |
| `CWar` | `IsLimited` | a limited war |
| `CFlags` | `IsFlagSet(name)` | whether the flag is there and set |
| `CEU3Date` | `GetDayOfMonth` | the day of the month counted from 0, as the month is |
| `CAIStrategy` | the prepared wars | section 5 |
| | `GetProtectionism(tag)` | the AI's strategy's `protect` weight, as friendliness is `befriend`'s (`aistrategy.md`) |

The actions are the original's twelve classes and the lend-lease offer: `CAllianceAction`, `CNapAction`,
`CGuaranteeAction`, `CMilitaryAccessAction` (asked), `COfferMilitaryAccessAction`, `CEmbargoAction`, `CInfluenceNation`,
`CInfluenceAllianceLeader` (align), `CDebtAction`, `CRequestLendLeaseAction`, `COfferLendLeaseAction`, `CFactionAction`
and `CCallAllyAction`. **`IsSelectable` is each kind's own**, as the screen greys its row: the original's Lua reaches
the kind's override through a virtual call, not the base's "actor is not recipient". `CAI:PostAction` posts any of them,
with no test of its own (`agreements.md`, section 1).

## 5. Prepared wars

A country's AI keeps a **list of war targets**, each: the target, the decision tied to it (or none), whether it is to
be limited, the hour it was prepared, and a value. The Lua's methods of `CAIStrategy`:

- **`PrepareWarDecision(tag, value, decision, limited)`** - nothing where the target is the null tag, owns no province,
  is this country's puppet, or is in this country's faction (one it has). Otherwise **a new entry at the end of the list
  where the target has none** - the target, the decision, `limited` and this hour - and then **the entry's value is
  set**: so a second call for a listed target changes only its value, and keeps its first decision.
- **`PrepareWar(tag, value)`**, **`PrepareLimitedWar(tag, value)`** - the same with no decision, `limited` no or yes.
- **`CancelPrepareWar(tag)`** - the target's first entry taken off.
- **`IsPreparingWar()`** - whether any entry's target owns a province or is in exile, is not this country's puppet, and
  is not in its faction.
- **`IsPreparingWarWith(tag)`** - false where the two are at war, or the target owns no province or is in exile; else
  whether it is listed.

The politics minister's script asks the last two (`politics.md`, section 12) to mobilise.

**The pruning** (section 1, step 3). A date's **month** is `year x 12 + month`. The last entry in the list whose target
is a major or has more than 100 IC is **the great target**; with none, nothing. Otherwise every other entry whose target
is neither, where **the great target's month less its own is more than 1, or this month less its own is more than 3**,
is taken off. So while a war on a great power is prepared, the small targets prepared more than a month before it, and
any older than three months, are dropped; with no great target nothing is.

## 6. The walk of prepared wars

**The first walk**, over the list in order:

1. Where the country **holds transit rights into the target** (it asked and was granted them), it gives them up - an
   offer of the asked kind with value no (`agreements.md`, *ask for military access*, *no*) - and **its day ends there**.
2. Where the target is a **neighbour** (owning land beside its own, as `GetNeighbours`): the readiness test (section 7).
   Ready: on to the second walk at once. Not ready: remembered, and the walk goes on.

At the end, with a neighbour not ready, the day ends.

**The second walk**, over the whole list from the start, each target put to the readiness test. For one that is ready:

- **with a decision**: a fresh scope - the country, a draw on the game's generator for its state and its seed - its
  `potential` and its `allow`, and where both hold the decision is posted. **No Lua score is asked.**
- **without one, war is declared**, where its row may be pressed (`war.md`, section 5): **limited** where the target was
  prepared so or the country's faction has the rule `limited_war`, with the war goal **`conquer`** - or, for three pairs
  the engine names by their tags, a goal of their own where that goal's `is_valid` holds (the target's scope with THIS
  the declarer): `vichy_war_goal` for Germany on France, `barbarossa_war_goal` for Germany on the Soviet Union,
  `japan_USA_goal` for Japan on the United States.

Nothing takes a target off once its decision is taken or its war begun; the decision's own `potential` keeps it from
being taken twice, and a war in being makes the declaration's row unpressable.

## 7. Ready for war

The original's test counts **the fronts** of an area (`theatres.md`, section 2) - built since 2026-10-17, so the test
is whole (`CWarReadiness`; `FINDINGS-foreignminister.md` E). For each area the country holds with a neighbouring area
whose first province the target holds, every province of the area's fronts with a passable edge into the target's land:

- the **enemy's** land strength in the target's provinces beside it, each province counted once;
- a **front province**, and an **empty** one where none of the country's own land units stands there fit - out of a
  battle's wait and at 99% or more of their strength and organisation on average - and **ours**, the strength of those
  that are.

Three months after the war was prepared the enemy's strength is taken at 0.66, and **Japan is then ready on fewer than
four front provinces** whatever else. Otherwise **more than 30% of the front empty is not ready**, and then it is ready
where the enemy's strength is no more than ours. **No front at all is ready** - a target across the sea, as Albania is
from Italy. So a war prepared on a neighbour - `danzig_or_war`, `marco_polo_bridge_incident` - waits until the country's
units stand on the front, which the unit agents, the military AI's next piece, are what put there.

## 8. Told of a war

**`ForeignMinister_OnWar(minister, joiner, enemy, war)`**: as a country joins a side of a war, for each country on the
other side, **every foreign minister** is told - the original's broadcast to its AI ministers. A new game's ministers
are told of every war already being fought, each attacker with each defender (*ours*: the order). Empty in every
shipped script; a failure is reported. *Inferred*: the order of the two tags, which the original pushes and nothing
reads.

## 9. In the headless run

`headless <install> 1938`, 2026-10-16, Their Finest Hour, with the decisions alone: **seven decisions in two years,
every one the AI's** - the United States' `new_deal` on 1 January 1936, Italy's `trains_run_on_time` on 1 April,
France's `popular_front` on 3 June, Germany's, the Soviet Union's and Italy's interventions in Spain on 20 June, the day
the war began, and the Soviet `great_officer_purge` on 1 January 1937 - against none before. Japan's minister prepared
its war on Shanxi and China on 1 July 1937, which is not ready (section 7). No Lua error. **No reference save
lies late enough to set the dates beside.**

**The whole minister**, the same day: the same run has no Lua error and the same seven decisions, and its ministers
influence and align - 67 influences begun and 67 ended, five alignments - and propose licences. `OpenHOI3.SaveCheck
foreignai <install> 180` tells every offer: in 180 days nine influences, one cancelled, three alignments and ten licences,
all accepted; no NAP, guarantee, alliance or embargo, the scripts' scores leaning on the AI's strategy, which is not
built. **Set beside the Ireland game's saves of February and March 1936 and the April save, where the original's AI
plays every major** (an `align_towards_*` modifier on a country is an influence on it or its alignment): Schweiz to the
Axis from 6 January (ours 2 January), Sweden from 14 January (ours 17 January), Belgium under the Axis from 28 January
and no longer in March (ours, Germany influencing Belgium from 1 January, again and again), South Africa to the Allies
by 8 March (ours, Britain influencing it from 2 January), Turkey to the Comintern by 25 March (ours 16 January). No NAP
in any of the saves either, and the 22 guarantees are the history's. *Measured*.

**With the AI's strategy built** (`aistrategy.md`, the same day), the same 180 days: fifteen influences, two of them
cancelled, three alignments and eleven licences, no Lua error - and still no NAP, guarantee or alliance offered, as in
the original's saves, which hold none made since the start. Every alignment and influence those saves hold is one ours
makes, Britain's influence on South Africa on the very day (8 March).

**Whether the AI declines every NAP as the original's does** (looked at 2026-11-21): every answer of
`DiploScore_NonAgression` on a new game is below nought here (`OpenHOI3.SaveCheck aiscore`), and the install's script
makes it so by its own terms - the relation above 150, a much larger actor, friendliness and antagonism, halved for a
country that is no neighbour, **less the whole distance between the two acting capitals** in the map's measure - so a
NAP scores above the proposer's 50 only between countries a capital's width apart, or where a country's script sets the
score (`JAP.lua` and `SOV.lua` give each other 100 at war with China or Germany). The original's saves agree: the
Ireland game holds **no NAP its AI made until 29 December 1937**, China and the Soviet Union, and none in 1936 or 1937
before it; its Japan-China pact of August 1938 and every Vichy one are script (event 520, `surrender2.txt`), as is the
Molotov-Ribbentrop pact. So declining every NAP on a new game is the original's too. *Measured* on the saves, the script
read; whether ours makes the China-Soviet pact of December 1937 is not looked at, its China capitulating by then
(`headless.md`, section 4).

## 10. Ours

- **The country's areas** the readiness test walks are taken as the areas of the land it holds; the original's list of
  them was not traced (`FINDINGS-aitheatre.md`).
- **The scores** - a war target's value, the proposal's, the proposed war's - are kept as the script gave them, single-
  precision numbers by their bits, so that nothing in the game state is a float.
- **The byte the original sets** when a neighbour's war target changes, read by its theatre layer (by its look), is not
  kept.
- **The Lua runs side by side on eight states**, a country always on the same one, each state's `math.random` its own
  and a run's draws on a generator of its own; the proposal, the pruning and the walk follow in the countries' order
  once every country of the hour has run (`ministers.md`, *Ours*). The original's threads take countries as they come
  free, each with the thread's `math.random`.

## 11. Kept as the original does them

Do not tidy:

- The decisions pass asks every decision in one scope and never sets it back between them.
- A prepared war's decision is posted without asking the Lua; a decision tied to a target already listed is lost.
- Holding transit rights into a target ends the day before anything else of the walk.
- Nothing takes a target off the list.
- The pruning keeps every small target while no great one is listed.
- A proposal at 50 or under stays kept for ever until beaten; only one proposal a day goes out.
- A new minister's first work does nothing.

## 12. Not established

- ~~Where in the AI's pass a minister's hour falls beside the others'~~ - read 2026-11-21
  (`reversing/findings/FINDINGS-ai.md`, section 1a): each country's pass runs its ministers in the order foreign,
  production, intelligence, technology, politics, then answers its offers, then moves its units; a landless country not
  in exile runs none. **Ours runs one step per minister over every country**: what each posts waits for the hour's end,
  so what differs is the order of the posted commands and of the draws on the AI's one generator - which in the original
  hangs on its worker threads too, the countries' passes running side by side (`tick.md`).
- What one figure of the original's minister, -1 from its constructor, is (`FINDINGS-foreignminister.md`).
