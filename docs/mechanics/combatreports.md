# The record of finished battles

**Ours**: an enhanced game's only (`docs/enhanced.md`, *Interface*). Built 2026-11-22, for BlackICE's Combat Reports
page, by the user's word of that day: "This one should stay in the utility, but as you said needs the engine to keep
records."

## 1. What the original has, and what BlackICE's DLL adds to it

The original keeps a week of finished battles, each with its hour, its kind, its province and one side's country -
the winner's - and no losses (`CCombatHistory`; `airmap.md`, section 2;
`reversing/findings/FINDINGS-combat.md`). So it cannot say what a war cost.

BlackICE's DLL catches every battle as it ends and keeps what it catches in text files beside itself, because it has
nowhere in the game to keep it: a campaign is told apart by a number in a variable of the country `OMG`, and every
load of a save starts a "session" under it, so that the months fought again after an old save was loaded can be
told from the months they replace (`docs/bicelib/features.md` of the mod's repository, *the combat record*).

## 2. What this engine keeps

**A report for every battle that ends, in the game itself** (`CCombatReports` on `CGameState.CombatReports`,
`CCombatReport`): of every kind - on land, at sea, in the air, and the three bombings - and a raid on a convoy, which
is a battle. It is written where the week's list is written, as the battle ends (`CCombats.Finish`, `CConvoyRaid`),
and holds:

| | |
| --- | --- |
| the hour | the game's hour the battle ended at |
| the kind | land, naval, air, ground bombing, land bombing, naval bombing |
| the province | where it was fought |
| each side's country | **the first country the side fought as** (`CCombatSide.Countries`), which a beaten side keeps; none where a side had none |
| each side's losses | the strength the side lost over the whole battle, in thousandths, as its battle message totals it (`CCombatSide.Losses`): at sea a thousand a ship sunk |
| each side's men | how many it had in the fight as the message counts them - each type's tally cut to whole ones and added, a type at nought or below left out (`CCombatSide.Size`); a count of men only on land and at sea |
| who won | the side that alone still had a country in the fight (`CCombatSide.Standing`); nobody where both had or neither - every bombing |

Every figure is a total the battle held as it ended. Nothing of single units, leaders or ships is kept.

**Both sides are always named**, where the DLL on the original has the loser only where a second list could be
read: the side's own list of the countries it fought as is not emptied when it is beaten.

**No report is ever taken off**, and there are no campaigns, sessions or files: the record is the game's own state.
**A game loaded from an earlier save has that save's record**, which is what the DLL's sessions work out from
outside.

**Only an enhanced game keeps it.** A faithful game records nothing, and its save stays the original's key for key.

## 3. Where it is and is not

- **In an enhanced save** (`savegame.md`, section 22; `CSaveCombatReports`): a key a battle. So a game saved and
  loaded shows what it showed before - the rule on a whole game's replay (`docs/enhanced.md`) is kept.
- **Not in the state's hash, nor the play checksum**: nothing in the game reads the record, so it cannot make two
  games play apart. It is made from the battles, which are hashed.
- **The same on every machine** of a game between several, each making it from the same battles.

**It grows for the whole game.** A report is ten figures; a long war has tens of thousands of battles, the air's
and the bombings' the most. *Not decided*: whether old reports are to be gathered into monthly sums, or dropped
after so many years - listed with the save's other bloat in `docs/enhanced.md`.

## 4. What a page reads

Through `OpenHOI3` in a page's `Collect` (`modwindows.md`, section 6):

- `OpenHOI3.CombatTally(tag, from_day, to_day)` - the country's battles that ended from one day to another, both
  whole, counted for each kind: `land`, `naval`, `air`, `ground_bombing`, `land_bombing`, `naval_bombing`, each
  `{ combats, won, lost, as_attacker, as_defender, kills, losses }` - `won` and `lost` only where a winner is known,
  `losses` its own side's and `kills` the other's, in thousandths - and `recorded`, how many reports the game holds
  in all (`CCombatReports.Tally`).
- `OpenHOI3.LastCombats(tag, count)` - its last battles, the newest first: `{ ended, kind, result, role, against,
  enemy_losses, enemy_men, own_losses, own_men, province }`, `ended` as `year.month.day hour:00`, `result` `won`,
  `lost` or empty, `role` `attacker` or `defender`, `against` the other side's tag or `---`.
- `OpenHOI3.Today()` - `{ year, month, day, hour, day_number }`; `OpenHOI3.DayNumber(year, month, day)` - a date's
  day as a number, nil for what is no date; `OpenHOI3.DateOf(day_number)` - back, as three numbers. Days are counted
  as the game counts them, years of 365. The last two ask no game, so a page may call them as it draws.

**A country is counted for a battle where it is the attacker or the defender named** - the first of its side - so a
battle fought beside an ally under the ally's name is the ally's. As on the original.

**No test of what the player knows**: a page shows any country's battles, as the DLL's does.

## 5. Checked

`CombatReportTests`: a report of a land battle with both sides, their losses and men and the side left standing as
winner; a bombing with no winner; a faithful game keeping none; a tally between two hours, both among them, by kind;
the last battles; the record written into an enhanced save and read back, a faithful one writing nothing, a report
that is no report left out and told.

On 2026-11-22, BlackICE's working copy played from a linked stand-in install as Ethiopia, which Italy is at war with
on the first day, twenty days run with `--click=h480`: five battles in the record, one of them Ethiopia's - a land
bombing by Italy at Dolo on 18 January, with no winner and nothing lost on either side - shown on BlackICE's Combat
Reports page with a land battle at Dolo still going. **No battle with losses and a winner was seen on the page**:
those are the test's.

**"Nothing lost on either side" was a fault, and the user's finding the same day**: "The land bombings dont track
the casualties." A bombing settled its damage and added nothing to its sides' losses, which a battle on land, at
sea and in the air each does - the engine's own gap, in a faithful game too, the original summing them for a
bombing as for a battle (`bombing.md`, section 9; `reversing/findings/FINDINGS-combat.md`). Mended: the same raid
then showed the bombers losing 6 and Ethiopia 134, where the fact base watched 2.2 and 289.8 on a raid of the same
two countries in the original - the same kind of figure, not the same raid.
