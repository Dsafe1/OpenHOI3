# Numbers: thousandths, and the second kind

How the original holds a number, written as rules this project can implement. No addresses
and no offsets. Each rule says how sure it is - `confirmed`, `likely`, `inferred` in the
fact base's vocabulary - and where this project chooses its own behaviour the paragraph is
marked **Ours**.

Sources: `reversing/findings/FINDINGS-fieldmap.md` (*Numbers are fixed point*),
`reversing/findings/FINDINGS-airdefence.md`, `reversing/findings/FINDINGS-distribute.md`,
`reversing/CLASSES.md`.

## 1. Thousandths

- **A number the game shows with three decimals is a whole number of thousandths.** A
  country's officers read 237964731 in memory where the savegame, which is plain text,
  writes `237964.731`. `confirmed`, against a savegame for ten countries.
- **It is a 32-bit signed integer in the original**, so the largest value is about
  2,147,483.647. `confirmed`
- **Reading one from a script file is "the whole part times 1000, plus the fraction".**
  `confirmed` for that shape. `air_attack = 22` becomes 22000.
- **Not everything is thousandths.** Counts that cannot be fractional are plain integers -
  convoys and escorts match the savegame exactly as ints - and a few fields use a scale of
  their own: a unit definition's `distance` is stored times 100,000. So a field's scale is
  part of its definition and has to be checked per field, with the savegame as the oracle.
  `confirmed`
- **Dividing by 1000 is an ordinary signed integer division**, which truncates toward zero.
  `likely`: the compiler's idiom for it appears wherever a thousandths value is turned back
  into a whole number, and two write-ups read it that way independently.

## 2. The second kind: 48.15 binary

The original has another fixed-point type, and the compiler's own type information names it
exactly: a 64-bit value with 15 fractional bits, in which **32768 is 1**. `confirmed`

It appears where thousandths are too coarse or too small: a leader's experience, the four
leadership shares a country divides between officers, espionage, diplomacy and research,
the six shares it divides its IC between, and the IC and manpower cost of a whole build. It
has its own type here, `CFixed15`, rather than being squeezed into thousandths - the two
round differently, and a system specified in one must not be computed in the other.

- **A thousandths value becomes one by `value * 32768 / 1000`, truncated.** `confirmed` So
  0.1 is 3276, not 3276.8, and the savegame writes it back as `0.09998`.
- **It becomes thousandths again by `value * 1000 / 32768`, rounded down.** `confirmed`
- **A product drops what falls below the 15 places.** `confirmed`
- **The savegame writes one with five decimals, to the nearest.** (measured, on the shares
  of IC of every country in a savegame.)

## 3. What is not established

- **How a product or a quotient of two thousandths values is rounded** in general. Each
  formula that matters has to be read where it is computed; the specs for those systems
  say what was found.
- **What happens to a fraction longer than three digits** when a file is read: dropped, or
  rounded.
- **How a negative number smaller than one is read** - `-0.5`. "Whole part times 1000 plus
  the fraction" gives the wrong sign for it if done naively, and whether the original does
  it naively has not been read.

## 4. Ours

`CFixedPoint` in `lib/OpenHOI3.Sim` is thousandths.

- **It is 64 bits wide, not 32.** Nothing here is meant to overflow where the original
  would, and intermediate products need the room.
- **Multiplying and dividing truncate toward zero**, as integer division does - the
  `likely` rule above, applied everywhere until a formula's own spec says otherwise.
- **Reading keeps the first three fractional digits and drops the rest**, and a negative
  number is negative whatever its whole part is.
- **Writing always shows three decimals**, as the savegame does: `237964.731`, `5.000`.
- **No `float` or `double` reaches the game state.** They are not deterministic across
  machines in the way a simulation shared between players needs. Real time - how long a
  frame took - is the one place they appear, and it paces the game without being part of it.
