# Politics

What a country's government is and how it moves: its parties, its cabinet, its elections, its national
unity and its dissent; how its laws and ministers are changed; the screen; and the AI's politics. Read
out of the original in one wave (`reversing/findings/FINDINGS-government.md`, sections A to E), with
what was known before it (`reversing/findings/FINDINGS-effectrun.md` sections 1.4 and 1.5,
`reversing/findings/FINDINGS-politics.md`, `reversing/findings/FINDINGS-triggerleaves.md`). Built in
four pieces, by the decision of 2026-10-14: the government's figures and their days; laws and
ministers changed; the screen; the AI's politics.

Figures are thousandths unless said otherwise; a party's figure is thousandths of a percentage point, so
65 in a history file is 65000.

## 1. What a country holds

- **The ideologies**, from `common/ideologies.txt` in its order, numbered from 1 - number 0 is the null
  ideology, which every list below has a place for and nothing names. Each has a colour and its group.
- **Its ruling party**, an ideology; **its government**, one of `common/governments.txt`'s, which says
  which ideologies it accepts, which cabinet posts an election changes (`<post> = yes`), whether it holds
  elections (`election = yes`) and its term (`duration`, in months), and is a modifier like a law.
- **Two figures for every party**: **popularity** and **organisation**, one value per ideology, number 0
  included. With each figure, five **tallies** - this month's record of what set it, by source: 0 the
  election's scratch, 1 the effects, 2 the history, 3 the spies, 4 the monthly drift - and with
  popularity, **the last change** the monthly drift made. Every writer adds the **new figure** to its
  tally, not the change. Only the values are saved.
- **Its cabinet**: whole seats per ideology (section 4).
- **The cabinet posts**, `common/government_positions.txt` in its order, numbered from 1; a post with
  `changeable = no` - the head of state and the head of government - is fixed.
- **Its ministers**: those its own file in `common/countries/` declares, each with an ideology, a
  picture, a start and a death date and a minister type per post he may take; **the pool** of ministers
  it may appoint (section 7); and who holds each post.
- **Its last election** and its own `duration`, which overrides the government's where it is not 0.
- **National unity**, held between 1.0 and 100.0, and **dissent**, at least 0 with no upper bound.
- **Its strategic warfare**: three lists of `(hour, value)` - convoys, allies, bombing - each summed to an
  impact held to -2.0..2.0 (section 6).

## 2. A new game

1. **The history** gives the ruling party (`ideology`), the government (`government`), national unity
   (`national_unity`, held to 1..100, with no `national_unity_effect`), dissent (`dissent`, at least 0)
   and the parties: `popularity = { ... }` and `organization = { ... }` - each figure goes in **raw**,
   x1000, into tally slot 2; a party the history does not name keeps 0; a negative figure is ignored;
   **nothing is normalised**. Germany's organisation history sums to 230. A country whose history gives
   no national unity has 0, which its first day lifts to the floor of 1.0.
2. **The pool** is made (section 7) and the empty posts are filled (section 7) - empty posts only.
3. **The election dates**: `last_election` from the country's own file in `common/countries/`, or a
   `last_election` in its order of battle. A date before today is moved forward by whole terms to the
   last election date on or before today; a country with none gets **today**.
4. The cabinet seats are allotted (section 4).
5. The month's pass a new game runs before its first hour (`tick.md`, section 6) runs **one drift**
   (section 3), which normalises both figures to 100 and allots the seats again.

**Measured, not read:** the start save's organisation is the history normalised **twice** for the
countries that own land (Germany 39.2/13.0/13.0/13.0/17.3/4.3 from 90/30/30/30/40/10, where once gives
39.1) and once for those that own nothing; what normalises it the second time was not found
(`FINDINGS-government.md` B.8). **Built so**: a country that owns a province has both figures normalised to
a hundred as it is set up, before the drift - which makes a new game's organisation the start save's, 1010
of 1010 parties (`OpenHOI3.SaveCheck politics`).

## 3. The monthly drift

Once a month, for every country, existing or not, for every ideology in its order, **number 0
included**:

    drift   = (organisation[i] - popularity[i]) x 1000 / PARTY_POP_CHANGE_RATE              (truncated)
    ruling  = i is the ruling party ? floor((ruling_party_support / 1000 + 0.0005) x 1000) : 0
    random  = r x POPULARITIY_RANDOMNESS / 1000 - POPULARITIY_RANDOMNESS x 1000 / 2000
              r a draw of 0..999 on the game's generator
    penalty = n >= 2 ? (dissent x n) x ELECTION_MINISTER_DISSENT_PENALTY / 1000 : 0
              n = cabinet posts held by ministers of ideology i, a man in two posts counted twice
    last_change[i] = drift + ruling + random + penalty
    new = popularity[i] + last_change[i]
    if new >= 0:  popularity[i] = new, and the tally's slot 4 gets new
                  (a negative result is not applied at all - the party keeps its figure)

Then popularity and then organisation are **normalised to a hundred**, the seats allotted (section 4),
and both figures' tallies cleared. Their Finest Hour: `PARTY_POP_CHANGE_RATE` 10 - a party closes a
tenth of its gap to its organisation a month - `POPULARITIY_RANDOMNESS` 10 (sic), the draw -5.000 to
+4.990 in steps of 0.010, `ELECTION_MINISTER_DISSENT_PENALTY` -0.1. `ruling_party_support` comes from
`repression` 0.1, `totalitarian_system` 0.3, three minister types and event modifiers: 0.3 is +0.300 a
month. Nothing else moves organisation but script and spies; nothing moves either figure daily.

**Normalising to a hundred**, over numbers 1 and up - **number 0 is left out of the sum and of the
scaling**: `v = (v x 1000 / sum) x 100000 / 1000`, two truncations, so every figure is a multiple of 0.1
and the total a little under 100. **To one** (the seats' only): `v = v x 1000 / sum`.

**Measured**: from the autosave of 1 February to that of 1 March 1936, 101 of 101 countries' party 0
moves exactly as `old + trunc(-old/10) + (10r - 5000)`; one sum before normalising fits every other party
but the ruling one in 97 of 97 countries.

## 4. The cabinet's seats

On a copy of popularity: normalise to one; the seats are the posts that are **changeable** (8 in Their
Finest Hour); every party under `PARTY_POPULARITY_CUTOFF` (0.1 against the 1.000 scale - under 10%) is set
to 0; normalise to one again; for each ideology from 1, `q = share x seats x 1000 / 1000` (truncated) and
`n` = q / 1000 rounded to the nearest, ties to even; the parties sorted by `n`, greatest first, **equals
in their order**; handed out in that order, `seats[i] = min(n, seats left)`. **Seats the rounding leaves
over go to nobody.** Reproduces 606 of 606 saved cabinets.

They are allotted after every drift, after the history's popularity, as a game is set up, after a coup
and an election, and **on every daily pass on which the ruling party holds no seat**.

**A fractured government** is one where an ideology of **another group than the ruling party's** holds a
seat. It gets the static modifier `fractured_government` (`national_unity = -0.05` a month), step 18 of
its modifier (`modifiers.md`, section 4).

## 5. Elections

Each day, after the country's other work and before the surrender: where its government holds elections,
the election is due at `last_election` plus the term in months (the country's own `duration` where not
0, else the government's); **one is held when that date is today or past**. Nothing forces one.

**Holding it**: for each ideology, number 0 included, one draw t on the game's generator;
`m = 100000 x ELECTION_RANDOMNESS / 1000 / 1000`; `factor = max(1000 + 1000 x ((t % m) / 100), 0)`;
`score = popularity x factor / 1000`. With the shipped 0.15, m is 15 and `(t % 15) / 100` is always 0:
**the most popular party wins**, the first of equals by number, number 0 skipped. The winner becomes the
ruling party; **the government's elected posts are filled again** (section 7, all of them, elected only);
`last_election` is today; the seats are allotted. A played country is told `ELECTIONRESULTS` (PREVIOUS_PARTY,
WINNING_PARTY, ELECTION_YEAR) - a popup with a pause and no log entry. Popularity is left as it was.

## 6. National unity

**Daily**, for every country, right after its modifier is made again:

    the three strategic-warfare lists lose entries older than 168 hours, and each is summed, held to -2.0..2.0
    fp15(x) = trunc(x x 32768 / 1000)
    month = fp15(convoys + allies + bombing) + max(fp15(national_unity modifier), -1.0)
    day   = trunc(month / 30)
    u = national unity
    if u < 49.0 and day < 0:  day = trunc(day / isqrt(50 - trunc(u)))
    if u > 51.0 and day > 0:  day = trunc(day / isqrt(trunc(u) - 50))
    delta = floor(day x 1000 / 32768)
    change national unity by delta

`isqrt` is the integer square root, floored. **Changing national unity** is
`u += trunc(delta x (1000 + national_unity_effect) / 1000)`, held to 1.0..100.0; the `national_unity`
effect changes it the same way, not divided by 30 nor slowed. So every `national_unity` modifier is **a
figure a month**; a negative one costs at least 0.001 a day, and a positive one under about 0.031 a month
does nothing. The slowing is only **away from 50**, past 49 or 51. Nothing touches national unity on a
declaration of war, a lost province or capital, an occupation or a surrender.

**Strategic warfare's lists.** Each value added is multiplied by the owner's total IC / 100, held to 0.1..10;
a negative bombing entry also by `1 + sw_national_unity_effect`. The writers:

| list | when | value, to whom |
| --- | --- | --- |
| convoys | a raid beaten off (`CONVOY_TOO_STRONG`) | `-(STRAT_CONVOY_DAMAGE / victim's convoys) / 10`, to the victim |
| convoys | a raid that sinks ships | `(transports + escorts lost) x STRAT_CONVOY_DAMAGE / victim's convoys / 1000`, halved with an escort, to the victim |
| convoys | a sea battle's first hour, **a raid's own battle** only, where a defending unit is raiding convoys or patrolling - which is never, the defender of a raid's battle being the convoy's fleet under no order (`navalmissions.md`, 8.3) | `STRAT_RAIDER_FOUGHT_IMPACT`, to the attacker's first country |
| convoys | an air battle's first hour where a defender is raiding convoys from the air | attacker's wings x `STRAT_AIR_RAIDER_FOUGHT_IMPACT` / 5 |
| bombing | an air battle's first hour where a defender is on strategic bombing | attacker's wings x `STRAT_BOMBER_FOUGHT_IMPACT` / 5, times `STRAT_BOMBER_NONCRIT_IMPACT_FRACTION` where the province is land a **defender's** country controls and not its core |
| bombing | a strategic bombing starting | `STRAT_BOMBING_PENALTY`, to the target unit's owner, else the province's controller |
| allies | a land battle, once a day a side | below |

The allies' writer, at the end of each land side's hour at midnight, after its units have fired: C is the battle
province's controller - or, for an attacking side whose first unit's owner is a friend of the province's owner, the
controller of the province that unit stands in, **and that province is then the one the rest asks of**. It acts only
where that province is in the supply area of C's acting capital, C is a friend of its owner (military access
counting), and C has strong allies at war; then, in the side's order, each of the side's units owned by C adds
`brigades x STRAT_NO_ALLIES_HELP_PENALTY` (-0.001) to C's list and each one owned by one of those allies
`brigades x STRAT_ALLIES_HELP` (0.1) - both scaled by C's IC as every entry is. A lent unit counts for its owner,
not its lender. `confirmed` (`reversing/findings/FINDINGS-definesuse.md`, section 9).

**Strong allies at war** are the original's list made of a country's allies (`reversing/findings/FINDINGS-factbase.md`;
`FINDINGS-scopetriggers.md`), in their order: each ally that is not a government in exile, whose acting capital is on
the continent of the country's, whose maximum IC is at least half the country's - halved toward nought, so at half
exactly it counts - and which is at war. The original keeps the list and makes it again with its other lists of
countries; it is worked out here when asked for (`CCountryLists.StrongAlliesAtWar`), as those lists are (`triggers.md`,
section 2).

(measured, 2026-11-18, a look) `OpenHOI3.SaveCheck load` with a later save sets each country's entries made since the
earlier save beside the later save's `alliance` list. `Ireland1939_09_01_00.hoi3` played to `_09_06_18`: Poland alone
has entries in both, small charges at midnight and no credit - nobody fights in Poland beside it that week - 23
summing to -0.020 in the save and 32 summing to -0.016 in ours. Ours has more Polish units in battles at midnight, which
is the AI's moves, and more entries of 0.000 - 14 to the save's 3 - which at Poland's 52 IC is a unit of one brigade;
whether ours puts more such units into battles, or something else, was not looked into.

So a country fighting in its own land with nobody to count on gets no entry at all, good or bad; one with an ally to
count on is charged a little for each of its own brigades and credited a great deal for each of the ally's. Built
2026-11-18 (`CStrategicWarfareImpacts.AlliesFought`). The other six are built; the air raider's since 2026-11-14, with
the raid on convoys from the air (`navalmissions.md`, 8.1 and 8.3).

## 7. Ministers: the pool and the filling of posts

**The pool** (at a new game): **every minister the country's file declares whose start date has come**,
in the file's order. **Ours, following the save**: the original's bytes also ask that his death date is
still to come, which would leave out every minister without one; the start save pools them all
(`FINDINGS-government.md` A.6, open). Afterwards a minister joins on his exact start day. **No minister ever
dies**: nothing takes one out of the pool or his post.

**Filling posts** (`refillAll`, `electedOnly`): for each post from 1, skipping it unless `refillAll` is set
or it is empty, and with `electedOnly` unless the government's election changes it: in the pool's order,
the **first** minister of the ruling party who may take the post; else the **last** of the ruling party's
group who may; else the **first** of anyone who may; where nobody may, the post keeps its holder. "May take
it": the post is among those his block names. A new game fills empty posts only (0, 0); an election its
elected posts (1, 1); a coup, a government changed by script, a release or a liberation every post (1, 0).
A country with no ruling party then takes its head of state's ideology. **Measured**: 220 of 220 posts for
the 22 countries the history names nobody for.

**A minister is the first type his block gives for a post.** `confirmed` (`reversing/findings/FINDINGS-startshares.md`):
the original appends each post-and-type line of a minister's block to his list of postings in the file's order, and
asks his type in a post of the first line that names it, so a second line for the same post is read and never used.
(measured) Honduras's armament minister of 1936 is declared a laissez-faire capitalist and then a resource
industrialist; the start save's energy for Honduras, 17.500, is the first's, where the second's 5% more resources gave
18.000 until 2026-11-14. The base game declares a post twice or more 270 times, 71 of them with different types -
Germany's chief of security 111 a prince of terror and an efficient sociopath among them. *Exe-hardcoded*: the first
wins; *mod-able*: the blocks. Listed in `docs/enhanced.md`: the second line is dropped without a word.

## 8. Script

| | |
| --- | --- |
| `government = <name>` (condition) | the country's government is that one |
| `ideology = <name>` | the ruling party is that ideology |
| `ideology_group = <name>` | the ruling party is of that group (the original faults on no ruling party; here false) |
| `popularity = N`, `organisation = N` (conditions) | the ruling party's figure at least N |
| `<ideology> = { organisation = x popularity = y }` (condition) | that party's organisation at least x **or** its popularity at least y - each a whole number, nought where not written, so a block naming one alone always holds |
| `minister_alive = id` | the minister exists and today is within his start and death dates, inclusive. **Ours, following the save as the pool does**: one with no death date is alive from his start |
| `has_removable_minister = yes` | a minister holds a changeable post and no fixed one |
| a post `= id` (condition) | that minister holds it |
| `organisation = N`, `popularity = N` (effects) | the ruling party's figure + N, held to 0..100, the new figure to tally 1 |
| `<ideology> = { organisation = x popularity = y }` | that party's two figures + each, **a negative result not applied**, no upper limit |
| `government = <name> / this / from / REB` | the government set, then every post filled again (1, 0) - no test that it changed |
| `coup_by = <ideology> / this / from` | a new ruling party; with this or from also the instigator's government and the midpoint of the two alignments; then every post filled again (1, 0), `last_election` today, the seats allotted |
| `do_election = TAG / this / from` | that country's election, now |
| a post `= id` (effect) | the minister into the post, where he may take it and does not already hold it |
| `remove_minister = id / post / yes` | his first changeable post emptied; he leaves the pool. A minister holding no changeable post only leaves the pool |
| `national_unity = N` (effect) | national unity changed by N (section 6) |
| `dissent = N` (effect) | dissent + N, at least 0 |

## 9. A law or a minister changed

Two commands carry every change (`FINDINGS-government.md`, section A); script effects write a post themselves.
**Neither checks anything when it runs** but that the country, and the law and its group or the minister and his
post, are real: not the law's `allow`, nor that it belongs to the group, nor the money, war or peace, the pool, his
postings, the post's being fixed, ideology, dates or a second post. **Whoever posts one asks the rest.**

**A law changed** (`CChangeLawCommand`: the country, the law, its group, *pay the cost*, *tell the player*):

1. the law in its group's place;
2. with *pay the cost*: the cost off the money of the country's stockpile, which may go below nought. **The cost** is
   the law's `cost` times thirty days of money from the country's whole IC - `MaxIC x 30 x IC_TO_MONEY x (1 +
   global_money)`, every division truncating; with the shipped `cost = 0.5` and `IC_TO_MONEY` 0.05, 0.75 money a
   point of IC - and **nothing for a country no person plays**;
3. with *tell the player*, for a country a person plays: `LAW_APPOINTED` - "New law Enacted", "$LAWNAME$ has been
   enacted as our new $POSITION$", "$EFFECT$\n$DESC$" - LAWNAME the law's `<law>_<TAG>` text where there is one, else
   its name; DESC `<law>_desc` with every `§b` made `§W`; EFFECT the law's modifier, a line a value; POSITION the
   group's name. A popup, not logged;
4. always: the country's modifier made again; **every line of its build queue priced again**, the work done left
   alone (`buildqueue.md`, section 2); and **every brigade held to what the new modifier allows** - strength above
   its most cut to it and the men of what was cut given back to manpower, `excess x 1000 / max strength x the men a
   brigade costs / 1000`, and organisation above its ceiling cut to it.

No history, no change to dissent, national unity, the parties or neutrality, no delay and no cooldown. The original
keeps the two flags only while a game is under way: a change made as a game is set up pays nothing and is silent.

**A minister appointed** (`CChangeMinisterCommand`: the country, the minister, the post): he holds the post, and a
country a person plays is told `MINISTER_APPOINTED` - "A new minister", "$MINISTERNAME$ has been appointed to the
position of $POSITION$", "$PERSONALITY_DESC$". Nothing else: he and the man he replaced stay in the pool, he may keep
another post, and his modifier arrives with the next day's. *Ours*: PERSONALITY_DESC is his minister type's name -
how the original describes it was not read.

**The peace-time revert.** After any change of diplomacy - here, a country leaving a war (it and each of its enemies
there), a country joining or leaving a faction, and a puppet made - a country **not at war** has each group's law
whose `allow` no longer holds replaced by **the last other law of the group, in the file's order, whose `allow`
does**, posted free and with its message, the player's country included; where none holds the law stays. `allow` is
asked with the country as the scope, no FROM, no province and no chance. *Corrected 2026-11-15*: the walk goes down the
group's own list, which each law read is linked in front of, so it runs from the file's last law back - the list the new
game's walk of section 13 goes down too (`reversing/findings/FINDINGS-starttech.md`, section 3); this said "the first"
until then. (measured) China, made Japan's puppet on the 1939 bookmark's first day, gives up `repression`,
`three_year_draft` and `war_economy` for `legalistic_restrictions`, `volunteer_army` and `basic_mobilisation`, as the
start save has them, where the file's first allowed would be `limited_restrictions` and `full_civilian_economy`. Made
while a new game is being made the command keeps neither flag, as every command the original makes before its game is
under way: free and silent. **Not every leaving of a war is a change of diplomacy**: the original's removal from a side
leaves that to its caller, and two callers make none - the end of a new game's entry taking a landless country off its
wars, and a puppet made, which changes the overlord's diplomacy and then the vassal's once it is all done (`war.md`,
sections 4 and 13). Annexation and the `leave_faction` effect also leave wars by that removal, and the record has no
change of diplomacy in annexation and one, the country's, in `leave_faction`; ours still tells both sides there, not
set against anything.

## 10. Against the start save

`OpenHOI3.SaveCheck politics <install> <start-date savegame> [examples] [laws=save]` makes a new game with the
install's Lua and nobody playing - the original's look at the laws ran for every country - runs its two days, and sets
each country's government and laws beside the save. On 2026-10-14: **the ruling party, the government and the last
election 101 of 101; organisation 1010 of 1010 parties; who holds each post 1009 of 1010; the laws 703 of 707 groups;
national unity 98 of 101**. Before the AI's look at the laws (section 12) national unity was 64 of 101: the press
laws, which `national_unity_effect` comes from, are the AI's. Three of the laws that differ are the rebels', whose
conscription, economy and press no history gives, and the fourth is Israel's press law: free, as a democracy at peace
is given, where the save keeps the history's state press. `laws=save` gives every country the save's laws instead,
before the two days: 707 of 707 and national unity 99 of 101. **Since 2026-11-02, with the engine's own pass before the
Lua's (section 13), the new game gives the same: the laws 707 of 707 and national unity 99 of 101** with nothing from
the save - the rebels' three and Israel's press were what the engine's pass moves. The rest, run again that day, as
before: the ruling party, the government and the last election 101 of 101, organisation 1010 of 1010, the posts 1009 of
1010, and national unity's two misses Communist China's and the Dominican Republic's, each 0.002 short. Popularity and the cabinet carry the drift's draw on a
generator whose seeding is ours, so popularity is set against the save to within five points (928 of 1010 on 2026-10-14 with espionage's draws before it, 932 on 2026-11-02; the figure moves with every draw added ahead of the drift) and the
cabinet differs where a party drew across the cut (Germany 7 seats of 8 here; 877 of 1010 parties' seats on 2026-11-02, the rebels' among them). The post that differs is New Zealand's
foreign minister, whom its AI changed before the save was written - and whom the minister here changes on its first
day. Dissent differs by 0.018 where the AI moved its consumer goods - **and since the standing army came into the
demand (`production.md`, section 3), by more in four countries: 85 of 101 agree on 2026-11-02**, the Soviet Union at
0.278, Japan 0.244, Finland 0.846 and Afghanistan 0.950, where the save has 0.018 or nothing. A new game's consumer
goods share is set before any unit exists, so it is the demand without its army, and the two days run short of it by
the term; with the term taken out, 94 of 101 agree and every difference left is the 0.018. What the original's starting
share is made of is not established (`production.md`, section 1). The start-date check (`OpenHOI3.SaveCheck
<install> <save>`) gives the posts of the countries that own nothing 230 of 230, where the history alone gave 10; the
diplomacy check's alignment rose from 50 to 60 of 101 with section 2, to 79 with section 12 and to 80 with section 13.

## 11. The screen

`interface/country_politics.gui` and `.gfx`, read whole (`FINDINGS-government.md`, section D). The top bar's
`topbarbutton_politics` opens it and closes it again, as the other three screens do; its `shortcut = "F4"` is the
file's. `CPoliticsView` in `src/Screens`, with `CPoliticsScreenText` and `CPoliticsTooltips` for what it says.

**When things are set.** The legend is made once, with the screen. The pie, the organisation bars and the government's
three lines are set **when the screen goes up and not while it is up**. Every frame while it is up: the cabinet rows,
while the cabinet is showing; the law list, made again where a row's law is no longer the country's; the three buttons
on one spot; and the strategic-warfare figures. Going up also hides the choice of a minister, of a law and of a policy
and the puppet and liberation windows, and sets the faction symbol - the faction's frame, or hidden.

**The government.** `government_type` is the government's name, or its `<key>_vassal` text for a subject where there
is one. `government_desc` is the elections line: `TERM_FOR_LIFE` and a line break for a government with no term;
otherwise `TERM_DURATION` with the term in months - the country's own `duration`, else the government's - and a line
break, and where it holds elections `NEXT_ELECTION`, ": §Y" and the date of the last election that many months on,
"November 14, 1939" - **with no closing colour and no line break**, so what follows runs on in yellow; then the
government's modifier, a line a value. `ruling_ideology` is the ruling party's `<ideology>_<TAG>` text where there is
one, else its name, and its icon is tinted the party's colour.

**The parties.** The pie has a slice for every ideology from 1, in the database's order and its colour, each its
**whole percent rounded half to even** of the popularity summed from 1 - the null party is never in it - and none at
all where that sum is 1.000 or less. A party at nought has a slice of nought. How the original draws a pie was not
read: **its slices run from three o'clock against the clock**, from a screenshot of the original on
2026-10-14, and it is **twice its `size` across with its `scale` not applied and the `.gui` position the top of its
middle** - measured on the shadow the screen's art has under it, which is 100 across with its middle 50 below that
position, and on which the screenshot's pie sits. The organisation bars are the icons `<ideology>_org`, filled from the foot to
organisation / 1000 cut to a whole percent, in the party's colour; their sprite has no picture, and a bar with none
is drawn here as its colour alone. *Ours*: an ideology with no bar in the file is passed over, where the original would
use it untested. The legend has a row for every ideology from 1, its name for the country and its colour, under the
names of the country played when the screen was made.

**The cabinet.** The posts in their file's order, the first half - `count / 2` of them - in `minister_list1` and the
rest in `minister_list2`. A row shows the post's name, the holder's name, his personality in that post and what it
does, his party's colour and his portrait, `gfx/pictures/portraits/<picture>.tga` (`empty_position` where he has
none); with no holder, "???" twice, nothing for what it does, the colour hidden and **the last portrait left**. **A
row is drawn again only when the post's holder changes**, and its Replace button is worked out then: pressable where
the post is changeable and some minister of the pool other than the holder is of an ideology the government accepts
and has a posting for it. Replace hides the cabinet and lists **every minister of the pool the government accepts
who has a posting for the post, in the pool's order, the holder among them**; a choice's SELECT is greyed where the
post is not changeable, and posts `CChangeMinisterCommand` (section 9) with nothing asked. `cancel` brings the cabinet
back.

**The laws.** A row for every law group, in the file's order: the group's name, the country's law's
`<law>_<TAG>` text where there is one, else its name, and a button that opens the choice. **The choice lists every law
of the group**, each with its name and its price in brackets - `§G` where the money is there and `§R` where not, one
place - what it does, and the current law's indicator or the other one. **Its button is pressable only where the
law's `allow` holds, the money is there and it is not the law in force.** It opens `DefaultDialog`, both shields the
country's, 150 below and right of where the file puts it: `CONFIRM_CHANGE_LAW_TITLE` and `CONFIRM_CHANGE_LAW_DESC`
with the law's name. Ok posts `CChangeLawCommand` with the cost paid and the player told, **asking nothing again**,
and closes the choice; the dialog goes by itself when the law's `allow` stops holding, whatever the money. The money is
the stockpile's (section 9 for the price).

**Mobilisation.** `mobilize`, `demobilize` and `revolt` stand on one spot. A government in exile shows `revolt`
alone; otherwise `demobilize` shows where the country is mobilised and `mobilize` where not, **both greyed at war,
not hidden**. They post `CToggleMobilisationCommand`: mobilising gives the country `initial_mobilization` for
`MOBILIZATION_BONUS_DAYS`, standing down takes it away - each only where it changes something - and then, whatever
happened, its threat is spread over the map from its acting capital (`diplomacy.md`, section 5.2), `MOBILIZE_THREAT_IMPACT` on
mobilising and taken off again on standing down. **Being mobilised is what calls the reserves up**: a reserve
brigade is then held neither to the share of its strength nor of its organisation `RESERVES_PENALTY_SIZE` keeps back,
and the day's reinforcement fills it, doubled by `initial_mobilization`'s `reinforcement_bonus` while that lasts. It
is one flag, the original's one byte, which a war sets too; until 2026-10-14 the reserves read a second flag of ours
set only at the start, so the button changed nothing a unit felt, which testing found.

**Standing down gives the men back**, after the threat - on every order to stand down, mobilised or not: `confirmed`
(`reversing/findings/FINDINGS-definesuse.md`, section 7). For every unit of the country, every level, and each of its
brigades, ships and wings in order, with the flag already cleared:

    most   = the brigade's most strength - for a reserve, less RESERVES_PENALTY_SIZE where its owner is neither
             mobilised nor at war (section above; the owner the lender's for a lent unit)
    excess = strength - most;  nothing more where it is nought or less
    men    = excess / max_strength x build_cost_manpower x DEMOBILIZATION_FACTOR     each step truncated
    strength and highest strength = most                                             the highest lowered too
    manpower += men, never below nought

so a reserve filled up while mobilised is cut back to its peacetime share, and half the men of the cut (0.5 in Their
Finest Hour) come home: a full reserve brigade of the base game's infantry under a -0.5 penalty goes from 30 to 15 and
gives back 0.5 x 3.33 x 0.5 = 0.832 manpower. The figures are the brigade's own, with its technologies in. Built 2026-11-18 (`CDemobilisation`). **`revolt`**, "Organized
Revolt!", raises the exile's ready undergrounds in its own land at once, may be pressed where one is ready, and says how
many are - built since 2026-10-15 (`undergrounds.md`, section 6). *Not built*: making a puppet and liberating a
country - both buttons are greyed, `create_puppet` saying
`POL_PUPPET_NO` and `liberate_country` `POL_LIB_C_NO_FACTION`, `POL_LIB_C_WE_EXILE` or `POL_LIB_C_NO_EXILES`. The list
of occupied countries and the choice of an occupation policy are built since 2026-10-14: `occupation.md`, section 4.

**Strategic warfare.** `convoys_val`, `allies_val` and `bombs_val` are the three impacts of section 6, two places:
`§R` below nought, `§G+` above, `§Y` at nought.

**Tooltips**, by the element:

| element | says | delayed |
| --- | --- | --- |
| the pie | the slice's party, its name for the country with " (its own name)" where those differ, " - §Y", the percent, "%§W" | its popularity's change factors; `"\n--------------\n"`; the terms of its drift without the draw - the ruling party's support with where it comes from, the dissent penalty where its ministers hold two posts or more, its distance from its organisation; a line break and `QUALIFIES_FOR_CABINET` with its seats |
| an organisation bar | the party's name, ": " and its organisation, two places | its organisation's change factors |
| `ruling_ideology_icon` | the ruling party's name for the country | |
| `faction_symbol_icon` | the faction's name | its rules and modifier, as on the diplomacy screen |
| `mobilize` | `POL_MOBILIZE_IRO`, or at war `POL_MOBILIZE_WAR_IRO` | `POL_MOBILIZE_DRO_V2` with the days, then `POL_MOBILIZE_DRO_V3` with the men full reserves need - for each reserve brigade, its missing strength over its most times the men a brigade costs, one place |
| `demobilize` | `POL_DEMOBILIZE_IRO`, or at war `POL_DEMOBILIZE_WAR_IRO` | `POL_DEMOBILIZE_DRO` |
| a strategic-warfare icon | `POL_SW_CON_IRO`, `POL_SW_ALL_IRO`, `POL_SW_BOM_IRO` | its `_DRO` |
| a strategic-warfare figure | `POL_SW_CON`, `POL_SW_ALL` or `POL_SW_BOM` | |
| a cabinet row's colour | the minister's party's name | |
| a cabinet row's Replace | `NO_REPLACE_MINISTER`, **only while it cannot be pressed** | |
| a law row's law or button | what the law does | |
| a choice of law's button | `POL_CURRENT_LAW` for the law in force; else where `allow` fails, its account of itself; else where the money is short `CANNOT_AFFORD`; else the account again | |

**A change factor** is a line each, only where not nought: `POL_PARTY_POP_FACTORS`, `§R` at or below nought and else
`§G+`, the last month's change trimmed, `§W` and a line break; then `POL_EVENTS` and `POL_SPIES`, `§G` above nought
and else `§R`, the effects' and the spies' tallies trimmed - **the new figures they wrote, not the change** - and
**"+" and a line break, with no closing colour**. With none, `PO_NO_CHANGE`. *Ours*: what fills a strategic-warfare
figure's four places was not read; they are the entries of its list against the country and their sum, and those for
it and theirs.

Nothing else on the screen has a tooltip: not the government's lines, the legend, a row's name, personality, ability
or portrait, nor the choice of a minister.

**Kept as the original does them** - do not tidy: the pie, the bars, the legend and the government's lines do not
change while the screen is up; a cabinet row's Replace is worked out only when its holder changes; the choice of a
minister lists the holder and greys SELECT rather than leaving out a fixed post; the choice of a law lists every law,
and its Ok asks neither `allow` nor the money; the Events and Spies lines end in "+"; the elections line runs on in
yellow, and its variable is `YEARS` though it counts months; a law costs a country nobody plays nothing; a missing
minister keeps the last portrait; mobilising is greyed at war, not hidden.

Built 2026-10-14, read out of the original, tried with `--click`: the pie's and a bar's tooltips, a minister replaced
by Replace and SELECT with the message after it, a law chosen through its confirmation with the money taken and the
message after it, and mobilising. `--screen=politics` opens it, and `--money=GER:500` gives a country money so that a
law may be paid for on a new game, where none can be. **It has not been reviewed in play.**

### 11.1 Making a puppet and liberating a country

Read 2026-11-09, `reversing/findings/FINDINGS-puppetrows.md`; hardcoded but the windows and words. `create_puppet` and `liberate_country` are greyed where their list is empty, and each opens its own window, `puppet_select` or `liberate_select`, its rows filled once as it opens and none greyed; `back_button` shuts it. **Who may be made a puppet** (`CCountryLists.PossibleVassals`): walking the player's land that it holds, its capital left out, and each province's cores, a country with no land of its own, whose capital the player owns, in the order first come on. So occupied land gives nothing. **Who may be liberated**: a government in exile with land, whose declared capital the player holds, that is not its enemy - of any faction, whatever the tooltip's "in our faction" says.

A row's flag opens a `DefaultDialog` at its own place and (150, 150), the target's name and flag twice and `VASSALPROVNUM`: its cores the player owns and holds for a puppet, its land the player's faction holds for a liberation. A row has no tooltip. A row's line names up to three provinces and otherwise counts them; it can differ from what the command gives, which leaves out the player's capital and takes land the rebels hold.

`CCreateVassalCommand` (a country with no land): its cores given by `CRelease`, as `release` does - armies, technology and the news - then the bond, the overlord's place on the triangle, the relation raised 500 both ways and held to 200, the overlord's government where a new country may have it, every post filled, and its faction with its wars where it has a real one. `CLiberateCountryCommand`: every province the exile owns whose holder is in the liberator's faction goes back to it, no owner changing; across factions the same bond, relation, government and faction; `WE_RETURN_EXILE` or `RETURN_EXILE`; and it is no longer in exile. Two countries with no faction count as one.

**Ours**: a list's order follows the provinces' numbers, where the original's follows the owner's list of provinces. **Not built**: the original gives a further alliance where one already stands, not traced as reachable; ~~and the AI's own liberating and puppets, which stay reported as unbuilt (`CLiberateCountryCommand` from Lua, `MayLiberateCountries`, `CanCreatePuppet`)~~ - built since: Lua's `MayLiberateCountries` and `GetPossibleLiberations`, `CanCreatePuppet` and `GetPossiblePuppets` (the original's name, `lua.md`, section 4) answer the two lists, and `CLiberateCountryCommand` and `CCreateVassalCommand` post these commands. `ITA.lua`'s `HandlePuppets` releases Ethiopia so, which a later start reaches in its first hour, Italy owning Ethiopia's capital (`headless.md`, section 5).

## 12. The AI's politics

`FINDINGS-government.md`, section E. **The minister is only for the countries nobody plays**; **a new game's look at
the laws is for every country, the player's too**, as the original's is - ~~the player's laws and ministers are never
the AI's (`docs/compatibility.md`, entry 6)~~, reversed on 2026-11-02, when it was found that part of
what moves the player's laws in the original is the engine's own (section 13). `CPoliticsAi` in `Sim/Politics`; the
Lua's classes in `lib/OpenHOI3.Lua/Api`.

**A new game.** Once the parties are set up and the new game's diplomacy is done - its effective neutrality is what
the laws' `allow` blocks read - the install's Lua `Laws(country)` is called for every country, in the countries' order,
handed the country itself. It is done with the game being made, so a law command the script makes **pays nothing and
posts no message** until the script's `SetEnablePostMessage(true)` - which Their Finest Hour's does, and nobody is told
anyway: the commands were made free and silent. **What it posts waits on the queue until the new game's two days are
run** (`tick.md`, section 6), as the original's commands wait on its session's queue until the entry that posted them is
over - `likely` from the reading, which has the posts go to that queue and the queue drained outside the entry
(`reversing/findings/FINDINGS-startshares.md`), and (measured) on the start save: 16 untouched countries made their
supplies under the industrial policy before the change, and 74 countries' brigades are held to the running game's
ceilings, which only a change of law after the days does. So the days are run under the laws the engine brought forward
(section 13), and then the changes are made, each holding its country's brigades to the running game's ceilings and
pricing its build queue again (section 9). **Behind them on the queue is everything else the entry posted**, the two days'
among it, run in the order it was posted, each asked first whether it may still run - the drain asks every command so
(`CPoliticsAi.RunNewGameLaws`). It was dropped until 2026-11-15. So a country leaving a war in the days gives up the laws
only the war allowed after the look's changes (China at 1939.9.1: the laws 707 of 707, from 704), and the AI's sliders
set in the days stand on the first screen (the start save's shares, a look: 34 of 101 countries to five places, from 24 -
the rest set at the same point from figures that differ). **The look is made once** - the original asks the script once in each
country's set-up, and each law the script moves goes one step: the 1939.9.1 start save's Soviet Union is one step from
`volunteer_army`, at `one_year_draft`, where a second look would take it on (*measured*, 2026-11-15). Until then the look
was repeated until it posted nothing, at most ten times, which gives the 1936 start save the same laws. Each country is
asked **in its turn of the set-up** (section 13): a country whose turn is still to come has no IC worked out, so the
script's `ValidFor` of a law with `enemy_ic_ratio` is false while any enemy comes later. **And the script is asked
before the country's manpower is started at its most** - the call stands before `UpdateMaxManpower` and `manpower =
max_manpower` in `SetUpCountryForNewGame` (`reversing/findings/FINDINGS-startshares.md`, section 2) - so the country
asked and every one after it has none. Their Finest Hour's look reads no manpower; BlackICE's `ConscriptionLaws2` takes
`conscription_law_two` by manpower over eight per division, which with none is nought - or not a number, for a country
with no division - and moves nothing: (measured, 2026-11-19, on a BlackICE start save) the 17 countries the engine's
walk gave a later law keep it, Germany its history's `standard_conscription`, where with their manpower counted all 17
went to `volunteer_recruitment` and Germany's reserves started at 70% to the save's 80%. Each such country is given a
politics minister as the look is made, which first works after `draw mod 3 + 1` hours, a draw on the game's generator
each.

Until 2026-11-14 the changes were made at once, before the days. Made after them - with a new game's shares
(`production.md`, section 1), its manpower (`manpower.md`, section 4) and a minister's first type for a post (section 7)
read the same day - a new game is the start save's in every brigade's organisation (3,792 of 3,792, from 962), every
country's dissent (101 of 101, from 85), national unity (101 of 101, from 99) and the alignment of 100 of 101 countries
(from 80; 93 with the laws and the shares alone) - `OpenHOI3.SaveCheck firstdays`, `politics` and `diplomacy`.

**Its hour.** Each hour, after the trading, every politics minister in the countries' order: one whose country owns
land or is in exile counts down to its first work, and from then on is asked every hour, but does something only in
its country's hour of the day, `(number + 17) mod 24` - *ours*: the number is its place among the countries plus one,
the original's numbering not read; borne out by New Zealand, 79 so, whose minister alone works at hour 0 of both 1941
bookmarks' start saves (below).

**The hour before the first (not built).** The original's first AI pass runs at the new game's hour 0, the frame after
its entry, before the first hour is played (`reversing/findings/FINDINGS-government.md`, E.1; `FINDINGS-aisched.md`), so
a minister whose first countdown is one and whose hour is 0 - Italy's, Portugal's, Guangxi's and New Zealand's by the
numbering - works then. Both 1941 start saves hold New Zealand's foreign minister, minister of security, chiefs of staff,
army and navy other than its history's - 55004, 55062, 55007, 55082, 55085, where the history names 55040, 55061, 55059,
55007 and 55009 - and each is exactly the pick of the script's `OfficeManagement` at war (*measured*: all eight posts it
manages, scored on the ministers' personalities): the script's one-in-eleven branch, drawn on the AI's own generator.
Here the ministers first work in the first hour, so the six posts are the history's, 1004 of 1010; building the pass would
run every minister at hour 0 - next to the units' entry pass of `agents.md`, section 2.1 - and what it posts would hang on
draws whose seeding is ours. In that hour, on the 1st, 8th, 15th, 22nd and 29th of the month, it first fills the
empty posts; and then it runs the Lua's `PoliticsMinister_Tick(minister)`. Nothing while a game is being made. A failure
of the Lua is reported, and ends that day's work.

**The empty posts** are looked at in their file's order and each is given the minister the filling of posts would
choose (section 7) - the first of the ruling party who may take it, else the last of its group who may, else the first
of anyone who may - **save that the head of state is not asked whether his block names the post**. Each goes as a
`CChangeMinisterCommand` (section 9), nothing asked of a second post, his dates or whether the post may be changed.

**The script.** Their Finest Hour's `ai_politics_minister.lua` draws `GetAIRand()` - the game's generator here, as
trade's draws are (*ours*: the original's has a generator of its own) - and in one case in seven mobilises, else one in
ten looks at the laws, else one in eleven at the cabinet, else one in twelve at puppets and liberation, each test a
draw of its own. **Laws** walks the groups, the null group first: education to the biggest investment, industry to
consumer goods at peace and heavy industry or else mixed at war, training to basic, a democracy at peace to an open
society and a free press, and every other group the next law up - each where its `allow` holds - with a country's own
`CallLaw_<group>` before any of it (Germany's training is specialist). **The cabinet** scores each candidate of the
ruling party's group by his personality in the post and posts the first best where he is not already there - **one man
may take two posts**. **Mobilising** asks a faction leader at war, then the AI's war plans, then a country's own
`HandleMobilization`, then each neighbour's threat.

What the script is handed is the original's: a law database numbered from the null law, 0, with the null group
`noLawgroup` first; a country's law of a group, its minister of a post - **the null minister where the post is empty,
never nil** - its pool, its ruling party - **the null ideology `noIdeology`, in a group of no name, for a country with
none**, which is how the rebels are given their laws - whether it is mobilised and its manpower; a minister equal to
another by his number, his ideology, his posts and his personality in each - **the null type `noMinisterType` in a post
he has no posting for** (its key was empty here until 2026-11-16; `reversing/findings/FINDINGS-countrymodifier.md`) - the
minister type database and the cabinet as a list (`lua.md`, section 4); the posts by number; a law's `ValidFor`,
its `allow`; `CAI:Post` and `CCurrentGameState.Post`; the three commands; and the AI's war plans (`IsPreparingWar`,
`IsPreparingWarWith`), the wars its foreign minister has prepared since 2026-10-16 (`foreignminister.md`, section 4).
~~**Not built, and answering false, each reported once**: its own declarations of war
(`CanDeclareWar`), its personality (`IsMilitarist`), liberating a country (`MayLiberateCountries`,
`IsCapitalSafeToLiberate`, and `CLiberateCountryCommand` posts nothing) and releasing a puppet (`CanCreatePuppet`).~~
All built since (section 11.1; `lua.md`, section 4).

**Kept as the original does them** - do not tidy: each test of the tick draws afresh, and a mobilised country still
spends its one in seven; at peace every country without its own rule heads for consumer goods; one man may hold two
posts; the head of state is never asked his block; a group of the same ideology keeps the last candidate where the
ruling party's keeps the first; the default mobilising posts once for each neighbour that qualifies, and each spreads
the threat again.

**Against the saves.** The new game's laws: section 10, 707 of 707 groups since 2026-11-02 (703 before section 13's
pass). The cabinet:
`OpenHOI3.SaveCheck politicsai <install> [days] [tag]` plays a new game with that country played - Germany for sixty
days by default - and tells every law, minister and mobilisation the ministers post and what of the Lua failed. On
2026-10-14 a hundred and ten days changed **198 posts in 64 countries**, where between the start save and one of
20 April 1936 the original's changed 194 in 63; no law moved after the new game's look and nobody mobilised, as in the
saves; and the Lua failed nowhere. Which posts and when hang on draws whose seeding is ours, so the count is the check.
The new game's look also set the AI countries' brigades beside the start save's (`OpenHOI3.SaveCheck firstdays`):
their strength from 3041 to 3435 of 3534 - and to 3620 of 3620, the player's 172 of 172, once the engine's own pass of section 13 ran first (2026-11-02) - their diplomatic influence and officers from 4 to 72 of 77, their money
from 5 to 76.

## 13. A new game's laws brought forward

Read 2026-11-02 (`reversing/findings/FINDINGS-starttech.md`, section 3), the original's
`SetUpCountryLawsAndTechnologyForNewGame`, its first half; the second is the technology's (`research.md`, section 9).
`confirmed` off the listing. As a new game is entered - **every country, the player's too**, once, before its order of
battle is read and before the Lua's look of section 12, **never when a save is loaded** - each law group's laws are
walked **from the last in the file's order** towards the country's own:

- a law with any `unit_start_experience` ends the walk, the group keeping its law;
- the country's own law ends it too, nothing being moved back;
- the first law whose `allow` holds - asked of the country, with no FROM - becomes its law, put in place with no cost,
  no command and no message.

So **a law moves to the latest law of its group whose `allow` holds**, where that is later than its own, and
`training_laws`, whose last law gives starting experience, is never touched. The country's modifier is made again after.
A country with no law in a group takes the latest it is allowed. *Exe-hardcoded*: the walk and the test of
`unit_start_experience`; *mod-able*: the laws' order in `common/laws.txt`, their `allow` blocks and which carry
`unit_start_experience`.

**Not set against a save**: the Lua's look runs after it, and the start save shows the two together. With both, the
laws are section 10's.

**Each country in its turn** (`reversing/findings/FINDINGS-laterstart.md`, section 2; *measured* on the bookmarks' start
saves, built 2026-11-15). The original's entry sets the countries up one at a time, in the countries' order, and a country's
turn begins with its IC, its war and its effective neutrality worked out and then moves its laws. So what a law's `allow`
reads of the world is what the turns before it have made:

- **`enemy_ic_ratio` is false while any enemy's turn is still to come**: until its turn a country's IC is nought, never
  worked out since the constructor (`UpdateIC`'s three callers are the day, the new-game set-up and the entry, `confirmed`),
  and one enemy with none makes the condition false. So at 1939.9.1 Germany, Britain, France and Poland take
  `three_year_draft` and `war_economy` where every enemy's IC would allow `service_by_requirement` and
  `total_economic_mobilisation` - each has an enemy later in the order. The Lua's look (section 12) is made in the same turn
  and sees the same.
- **Effective neutrality** is written for the country in its turn as its neutrality less the threat it feels from its most
  threatening country - **the lobby's, elected with nobody threatened, so the first country it may fear in the countries'
  order**, mostly Germany - and the history's threat of every country whose turn came before has been spread by then
  (`diplomacy.md`, section 5.3). So at 1941.6.22 Sweden, with 90 neutrality and Germany's 41 threat, takes
  `three_year_draft` and `war_economy` in the walk.
- **A party's popularity is the history's**, which the lobby's replay left; a democracy at war takes the civil law its ruling
  party's popularity allows (New Zealand's `legalistic_restrictions` at 1939.9.1).

With these and the single look, the laws are 707 of 707 at 1939.9.1 and at both 1941 dates (674, 635 and 617
before), and the 1936 start save's 707 of 707 as before (`OpenHOI3.SaveCheck politics`). China's three at 1939.9.1 come of
its surrender on the first day - made Japan's puppet by the war's `puppet` goal - and the laws it gives up with that,
run behind the look's changes (section 9, the peace-time revert; section 12); 704 of 707 until 2026-11-15.

## Not established

- Whether anything repeats a new game's look at the laws (section 12): one look since 2026-11-15, which the 1939 start
  save's Soviet Union asks for.
- The changes of law the two days post - China's after its surrender on the 1939 bookmark's days - and whether the
  original's queue runs them after the entry: ours drops them with the queue's other posts of the days.
- What laws a country starts with that its history gives none - the rebels' conscription, economy and press (section
  10). Section 13's pass gives such a group the latest law it is allowed, where the original's country holds no law of
  it; whether it holds a null law there that the walk meets first was not read.
- The original's numbering of a country, which its minister's hour hangs on (section 12).
- What normalises an existing country's organisation a second time before the first screen (section 2) -
  built as measured.
- The minister pool at a new game (section 7): the bytes and the start save disagree; built as the save.
- What "strong allies" is, for the allies' strategic-warfare list (section 6).
