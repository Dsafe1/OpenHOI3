# The headless run: 1936 to 1948, nobody playing

Stage 2 of the plan is done when "a 1936-1948 run completes headless with totals that are plausible against a real
savegame" (`readme.md`, *The plan*). This is that run, taken up on 2026-10-16 as item (5) of the maintainer's order of
2026-10-15. It is a tool and not a mechanic: nothing here is read out of the original, and no rule of the simulation
is changed by it.

## 1. What runs

`OpenHOI3.SaveCheck headless <install> [until year] [savegame ...]` makes a new game as the client makes it - the
history read, the install's Lua loaded, the two days a new game runs first - with **nobody playing**, and runs the
tick hour by hour to the first day of the year given, 1948 by default. Nothing is given to it by hand: no war, no
order, no mission.

What acts on its own is what is built: the tick's hourly, daily, monthly and yearly passes; the events and decisions
of the base game, the AI's choice of an option on the game's generator; the AI's answers to offers and the install's
Lua for every country nobody plays - trading, its politics minister and its intelligence minister, and since the same
day its tech and production ministers, which research, set the sliders and order units, buildings and convoys, and the
engine's placing of what they build (`ministers.md`). **There is no AI for anything else**: no country moves a unit
or declares a war but by script. So the run tests the economy, politics, diplomacy and script running unattended, and
**not** war. (Sections 3 and 4 are the run before the ministers.)

## 2. What it tells

- Twice a year, on 1 January and 1 July, the eight great powers' figures - IC, manpower, leadership, officers, the
  seven goods, units, brigades, the build queue, the technology levels summed, national unity, dissent, neutrality,
  the ruling ideology and the faction - and the world's: countries holding their capital, IC, units, wars, trade
  routes, how many countries research and build.
- Every day, any country's goods or manpower below nought, with the first day and how many days.
- As they happen, wars begun and over and countries changing faction.
- At the end: how long each year took; events fired by year and the most fired; decisions taken; the pacts' messages;
  the Lua's errors; and every key of script the events met that is not built, with how often.
- Given savegames of the same game, at each one's hour every country's stockpile, manpower, officers, influence and
  technology levels set beside the save's, as ratios.
- A run that throws stops there and says on which hour.

## 3. The run of 2026-10-16

`headless <install> 1948 Germany1936_04_20_03.hoi3`, Their Finest Hour unmodded, a Release build: **1 January 1936
to 1 January 1948 in 2,614 seconds** - about 192 a year, 0.53 a day; 1937 and 1938 ran slower while the test suite
shared the machine. **It completes.** Its first attempt threw on 29 April 1938 (section 4, the first item); with that
mended nothing threw, and no country's goods or manpower was below nought at any day's end.

### What moves

- **Events: 1,107 fired**, 167 in 1936 falling to 55 in 1947 - the politics events (`5040`, `5041`, `5005` the most,
  over 170 times each), the Spanish Civil War on 4 May 1936, Italy's war with Ethiopia carried on from the history,
  France's government changing at an election. Fewer each year: what fires once has fired, and a world that does not
  move gives the rest less to answer.
- **Trade: 994 routes** standing at the end, 33,260 offers accepted and 32,259 routes broken over the twelve years - the
  install's Lua trading for every country, no error from it.
- **Politics and spying**: the ministers act; laws and posts change; national unity drifts (France from 35 to 71).
- **IC** rises a little where events and technology give it: Britain 92 to 108, the United States 153 to 179, China 30
  to 36; the world's from 1,540 to 1,692.

### What does not, because there is no AI

- **No research**: no country queues any; the technology levels summed move only by events (Germany 197 to 202).
- **No production**: the 1936 build queues finish by 1938 and nothing is ordered after; **the world has 1,579 units in
  1948 as in 1936**.
- **The goods pile up to the ceiling of 99,999** - metal, energy, rare materials, crude oil and fuel for every great
  power by 1940 - and manpower and officers grow without end (the Soviet Union 1,173 to 3,186 manpower; Germany 21,779
  to 156,868 officers).
- **The production sliders stay where a new game set them**: France, Italy, Japan and China live on nought supplies and
  nought money for twelve years, and diplomatic influence piles up unspent (fifteen times the April save's by April).
- **No decision is ever taken**: the original's AI takes decisions in its Lua (`events.md`, section 9).
- **No war but by script**, and none goes anywhere: units do not move.

### Beside the save of 20 April 1936

Over 78 countries, ours over the save's, the median: fuel 1.000, crude oil 1.08, manpower 1.08, metal 1.29, supplies
1.31, energy 1.35, rare materials 1.37, money 0.87, officers 0.86, technology levels 0.80, diplomatic influence 14.9. So
in four months fuel, oil and manpower are the save's; the resources run a third high, money and officers low, and
research a fifth behind - each what an AI's spending, building and research would move. The save's own player,
Germany, is the farthest in manpower (908 there, 405 here) and technology (227, 197): the maintainer was playing it.

## 4. What it found

In order of what they cost the run:

1. **A unit's draw divided by nought** where its province's need was nought and its pool below nought - a capital,
   whose pool is its country's stockpile and is charged whatever it holds during the day. The original's division
   answers -1 there; ours threw. **Mended** to the original's rule (`supply.md`, section 13; `FINDINGS-supplypass.md`,
   section 4).
2. **`country_event` is not built**, and it is what event chains hang on. **The Spanish Civil War** breaks first: the
   Republic's event secedes the Nationalists' provinces and declares war, but `split_troops` (not built either) gives
   them no army, the Republic's own troops stand on their land and hold it, and Nationalist Spain capitulates the next
   midnight and is annexed - the war ends on its first day, and event 2002, the Nationalists' own, never fires. **Tannu
   Tuva** leaves the Comintern in January 1944 by event 2101 and the Soviet event 2102 that annexes it never fires, so
   it stays alone. `infantry_brigade`, an effect giving a unit, is met there too.
3. **The Lua: 448 errors, all one line** - `script/country/FIN.lua`, line 38, calling a province's
   `GetNumberOfUnits`, which the classes handed to the scripts lack; Finland's politics minister fails each day from
   June 1939. `JAP.lua` calls it too, so far in no arm the run reached.
4. **`lost_national`**, a condition of a triggered modifier in `common/triggered_modifiers.txt`, met 4,381 times and
   answered false each time: that modifier never comes on.
5. The Lua's unbuilt calls reported once each: tribute, the AI's strategy, releasing a puppet, liberating a country
   and the AI's war plans - all the AI's.

### After the four were mended (2026-10-16)

A shorter run, `headless <install> 1940`, at the maintainer's word ("you can do shorter runs for now"): 1936 to 1940 in
770 seconds, nothing thrown, nothing below nought. **The Lua: no error** (28 by the end of 1939 before). **The script's
gaps met: none** - every key the twelve years had met is answered or done. Events: 546 in four years, more than before
in each year but 1938, the follow-ups `country_event` fires counted among them.

**The Spanish Civil War still ends on its first day**: on 19 October 1936 the Nationalists get 113 provinces and 34
units by `split_troops`, the Republic's divisions standing in six of those provinces - Burgos, the new capital, and
Valladolid and La Coruña among them, all their victory points - take them at the declaration that comes first in the
event, and the next midnight the Nationalists capitulate and are annexed. **It waits for the AI**: in the original,
by the maintainer's account, the AI has moved the Republic's units to the Portuguese border by then and the Nationalists
get units in their capital (`effects.md`, section 9).

### With the tech and production ministers (2026-10-16)

Stage 5's first piece (`ministers.md`). Each snapshot now also writes every great power's research queue, its four
leadership and six IC shares, its units waiting to be placed and its summed research progress. `headless <install>
1937`: 1936 in 189 seconds, nothing thrown, nothing below nought, **no Lua error**; every country holding its capital
researching and building - Germany's technology levels 197 to 243, the world's units 1,579 to 1,686, 21 buildings
finished, none left waiting to be placed. **Set beside the Ireland game's save of 11 March 1936**, where every major
is the original's AI, the summed research progress of the eight majors on that day:

| | GER | ENG | FRA | ITA | SOV | USA | JAP | CHI |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ours | 12.10 | 8.51 | 5.71 | 4.73 | 6.10 | 9.20 | 7.90 | 2.01 |
| the original's | 12.20 | 6.62 | 3.26 | 4.46 | 4.40 | 9.02 | 6.66 | 2.10 |

The levels themselves are still the gap a new game has against the start save (`resources.md`, section 5) - the
original's start gives technology this one does not - so the progress is the measure, and those ahead are countries the
original starts further on. The Soviet Union's supply share ran low, 168 supplies on 1 January 1937, not looked into.
The draws having moved, the Spanish Civil War fired on 30 January 1936 and still ended on its first day.

### With decisions (2026-10-16)

Stage 5's second piece (`foreignminister.md`). Each snapshot also writes every great power's prepared wars and whether
each is ready, and as they happen the decisions taken, by whom. `headless <install> 1938`: 1936 and 1937 in 225 seconds
each, nothing thrown, nothing below nought, **no Lua error**; seven decisions taken by the AI where none was before -
`new_deal`, `trains_run_on_time`, `popular_front`, the three interventions in Spain and `great_officer_purge` - and
Japan's war on Shanxi and China prepared on 1 July 1937, waiting on fronts not built (`foreignminister.md`, section 6).
The first run found `CEU3Date:GetDayOfMonth` missing, which every country with its own decision script reads, and the
second the ceiling for war, which the politics script asks once a war is prepared; both are built. The draws having
moved again, the Spanish Civil War fired on 20 June 1936, and still ended on its first day.

**With the whole foreign minister**, the same day: the same seven decisions, no Lua error, and the ministers' pacts -
67 influences begun and ended, five alignments - with 1936 and 1937 in 254 seconds each. The Spanish Civil War fired on
14 March 1936. What each minister offers is told by `OpenHOI3.SaveCheck foreignai` (`foreignminister.md`, section 9).

### With the AI's strategy (2026-10-16)

Stage 5's fourth piece (`aistrategy.md`): every country's strategy made as the game is entered, every ten days and at a
change of war, and read by the scripts. `headless <install> 1937`: 1936 in 220 seconds, nothing thrown, nothing below
nought, **no Lua error**; three decisions taken (`new_deal`, `popular_front`, `trains_run_on_time`), 37 influences begun
and 35 ended, three alignments, 21 licences bought. The draws having moved, the Spanish Civil War did not fire in 1936
this time.

## 5. Not established

- ~~What keeps the original's Spanish Civil War going past its first day~~ - the AI, by the maintainer's account: it
  has moved the Republic's units away from the Nationalists' victory points by the time the event fires. It waits for
  the AI here.

- Whether the totals are "plausible against a real savegame" past April 1936: no save of the maintainer's lies later.
  A save of a later year, with nobody playing or with as little played as he can, would be the measure. The Ireland
  game's saves of February and March 1936 (2026-10-16) are the closest to that so far.

