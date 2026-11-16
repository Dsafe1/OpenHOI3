# War exhaustion

What a country's wars wear it down by. Built 2026-11-07, read out of the original in
`reversing/findings/FINDINGS-diplostate.md` (section C.3, the month), `reversing/findings/FINDINGS-leadership.md`
(section 10, a new game's starting figure), `reversing/findings/FINDINGS-effectrun.md` (the effect) and
`reversing/findings/FINDINGS-triggergrammar.md` (the condition); the classes are `Sim/Wars/CWarExhaustion` and
`CCountry.WarExhaustion`.

A figure marked **[exe]** is fixed in the original's code; one marked **[mod]** comes from a file a mod may change.

## 1. The figure

Each country has one, in thousandths like every figure of the game, **never below nothing**. In Their Finest Hour it
does one thing: **it is paid back to neutrality when a war ends** (section 2). The `war_exhaustion` static modifier
times the figure is a step of the country's modifier (section 4), but Their Finest Hour's is empty, and so is
BlackICE's.

## 2. The month

In the country's monthly pass, after technology decays, for every country - those that do not exist included [exe]:

    the rebels:          war exhaustion = 0
    every other country: war exhaustion += its war_exhaustion modifier, floored at 0
                         war exhaustion = at most its max_war_exhaustion modifier
                         where it is at war with nobody and war exhaustion is above 0:
                             neutrality += war exhaustion (floored at 0, not capped; effective neutrality made again)
                             war exhaustion = 0

The modifiers come from `common/static_modifiers.txt` [mod]: Their Finest Hour's `war` gives `war_exhaustion = 0.041`
("about 1/24 increase each month") and `max_war_exhaustion = 100`, `peace` the same maximum. **So a country banks 0.041
a month at war, and the first month of peace adds all of it to its neutrality at once** - two years of war is about a
point of neutrality. Neutrality is held at 100 only by the next day's pass (`diplomacy.md`, section 6). With BiceLib's
`disableWarExhaustionNeutralityReset` turned on by a mod's Lua, as BlackICE's does, every country's figure is put back to
nought instead each month (`bicelib.md`, section 3.4). BlackICE's
`war` gives `war_exhaustion = 0` and both maxima 1, so there only events move it.

The month before a new game's first hour runs it too, where technology's decay is held back (`tick.md`, section 6).

## 3. A new game

As a game is made, before its two days, every country is given [exe]:

    at war:   a twentieth for each month since the earliest of its wars began, at most 10
    at peace: 3, less a twentieth for each month since the latest of its finished wars ended, not below nothing

**Months are counted by the calendar** - the year times twelve and the month, two of them taken apart - so October
to January is three, whatever the days. A war began on the first entry of its history file and a finished war ended
on its last (`war.md`, section 1). On 1 January 1936 Italy and Ethiopia are three months into the war of 3 October
1935 and start at 0.150; no war of the history has ended by then, so every other country starts at nothing. The
month's pass of the two days adds 0.041: **a player sees Italy and Ethiopia at 0.191 on the first screen**.

## 4. The modifier

Step 10 of a country's modifier (`modifiers.md`, section 4) is the `war_exhaustion` static modifier [mod] multiplied
by the figure - what the file gives is for one point of it. Empty in Their Finest Hour and BlackICE alike.

## 5. Condition and effect

| keyword | answers / does |
| --- | --- |
| `war_exhaustion = N` (condition) | the scope's war exhaustion is at least N |
| `war_exhaustion = N` (effect) | adds N, never below nothing and with no ceiling; nothing is made again, so the country's modifier takes it at its next rebuild |

Their Finest Hour uses the effect once - France's `war_exhaustion = -5` on reuniting with Vichy (`surrender2.txt`) -
and BlackICE in ten files. What each says of itself is `scripttext.md`.

## 6. The save

`war_exhaustion` in each country block, between `duration` and `diplo_influence`, read and written (`savegame.md`).

## Measured against nothing

**No save of the maintainer's can check any of this**: every one holds `war_exhaustion=0.000` for every country,
1939's at war included. They were made with BiceLib's `disableWarExhaustionNeutralityReset` written into the
executable on disk, which replaces the month's addition with a store of nought - so the start save's Italy is 0.000
where the original gives 0.191 (`reversing/findings/FINDINGS-diplostate.md`, section C.6). The unpatched executable was
put back on 2026-11-07. Neutrality does not differ at the start - nobody is at peace with war exhaustion to pay back -
and `OpenHOI3.SaveCheck diplomacy` still gives 101 of 101; `write ... new` lists Italy's and Ethiopia's 0.191 against
the save's 0.000.

## Not established

- Who else in the original writes war exhaustion than the month, the effect and a new game - the fact base lists event
  or script writers beyond the effect as unread.
- Whether a war made and ended in play counts as a finished war for a new game's figure: here only the history's are,
  and nothing reads them after the start.
