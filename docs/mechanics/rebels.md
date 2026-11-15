# Rebels

What revolt risk comes to: a **rising** - a rebel army put down in a province - and the **rebel faction** it belongs
to, which marches, takes land, hands land back, and may win a country its independence. Read out of the original on
2026-09-30, 2026-10-14 and 2026-10-15 (`reversing/findings/FINDINGS-revolt.md`, sections 1, 2 and 5;
`reversing/findings/FINDINGS-occupation.md`, sections C, D and D.9). `confirmed` unless marked.

This is the third of the four pieces occupation and revolt were cut into (`occupation.md`); the undergrounds, which
raise partisans of their own, are the fourth.

## 1. In one line

    each country's day, each province it holds:  a draw under round(risk / 50) in 10000 (20000 for the AI),
                                                 and then one in 365 - three brigades rise
    a rising:     the type scoring highest, the faction it joins or a new one, one army of 1 to 4 brigades
    a faction's day:    hand land back, march at what its type scores, go home or disband
    a faction's month:  an independence 15% of the time, once its land has been held long enough

Over a year the two draws come to **the revolt risk in points / 500** a province for a player's country, and half that
for a country nobody plays.

## 2. The rebel types

`common/rebel_types.txt`, a block each, in the file's order:

| key | what it is |
| --- | --- |
| `spawn_chance` | a factor and modifiers, as a mean time to happen: the type scoring highest in a province is the one that rises (section 4) |
| `movement_evaluation` | the same shape: where an army marches (section 9) |
| `area` | `nation`, or anything else; with `nation` the rebels stay in their country's own land and the faction dies with it |
| `ideology` | the ideology whose governments the faction prefers (section 6) |
| `defection` | `none`, or anything else; with anything else every hostile province counts towards independence |
| `independence` | `none`, or anything else; with anything else a rising frees a country (section 5) |
| `defect_delay` | whole months a province must be held before independence; `YEARS_UNTIL_BROKEN` x 12 where not given (24 in Their Finest Hour) |
| `unit` | the brigade type the rebels are made of |
| `resilient`, `reinforcing`, `smart` | yes or no (sections 9, 10) |
| `auto_convert` | yes or no: partisans of an exile's land go to the exile (section 7) |
| `is_triggered_only` | yes or no: only an underground may raise it (the fourth piece) |
| `siege_won_trigger`, `siege_won_effect` | a province's condition and effects, asked when the rebels take a province (section 10) |
| `can_enforce_trigger`, `demands_enforced_effect` | read, and **never asked** |

**`area`, `defection` and `independence` are two-valued**: a word other than `nation` or `none` is "yes" whatever it
says. `unit_transfer`, which Their Finest Hour writes in two types, **is no key**: the original drops it.

Their Finest Hour's five: `partisans` (in occupied land), `organized_partisans` (triggered only), `nationalist_rebels`
and `patriot_rebels` (in a country's own land, freeing somebody), and `disgruntled_rabble` (a country's own land, a
factor of 1 against the others' 10). Their texts are `<type>_title`, `<type>_name`, `<type>_army` and `<type>_desc`.

## 3. The roll

Once a day, **in each country's day** - after its spies and before its elections - for every country that owns a
province, and only while a game is running (not in a new game's first two days):

    for each province the country holds, in a real area:
        denominator = 10000, or 20000 where nobody plays the country
        risk        = what is shown (revolt.md, section 4)
        threshold   = round(risk / 50)                      risk in thousandths
        a draw % denominator below threshold, and then a draw % 365 of nought:  a rising of 3 (section 5)

- **Two draws**, the second only where the first passes. The first is drawn for every province, at whatever risk.
- **The one in 365 is the executable's own**, and so are the 50 and the halving for the AI; no define touches them.
- A risk below 0.025 rounds to nothing, and nothing can rise.
- Over a year: 1 point is 0.2% a province for a player's country, 10 points 2%, 47 - BlackICE's worst occupation - 9.4%.

## 4. Which type rises

The type a province chooses - for the roll, for `create_revolt` and for the tooltip's "Likely Rebels":

- where **the rebels hold it**, the type of the faction that holds it - the first whose provinces include it or one of
  whose armies stands there;
- otherwise each type in the file's order, the **triggered only** ones left out, and an **independence** type left out
  where it would free nobody (below), scores its `spawn_chance` - the factor, times each modifier's factor whose
  condition holds - in a scope of the province and **its owner**. **The strictly highest above nought wins**, so the
  first of equals; none above nought, none.

A modifier of factor nought whose condition holds vetoes a type: Their Finest Hour's `partisans` rise only where the
owner does not hold the province, the others only where it does.

**The country an independence type frees** in a province: none where the province is in no real area. The candidates
are each country with a core on it, then each country of the game in order whose **original capital** - its history's
`capital`, else its capital - is the province or a neighbour of it; each must not be the rebels, must own nothing, and
is taken once. Each scores 1000, **+200000 for a core on the province**, **+300000 where its original capital is the
province**; the first strictly highest wins.

## 5. A rising

Of a type in a province, with a number of brigades:

1. Nothing where the province is in no real area or is not simulated.
2. Fewer than 1 brigade is 1.
3. The faction it joins (below).
4. **The province's revolt risk is set to nought** - before anything is made, so also where nothing is.
5. With a faction: the province's holder, where it is not the rebels, is added to the faction's **targets**, the
   countries it fights, unless it is there; then the faction's army is made (section 7).
6. Without: a new faction (section 6).

**The faction a rising joins**, over the factions of that type:

- an **independence** type joins the **first** faction freeing the same country;
- a type with `area = nation` joins the **last** faction whose country owns the province;
- any other type joins none, and makes a faction each time.

## 6. A new faction

Where no faction is found: if the type frees somebody and the province would free nobody, the type is chosen again
for the province (section 4) **and not tested again**. With no type, nothing is made. Otherwise the faction holds:

- **its type**, and **its province**, where it rose;
- **its country**, the province's owner - or the underground's country for an underground's rising;
- **its target**, one: the province's holder;
- **its independence country**, section 4;
- **its government**, from the governments that are real and `valid_for_new_country`: 1000 each, **31000 where the
  government's ideologies hold the type's `ideology`**, +50000 for the country's present government, the first
  highest. Where none, the owner's government;
- **its name**, `<type>_name` with COUNTRY the country's adjective, INDEP the independence country's and EVIL the
  first target's - "Ethiopian Partisans", "Manchu Nationalists";
- no armies and no provinces;

and then its first army is made (section 7).

## 7. The army

One army a rising, made for the faction:

1. Nothing for nought brigades - before the next step lifts it.
2. **Its owner** is the rebels (`REB`) - or the underground's country for an underground's rising - or, where the type
   is `auto_convert` and the province's owner is **a government in exile** that does not hold it, **the owner**.
3. **One to four brigades**: the number is held to that.
4. Its name: "1st", "2nd", ... - the first not taken by one of the faction's armies - and `<type>_army` with the
   faction's texts: "1st Ethiopian Partisans". `ST`, `ND`, `RD` and `TH` give the ordinals, `TH` for 11 to 13.
5. Each brigade, of the type's `unit`, named as the faction's country names one, its home the province:
   - **its experience is -1.0 plus the first target's `partisan_efficiency`**, at most 100;
   - **each of its technologies at a level drawn below the first target's**: a draw % that level, nought where the
     target has none - one draw for each technology the target has above nought;
   - **at full strength and full organisation**.
6. **No leader.** The army stands in the province and looks for a battle there at once - **a rising where an enemy
   stands starts fighting**. Put there as any land unit entering a province is, it brings its fifteen days of supplies
   and fuel out of its owner's stockpile - of which the rebels have none, so only an exile's or an underground's
   partisans bring any (`supply.md`, section 14; read 2026-11-15).

**Who is told**, the holder being taken before the battle:

- **the holder is the player**: `REVOLT` "Revolt!" - "$REBELS$ are revolting in $PROV$!", REBELS the faction's name,
  REBELDESC its `<type>_desc`;
- **otherwise, the army is the player's** - an exile's or an underground's partisans: `PARTISANS` "Partisans!" -
  "Partisans rising up for us in $PROV$.", EVIL the holder's name;
- nobody else.

## 8. The effects, and a new game

- **`<rebel type> = N`** in a province: a rising of that type, of **the province's manpower times N** brigades,
  truncated. It names its type, so `spawn_chance` is not asked, and it tests neither the area nor the province first.
  Its words: `CREATE_REBELS` "§R$SIZE$§Y $TYPE$§W regiment(s) rise up in revolt in §Y$WHERE$§W." - SIZE before the
  army's 1-to-4.
- **`create_revolt = N`**: the same number, the type the province chooses (section 4), and only in a real area and a
  simulated province. Its words: `CREATE_REVOLT`.
- **A province history's `revolt = { type size name leader }`** sets the province's rebel type and size; the name and
  the leader are read and thrown away. As a new game is entered, after every province's revolt risk is given its
  support (`revolt.md`, section 3), each province with a type rises: **the size times its manpower, at least the
  size**, in the faction of that type it would join or a new one, and where the rebels then hold it, the faction
  takes it (section 10). No file of Their Finest Hour or BlackICE has one.
- **Land the rebels hold as a new game is entered** has its history's taking by them dated to the game's hour, so the
  month's `defect_delay` (section 9) counts from the start, at a later bookmark too, not from the history's date
  (`reversing/findings/FINDINGS-laterstart.md`, section 7; the date stored being the hour is *inferred* there; built
  2026-11-15 as `RebelsTook` set for every province the rebels hold, in `CRisings.AtNewGame`). No province of Their Finest
  Hour's history is the rebels' at any bookmark.
- **`release = TAG`**: every core of TAG the scope's country owns is released to TAG (section 12). TAG need not exist:
  this is how a country comes back. Nothing where it owns none.

## 9. A faction's day

Every faction, once a day, after each province's day, one after another; a faction that asks to go is taken away at
once.

**Friendly and hostile.** A country is **friendly** to the faction where the faction has no targets, or where it is
none of them and is at war with every one - the rebels counting as at war with everybody. A province is **hostile**
where it is **in reach** - the type's `area` is not `nation`, or the faction's country owns it - and its owner or its
holder is not friendly.

In order:

1. **A resilient type** pulls its units out of battles it should not be in (section 11). No type of Their Finest Hour
   or BlackICE is.
2. **It dies** where its country owns nothing and its type is `area = nation`;
3. or where its independence country owns land - its goal is won.
4. **Its provinces**: each the rebels still hold, owned, whose owner is friendly, is **handed back to its owner** -
   with everything a capture does, the damage to its buildings included; one the rebels do not hold, or unowned, is
   taken off its list.
5. **Its armies**: each that no longer exists, or is no longer the rebels', is taken off its list.
6. **It dies** with no armies and no provinces.
7. **It marches**: each army not moving, not falling back and not fighting -
   - its **target** is the province scoring highest on the type's `movement_evaluation` (below);
   - with none, where it stands on ground that is not hostile: to the faction's province where that is hostile; where
     not, and the province's holder is friendly, **the army is disbanded**;
   - it walks the way to the target, **cut after the first province in reach whose owner or holder is not friendly** -
     or after the first, where none is.

**Where an army marches.** A type both `reinforcing` and `smart` stays where its strength is below 0.66 a brigade (a
full Their Finest Hour brigade being 30). Otherwise, where the army stands in a real area, the candidates are every
province of its area and then of each area touching it, in order, each one hostile; each scores the type's
`movement_evaluation` in a scope of that province with **no country**, and **half that where it is not a neighbour**.
**The strictly highest above nought wins**, the first of equals. One draw seeds the scope each time it is asked.

## 10. Taking a province

**The rebels hold land; they never own it.** A province changing owner to the rebels keeps its owner.

**Who takes a province a unit stands in** with no battle, not falling back, on owned land: the unit's owner - and for a
rebel unit with a faction:

1. the faction's independence country, where the type does **not** free anybody and that country owns land;
2. then **the province's holder**, where it is friendly to the faction;
3. then **the province's owner**, where it is friendly - last, so it wins: partisans that win in their owner's occupied
   land hand it straight back.

It takes the province once no other land unit stands there, not falling back, that is an enemy of the taker - its
owner the rebels, the taker the rebels, or at war with the taker. **Where the province was the player's and is now
another's**, he is told `PROVINCELOST` "Province Lost!" - "We lost $PROV$ to $COUNTRY$.", COUNTRY the new holder's
name, or the name of the faction holding it where the rebels do. This goes for every capture, not only the rebels'.

**When the rebels become the holder** of an owned province: the first rebel army there whose faction does not already
hold it makes that faction **take it** - **the siege won**:

- one draw seeds a scope of the province, with no country and the faction;
- where `siege_won_trigger` holds: the owner, if he is the player, is told `REVOLTSIEGE` with the effect's words, where
  they are more than one character - a key with no message type in either install, so the null type - and
  `siege_won_effect` runs, whoever the owner;
- the province goes on the faction's list, whether the condition held or not.

**When the rebels lose a province**, the faction holding it drops it from its list.

## 11. A resilient faction

Nothing unless the type is `resilient`. For each of the faction's units in a battle, battle by battle until one acts:

- **fighting a friendly country** - it falls back;
- otherwise, **only where `smart`**: units marching into the battle's province from a land neighbour join their sides;
  ratio = their brigades over ours (at least 1); below 0.75 nothing; else powers summed, ours 10 where nought; the
  dice d = theirs - ours scale their power by 1 + d / 18 where d > 0, or ours by 1 + |d| / 4.5 where d < 0; where
  their power over ours times the ratio is above 5, it falls back.

It falls back to the hostile land neighbour scoring highest above -1000: +1000 for each brigade of a rebel army there,
-1000 for each other brigade, +4000 where the rebels hold it, -10000 where its holder is friendly; the first of equals.
A unit standing in the battle's province is sent there; one marching in turns back.

## 12. A faction's month, and independence

Every faction, once a month, at the end of the month's pass.

**Without an independence country - defection**: for each province on its list the rebels hold, with no land battle
and not its owner's capital, the day it may defect is the last day the rebels took it plus `defect_delay` months; the
first not yet due ends the month's pass; otherwise the country to defect to is asked, and **it is always nobody**: the
original's choice was compiled to an empty loop. **No province ever defects.**

**With one - independence**:

1. **The candidates**: the faction's provinces that are not their owner's capital, are hostile, and are a core of the
   independence country - or every one where the type's `defection` is not `none`. None, nothing.
2. The day: the earliest of their last days the rebels took them, plus `defect_delay` months. Not yet, nothing.
3. **15 in 100**, a draw % 100 below 15 - the chance is the candidates over the same set counted again, always 1, times
   15%.
4. **Independence**:
   - the same provinces are **released** to the independence country (below);
   - **every army of the faction becomes the new country's**: it leaves its battles, falling back where it held its
     ground, and keeps its place;
   - the faction's government becomes the new country's;
   - **for each country that owned any of them, a war**: the new country attacks it, a revolutionary war, named
     `NORMAL_WAR_NAME` "$ORDER$$FIRST$-$SECOND$ War";
   - the faction goes.

**Releasing a country** (`release`, independence; `FINDINGS-occupation.md` D.9) - the oppressor is the first province's
owner:

1. each province, in order: the released country and that province's owner are counted as at war a day ago both ways,
   once for each owner; **the released country owns and holds it**, a peaceful change of owner; it gets a core of the
   released country where it has none; where it is the released country's history capital and its capital is
   elsewhere, its capital moves there;
2. **every technology the oppressor has higher is raised to the oppressor's level**, at most the technology's highest;
3. where the infantry brigade may be built, **each released province with IC gets an army of three infantry
   brigades** of the released country, which looks for a battle;
4. `INDEPENDANCE` "Revolution!" - "$ACTOR$ declared independence from $OPRESSOR$.";
5. the released country's ministers' posts are filled.

## 13. What else

- **The rebels are at war with everybody** and everybody with them, with no war between them.
- **`num_of_revolts`** counts the provinces a country owns that the rebels hold (`revolt.md`, section 5).
- **Rebel acceptance** - `CExecuteRebelAcceptanceCommand` and `REBEL_ACCEPTANCE_MONTHS` - is posted by nothing and
  read by nothing. **The rebels make no demands**: `can_enforce_trigger` and `demands_enforced_effect` are never asked.
- **The tooltip of a province's revolt risk** opens with `LIKELY_REBELS` "Likely Rebels" and the `<type>_title` of the
  type the province would choose, where one is (`revolt.md`, section 7).
- **A rebel brigade has no organisation after its first hour.** The rebels' country has no officers - its history file
  is empty and nothing gives it leadership - and every country's officer ratio is counted each hour, the rebels'
  among them (`FINDINGS-occupation.md`, D.10): it is nought, and a brigade's ceiling is its figure times the ratio
  (`organisation.md`). So an army made at full organisation is at nought an hour later, and marches and fights so.
  That is what the rules as read come to, and the maintainer confirms it of the original on 2026-10-15: rebels "dont"
  have organisation.

## 14. Against the saves

All twelve of the maintainer's saves hold `rebel=1` and no `rebel_faction` block: **nothing has risen in that game by 20
April 1936**, which is what the roll's one in 365 on top of the risk's one in thousands leads to. There is nothing to
set a rising beside, so `OpenHOI3.SaveCheck rebels <install> [occupier] [occupied] [days] [seed]` is a trial, as the
battles' are: the occupier at war with the occupied and holding ten of its provinces away from its capital, a rising
of three in the first of them and another in the occupier's own land where no unit stands, and the days run.

On 2026-10-15, Germany and Poland for thirty days: Zarasai's partisans - "1st Polish Partisans", at -1.0 plus
Germany's partisan efficiency of 14 - **hand Zarasai straight back to Poland** and march off into the land Germany
holds; at Cranz, nobody there, the "Disgruntled Rabble" take the province, and the siege is won; Berlin would choose
nationalists freeing the German Democratic Republic, whose original capital it is. The roll makes nothing in thirty
days. Both armies are at nought organisation after their first hour (section 13), and the rabble's march to its
first target does not begin.

## What looks wrong and is built so

- The one in 365 on top of the risk's draw, which makes the roll all but never fire; the AI's half.
- Revolt risk set to nought by a rising that makes nothing.
- A brigade's experience and technology come from the faction's first target, the occupier; its experience starts at
  -1.0; its technology is a draw below the occupier's, so nought at a level of 1.
- A nation-wide type joins the last faction that matches and an independence type the first; the type chosen again is
  not tested.
- The independence country a capture hands land to only where the type frees nobody.
- Partisans that win in their owner's occupied land hand it straight back; the owner, asked last, wins.
- Defection never happens; the month's defection pass ends at the first province not yet due.
- The independence chance is a set over itself; a province handed back takes a capture's damage.
- Movement and the siege are asked with no country.
- `unit_transfer` is no key.
- An army with nothing left to fight beside a friendly home is deleted.
- `REVOLTSIEGE` has no message type.

- A rebel brigade has no organisation after its first hour.

## Ours

- **A real area is one of the supply network's areas** (`supply.md`, section 2), which is what the original's areas
  are read to be; and **every province is simulated**, there being one start and no scenario.
- **The roll goes through the provinces a country holds in their order**; the original's list is in the order it came
  to hold them, which decides which province's draws come first.
- **A rebel army and its brigades are numbered as any unit is**, and the rebels' own counter numbers only the factions;
  in the original the three share one counter of their own, which the save keeps.
- **A rebel brigade's name is taken**, as a new unit's is, so no two are called the same (*inferred*: the original's
  call that names it was not followed to see whether it takes the name).
- **A brigade keeps no home province.**
- **The day the rebels last took a province is kept on the province**, where the original reads it from the province's
  history, which is not built; it is set where the rebels become its holder with a capture's consequences.
- **The month's defection is not walked**: the original's walk asks the country to defect to of each province not yet
  due and always finds nobody, draws nothing and changes nothing.
- **A resilient faction's weighing of the odds is not built**: it asks a unit's power and its side's dice, neither of
  which is built. No type of Their Finest Hour or BlackICE is resilient. A resilient army falling back here leaves its
  battles first, as an order to march does.
- **A siege won is raised whatever the effect says**, and the interface drops it where the effect's words are one
  character or none, which is where the original does not raise it.
- **The army a release raises** has its brigades at level nought of every technology and at full strength and
  organisation (*inferred*: the original only sets their type).
- **A rebel army that changes hands** keeps its leader, it having none; the original moves the leader between the two
  countries' lists.
- **A type whose `unit` no brigade type has** raises nothing; the original raises brigades of no type.

## Not established

- The order of the history the month reads the last day the rebels took a province from.
- `Province_Defect`, which nothing reaches, read off the decompiler only.
- What the original does with a rebel army of under 0.1 strength in all, which its unit's hour hands to the game state's
  queue of units to remove - likely the army taken away, the queue's handling not read.
