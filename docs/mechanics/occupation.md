# Occupation

What a country does with the land of another that it holds: the **occupation policy** it has set towards that
country, a modifier every such province is given. And **nationalism**, the years a province remembers it changed
owner. Read out of the original on 2026-10-14 (`reversing/findings/FINDINGS-occupation.md`, sections 1.4 and A;
the change of hands `reversing/findings/FINDINGS-surrender.md`, section D). What else a province's modifier holds,
step by step, is `modifiers.md`, section 3. `confirmed` unless marked.

This is the first of four pieces occupation and revolt were cut into on 2026-10-14 - the cut is this project's:
occupation and a province's whole modifier; revolt risk; rebels; undergrounds.

## 1. The policies

`common/occupation_policies.txt` holds them, one block each, in the order of the file. A block is a modifier - every
key but `allow` is a kind and its figure, `local_ic = -0.95` and the rest - with an **`allow`** block, a country's
condition asked with the occupied country as FROM. Their Finest Hour's are `collaboration_government`,
`military_government`, `full_occupation`, `total_exploitation` and `civil_war`; the last has an `allow` and no
figures.

They are numbered from 1 in the file's order. **Number 0 is `no_occupation`**, a policy of the game's own with no
figures and no `allow`: it is what a country holds towards another before anything has set one. Its texts are
`no_occupation` "No Occupation" and `no_occupation_desc` "This is never displayed.".

**Every country holds a policy towards every country**, in the record it holds towards it (`diplomacy.md`, section
1), and it starts at `no_occupation` - in a new game, and for every country's record towards a country annexed.
Nothing resets it at peace, on a change of owner or when a war ends.

## 2. What sets one

1. **A capture.** Whenever a province's controller changes - a battle, an effect, a peace, the day's return of
   occupied land, a change of owner - and before the new controller is written, the **new controller's record
   towards the old controller** is looked at. While it holds `no_occupation`:
   - `civil_war` is set where its `allow` holds, asked of the new controller with the old as FROM;
   - otherwise policy number 1, the file's first - `collaboration_government` in Their Finest Hour.

   A policy already set is kept. So it is set once, on the first capture, and it is keyed by **who held the
   province**, not by who owns it (section 3).
2. **The player**, from the politics screen (section 4), by a command: the country setting it, the country it is
   set towards, and the policy by name. **It checks nothing** - an unknown name sets `no_occupation` - and it
   sets the policy, makes again the modifier of every province the setter controls that the other owns, and makes
   the setter's own modifier again. Nothing is told.
3. **The history**: a country's history line `<policy> = <TAG>` sets its record towards that country, with no
   `allow` asked. Their Finest Hour's three (`GER total_exploitation = POL`, `BUL full_occupation = GRE`,
   `JAP total_exploitation = CHI`) are dated 1939 and after.

**Nobody else sets one.** No AI chooses a policy, neither the executable nor the install's Lua, and no condition or
effect names one. **So an AI keeps whatever its captures set.**

## 3. What a province gets

When a province's modifier is made (`modifiers.md`, section 3, step 7), **the policy its controller holds towards
its owner** is added whole - except where the owner controls it, and where `REB` does. **There is no war test**: an
ally, or a country at peace, that holds somebody's province puts its own policy towards the owner on it.

This is the whole of what occupation costs a province. There is no separate cut to an occupied province's IC,
resources, manpower or leadership: the policy's `local_ic`, `local_resources`, `local_manpower_modifier` and
`local_leadership_modifier` go into its modifier, which is where those figures read it. Its
`local_partisan_support` is revolt risk's ceiling (`revolt.md`, section 3).

Because a capture keys by the old controller while a province reads controller towards owner, the two disagree
when a province is taken from a third country: Polish land held by Germany and retaken by France sets France's
policy towards **Germany**, while the province reads France's towards **Poland** - which may still be
`no_occupation`, so the province gets nothing.

## 4. The politics screen

**The list of occupied countries.** `policies_label` ("OCCUPIED_COUNTRIES", "Occupied Countries") sits over
`policies_listbox`, which is filled **when the screen goes up**, with one row, in the game's order of countries, for
each country that

- owns a province, and is not the player,
- is at war with the player - or either of the two is `REB` - and
- owns a province the player controls.

So a country at peace whose land the player holds gets no row, though the player's policy towards it still applies.

A row, `current_policy_entry`:

- `country_icon`: the country's flag;
- `country`: its name;
- `which`: the name of the player's policy towards it - `no_occupation`'s "No Occupation" before one is set - kept up
  to date while the screen is up (*inferred*: every frame);
- `select_button` opens the choice of a policy for that country.

The tooltip of `which` and of `select_button` is the policy's figures, a line each, as a law's are
(`modifiers.md`, section 8), and its `<key>_desc` as the delayed half.

**The choice of a policy**, `policy_selection_window`: `category` is the occupied country's name, and `policy_list`
holds one row, `possible_policy_entry`, for **every policy of the file in its order**, `no_occupation` left out and
none filtered:

- `name`: the policy's name; `effect`: its figures, a line each;
- the player's current policy towards the country hides `possible_indicator`, every other row hides
  `current_indicator` - so before any is set, no row is current;
- `select_button` may not be pressed **where the policy's `allow` does not hold**, asked of the player with the
  occupied country as FROM. **It may be pressed for the current policy.** Its tooltip is the `allow` block's account
  of itself (`scripttext.md`), conditions not met in red.

Pressing `select_button` posts the command (section 2) and shuts the window; there is no dialog and no cost.
`cancel` shuts it.

| key | English |
| --- | --- |
| `collaboration_government` | Collaboration Government |
| `military_government` | Military Government |
| `full_occupation` | Full occupation |
| `total_exploitation` | Total Exploitation |
| `civil_war` | Civil War |
| `civil_war_desc` | This is a Civil War - no repressive occupation policy is necessary. |

**No message** goes with a capture's policy, the command or the history.

## 5. Nationalism

A province's **nationalism** is a count of years, in thousandths.

- **A change of owner sets it to `YEARS_OF_NATIONALISM`** (the defines' `country` block; 20 in Their Finest Hour)
  wherever the old owner was a country - **whether or not the owner changed**: nothing tests that.
- **The month takes 0.083 off every province**, held at nought - a twelfth of a year, short of the twelfth by a
  thousandth of a year in twelve months. That is done in every month's pass, the one a new game runs before its first
  hour included, straight after the pass has made every province's modifier again - so a province's modifier carries
  the figure from before the decay until something next makes it again (`modifiers.md`, section 3).
- **The history.** A province's `owner` line where the province already has an owner counts as a change of owner.
  *Inferred* from the start save: Their Finest Hour has five provinces with two history files that each set an owner
  (3948, 3949, 4592, 4647, 4905 - two files of one name in two folders, or two layers with two names), and those
  five, and only those, hold nationalism 19.917 on 1 January 1936: twenty years, less the month the new game ran.
  How the history applies an owner line was not read.

What it does is in the province's modifier (`modifiers.md`, section 3, step 11): the `nationalism` static modifier,
times the years, where the province's owner holds it and has no core on it. Their Finest Hour's is
`local_revolt_risk = 0.3`, so 0.3 points of revolt risk a year, 6 on the day of the change. In all five of the start's
provinces the owner has a core, so it adds nothing there.

**The condition `nationalism = N`** is true where the scope's province has more than N whole years left: the figure
against N's whole part times 1000, strictly greater. On a country it reads no province and is false.

## 6. Against the saves

- **The start save** holds nationalism 19.917 in the five provinces of section 5 and none elsewhere, and no policy.
- **83 a month**: the same five hold 19.834 on 1 February, 19.751 on 1 March and 19.668 on 1 April and 20 April.
  Fifty-two Chinese provinces appear at 19.917 on 1 April - a change of owner in March, a month's pass since.
- **The capture's key**: from 1 February `ITA -> ETH` and `ETH -> ITA` both hold `collaboration_government`. The
  second is Ethiopia retaking a province Italy held - Ethiopia's record towards the old controller. From 1 April
  `CHI -> CGX` holds `civil_war`, which its `allow` names.

## What looks wrong and is built so

- A capture keys by the old controller while a province reads its controller towards its owner.
- The command checks nothing, and `allow` only greys the button - the current policy's included.
- The policy applies with no war test, while the screen lists only the countries at war with the player.
- A policy outlives the war and the peace.
- Nationalism is set again by a change of owner to the same owner.

## Ours

- **The policy is held by its number** in the list of policies, 0 for `no_occupation`, as the original's database
  holds it.
- **The history's policy lines are kept until the countries' records exist** and set then; it comes to the same.
- **A history line naming a policy that does not exist** is not a policy line at all and is passed over, as any key the
  country history does not know is.
- **A mod with no `civil_war`** has the file's first policy set on every capture. The original looks `civil_war` up
  by name and tests nothing, and is taken to fault there (*inferred*).

## Not established

- How the history applies an `owner` line - section 5 rests on the save.
- That the row's policy name is rewritten every frame.
- What the `allow` block's account says for a policy with none.
- Whether a capture ever hands an owner's province to a third party in play.
