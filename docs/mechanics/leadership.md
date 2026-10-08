# Leadership: how much a country has, and what its four shares make

How the original works out a country's leadership each day and what each part of it does,
written as rules this project can implement. No addresses and no offsets. Each rule says
how sure it is - `confirmed`, `likely`, `inferred` in the fact base's vocabulary - or
**measured**, meaning checked on 2026-10-07 against a savegame the original wrote on
1 January 1936 with no mods. Where this project chooses its own behaviour the paragraph is
marked **Ours**.

Sources: `reversing/findings/FINDINGS-leadership.md`,
`reversing/findings/FINDINGS-espionage.md` (*The espionage leadership slider*),
`reversing/findings/FINDINGS-production.md` (*The siblings*), `reversing/CLASSES.md`
(*Leadership, and the four sliders*).

Research has its share and its number of projects here. What a project costs and how it
advances is a spec of its own, `research.md`.

All arithmetic is in thousandths unless it says otherwise, and every division truncates;
see `numbers.md`.

## 1. How much a country has

Once a day, for every country, before its IC is worked out; and once on entering a game.
`confirmed` throughout.

**The province sum.** For each province the country **controls**:

    part = (province leadership + province LOCAL_LEADERSHIP)
           * (1 + province LOCAL_LEADERSHIP_MODIFIER)

- The bracket on the right is never taken below nothing, and a part that comes out negative
  is not added.
- The province's own leadership is what its history file gives it (`resources.md`, section
  2). The two modifier values are the province's (`modifiers.md`, section 3). In Their
  Finest Hour only two things touch them at the start of a game: `overseas`, which takes
  90% off, and `non_core`, which takes 80%.

**The total.**

    total = (the province sum + country GLOBAL_LEADERSHIP)
            * (1 + country GLOBAL_LEADERSHIP_MODIFIER + the technology bonus)
    total is never below 1

- **`GLOBAL_LEADERSHIP` is a flat amount, and it is inside the multiplication.**
  `base_values` gives every country 3.5, so a country that holds nothing has 3.5 times one
  plus its laws: 4.2 with the best education law.
- **The technology bonus is added to the modifier, not multiplied with it** - the same
  shape as IC's. It is what a technology's `leadership_gain` adds up to over the country's
  levels.
- `GLOBAL_LEADERSHIP_MODIFIER` is on the four education laws, from -15% to +20%, and on one
  minister type.

**Sharing technology costs leadership.** After the total is worked out, each technology
category the country shares with another takes `SHARE_TECH_LEADERSHIP_COST` off it - a
define, 1 in Their Finest Hour. It is never left with less than 1: while it could not pay,
the newest agreement is cancelled. `confirmed`

## 2. The four shares

A country divides its leadership between four things, always in this order: `confirmed`

| | share | what the leadership makes |
| --- | --- | --- |
| 0 | officers | officers |
| 1 | diplomacy | diplomatic influence |
| 2 | espionage | spies |
| 3 | research | the number of things it may research at once |

- **A share is a fraction of the total, and the four add up to one**, in the same second
  kind of number the shares of IC are held in (`production.md`, section 1). `confirmed`
- **Every country starts with a quarter on each.** (measured: 100 of 100 in the savegame.)
- **What each is given to divide is the whole total**, turned from thousandths into the
  shares' own kind of number, which drops what does not fit: 4.2 becomes 137625/32768.
  Nothing is taken from it between one share and the next. `confirmed`
- **They are worked through in order once a day**, straight after the six shares of IC.
  `confirmed`
- **Every country with a tag does this except the rebels**, including one that holds no
  province. `confirmed`, and (measured): East Germany, which does not exist until 1945,
  trains officers and spies and earns influence every day.
- **Leadership is not a stock.** What a share does not use is gone; nothing carries over.

In one of the original's stand-alone battle scenarios the officer share is set to nothing
every day. `likely` There are no scenarios here.

## 3. Officers

    officers += share * leadership * LEADERSHIP_TO_OFFICERS * (1 + OFFICER_RECRUITMENT)

`confirmed`. The define is 4 in Their Finest Hour. `OFFICER_RECRUITMENT` is the country's
modifier of that name, which the conscription laws set: -50% for a volunteer army, up to
+50% for service by requirement. The pool is never below nothing.

(measured) East Germany, with 4.2 leadership, a quarter of it on officers and a volunteer
army, has 4.198 officers two days in: 2.099 a day. Nationalist Spain, on a one-year draft,
has 6.298.

The arithmetic is done in the shares' kind of number, each product dropping what falls
below it, and turned into thousandths at the end:

    a = share * leadership            the allocation
    b = LEADERSHIP_TO_OFFICERS * a
    c = b * (1 + OFFICER_RECRUITMENT)
    officers += c, as thousandths, rounded down

**What a country's officers start as** is what its units call for, times its history's
`officers_ratio`: `units.md`, section 7. (measured, 95 countries of 100)

## 4. Diplomatic influence

    influence += share * leadership * LEADERSHIP_TO_DIPLOMACY

`confirmed`. The define is 0.5. No modifier is read, and **there is no upper limit**; the
pool is never below nothing. The arithmetic is the officers' without its last step.

**A country starts a game with one point of influence for every five of its base IC, in
whole points.** `confirmed`, and (measured) for every country in the savegame: Germany's
base of 144 gives 28, the United States' 256 gives 51, and a country with only the five IC
`base_values` gives has 1. It is the base and not the total - the province sum in whole IC
before any law (`ic.md`, section 3). The five is built into the original.

## 5. Spies

    spies in training += share * leadership * LEADERSHIP_TO_SPIES

`confirmed`. The define is 0.15. **Here the allocation is turned into thousandths first**
and the define applied afterwards, where officers and influence apply theirs first: a
quarter of 4.2 is 1.049, and 1.049 x 0.15 is 0.157 with the rest dropped.

Whole spies are then sent out, to the countries the player or the AI has given a priority,
highest priority first; a fraction waits. That part belongs to espionage and is in
`reversing/findings/FINDINGS-espionage.md`.

## 6. Research

The share makes nothing by itself. It decides **how many things a country may research at
once**:

    projects = the share, as thousandths * leadership, to the nearest whole number

`confirmed`. Two details decide the edge cases: the share is turned into thousandths
before it is multiplied, and a figure exactly between two whole numbers goes to the
**even** one - 2.5 is two projects and 3.5 is four.

That number is for the interface and the AI: **the share's leadership itself is handed
out one point to each technology in the queue, in order, until it runs out**, so a country
with 3.4 to spend advances four projects, the fourth at 0.4. What a project costs, how
fast it goes and what finishing it does is `research.md`.

## 7. Ours

- **A country's leadership is recalculated once a day and on entering a game**, like its
  IC, from its provinces' modifiers and its own.
- **The four shares are changed only by a command**, which refuses four numbers that are
  not all at least nothing and no more than one between them.
- **Officers, influence and spies in training accumulate; nothing spends them yet.**
  Officers are for units, influence for diplomacy and spies for espionage, none of which
  exists here. Spies are not sent anywhere, so the figure is a pool that only grows.
- **Officers start with what the country's units bring** (`units.md`, section 7).
- **There is no sharing of technology**, so nothing is taken off the total for it.
- **The research share is spent on the country's queue** (`research.md`). The number of
  projects is worked out and shown, and nothing enforces it: the queue may be longer, and
  what is beyond the leadership simply waits.

## 8. What the savegame does and does not confirm

The total is not saved. What three of the four shares made in the save's two days is, so
`tools/OpenHOI3.SaveCheck` gives each country the laws, the government and the technology
levels the save has and compares:

- **the shares**: a quarter each, 100 of 100;
- **spies in training** against two days of section 5, less whole spies already sent: 95
  of 100;
- **officers** and **influence**, less two days of sections 3 and 4, being whole numbers:
  95 of 100 each;
- **the influence each country started with** against a fifth of its base IC: all 22 that
  do not exist, and 73 of the 78 that do.

**The same five fail all of them, and each has a triggered modifier.** Poland, the
Netherlands and France have a tenth more `GLOBAL_LEADERSHIP_MODIFIER` than their laws give,
the United States a flat 0.15 more leadership, and the United Kingdom both.
`triggered_modifiers.txt` gives Poland +10% while it holds the Corridor and any country
+0.15 for a hundred ships. Those need the script's conditions, which are stage 4; the
whole part of their starting influence is right.

Three more - Belgium, Portugal and Republican Spain - fit only once `overseas` is applied
to their African provinces (`modifiers.md`, section 3).

## What is not established, in one place

- which **triggered modifier** each of the five above has: what a country's modifier is
  made of is read now (`modifiers.md`, sections 4 and 5), and their conditions are the
  script's;
- who records what a country **shares technology** with.
