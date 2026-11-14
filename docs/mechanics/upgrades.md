# Changing a brigade's type

What the original's `common/unit_upgrades.txt` lets a player do to a brigade: turn it into another type. Not the upgrade
share of IC, which raises the levels of a brigade's technologies (`upkeep.md`, section 5). No addresses and no offsets.

Source: `reversing/findings/FINDINGS-upgrades.md`, read 2026-11-08, static. **One line of it was set beside a save of the
maintainer's**: the 1 March 1936 autosave's Infanterie-Regiment 8, infantry to motorised at 0.499 of its strength, a reserve;
its line costs 2.403 IC and takes 78.5 days, and here it shows 2.40 and the date those days come to.

Hardcoded unless it names a file or a define.

## 1. What may become what

`common/unit_upgrades.txt` (**mod-able**): a block `unit_upgrades` of `upgrade = { base = X target = Y }`, each adding Y to X's
list, duplicates and all, a name that is no type being dropped. Once the file is read, **each type is given what those on its list
may become**, pass after pass until nothing is added, the types taken in their database's order within a pass. So a list reads
the file's order first and then what the passes appended, and that is the order it is shown in. Ordering a type not on the
list is left to the interface to prevent: the command does not ask.

## 2. The order

**A brigade may be changed where it stands** if its unit is in no battle and not falling back, a fleet is in a province with a
naval base built, an air unit with an air base built, and the province is in the same area as its owner's acting capital. The
owner must be able to build one more of the target. Neither the men nor the IC are asked for.

Carried out (`CUpgradeRegimentCommand`), in order, **the brigade ceasing to exist at the end of it**:

1. A line of one, with the numbers any line takes, aimed at the unit, a reserve where the brigade was, its builder the owner
   (saved as the line's `builder`; `units.md`, section 5).
2. Its share of strength: its strength over its most, in thousandths, truncated and held to 1000.
3. One item of the target type: **a level of each of the target's technologies as the owner has now**; the old brigade's
   name; its experience, at least nought, and `PRIDE_BONUS_EXP` (**mod-able**) for a pride.
4. The men: that share of the old brigade's manpower given back, and the new brigade's taken; the pool stops at nought, and **a
   shortage refuses nothing**.
5. Priced (section 3) and added to the end of the queue. The unit notes the line.
6. **The brigade is taken out of the unit and is gone.** A unit left with none is an empty unit like any other, and the hour
   clears it (`attaching.md`).

No message is raised. The AI never does this: the only way it could, a Lua binding, is called by no script of the base game's or
BlackICE's (a grep of those files).

## 3. The line's price

Worked out every time a line is priced, from the new brigade at the item's levels and the old type, plain, **at the same sum of levels**:

    credit   = (UNIT_UPGRADE_COST x share / 1000) x the old type's IC a day / 1000
    cost     = the new brigade's IC a day - credit; under 1 thousandth, nought
    teaches  = cost x 1000 / the new brigade's IC, x UNIT_UPGRADE_PRACTICAL_MOD / 1000; 1 where it costs nothing to begin with
    days     = the new brigade's days - the old type's days x UNIT_UPGRADE_TIME; under 1 thousandth, nought

All divisions truncate. The three defines are **mod-able** (`military`): 0.4, 0.7 and 0.9 in Their Finest Hour.

**In the queue** its counter is the new type's, its name the old brigade's, and under it `=> ` and the unit it is for - which
every line aimed at a unit has, the theatres' among them. Its tooltip says what it changes from and to and, delayed, the
experience it will keep. Cancelling it gives back its men, **and not the brigade**.

## 4. A finished line

Delivered as any brigade: at the full strength of its type (a reserve's factor apart), its own new organisation, its experience the
order's or what it gathered if that is more, what it teaches scaled by section 3. **A pride is not a pride any more**: only its
experience came across.

A line ordered for a unit then joins it (`CDeployment.JoinOrdered`), if that unit may take the brigade wherever it stands or is a
theatre that may take the unit; at that unit's province, or else the nearest place in the home area. **A unit that is gone, fighting,
full or otherwise unable leaves the new brigade waiting to be placed**, as any finished one. A brigade joining is told as
`TRANSFEREDSUBUNIT`.

## 5. The single panel

`upgrade_button` on a brigade's row shows while the brigade may be changed where it stands and has any target the player may still
build; a press lights it and opens `select_upgrade_window`'s `list`: a `select_upgrade_entry` for each target, its button `target`
the type's name, and a last row `UPGRADE_CANCEL`. A row orders the change and closes the list; another row's button, a change of
selection and Cancel close it too. **The list stands at the button's position in its row plus the row's offset in the panel, taken as a
place on the screen**: not inside its window, which the file puts 254 to the right of the panel, nor the panel, 108 down. The sum is
read; that it is a place on the screen, and one pixel lower, are measured on the maintainer's screenshot of 2026-11-08, on
which a row's first target stands level with the panel's right edge and just above its row.

**Above the brigades' rows, a row for each line changing one**: its counter is the new type's, `subunit_name` is `UNIT_UPG_PROGRESS`,
the percentage to a tenth, and everything else on it is hidden or empty; it takes no click, and all of it answers with the line's tooltip.
The button's own tooltip is `UNIT_CAN_UPGRADE_TO` and a line for each target.

**The button shows only for the player's own units**, by the maintainer's word on 2026-11-08: foreign units "should not be manually
upgradeable". The reading found no test of the owner in the routines it read, so where the original makes it was not found.

## 6. The multi-unit panel

`upgrade_all` opens `upgrades_window`'s list (or shuts it, lighting while up): a row for every type some selected brigade may
become, in the order found, and Cancel. A row posts one order for **every** brigade of the selection that may change where it stands
and has the target on its list. Its tooltip is `MULTI_SEL_UPGRADE_DESC` with the number of such brigades.

**Ours**: the window is moved above the panel's rows, which here are the panel's own children and would otherwise cover it.

## 7. The counter and the save

A unit with a line under way shows the orange corner, after red and before yellow, to its own player (`mapunits.md`, section 2).

A line's item is saved with `upgrading_from_subunit` and `upgrading_from_subunit_str`, its aim as a line's `target`. A unit's list of
lines is not saved: it is made again from the queues on a load.

## Not settled

`UNIT_UPGRADE_PRACTICAL_MOD`'s value for a mod without it (here nought); a row's other
mouse handler; the exact order of a chain longer than two, which follows from the loader.
