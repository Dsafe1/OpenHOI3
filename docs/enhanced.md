# Candidates for the enhanced engine

The normal engine does what the original does, its faults included. The maintainer plans an **enhanced** version that changes or fixes some of them. This file is the list of what it might change. **Nothing here is decided, and nothing here is built.**

Add to it whenever a spec records something of the original's that looks like a mistake. Say what it does, why it is a candidate, and where it is written down. When the enhanced engine takes one up, mark it done here and leave the normal engine as it is.

**Kinds:** *bug* (the code does not do what it plainly means to), *oversight* (a case nobody thought about), *display* (what the screen says is wrong or misleading), *dead* (a rule, figure or control that does nothing), *unfinished* (a feature left half made).

**Risk** says what a change would disturb. *None* means only what the player sees. Anything that moves a figure can also part the game from the savegame checks in `CLAUDE.md`, which is why these belong in a separate mode.

**How this list was made (2026-11-09).** A survey of `docs/mechanics` by the phrases its specs use for such things ("kept as read", "looks wrong", "does nothing" and so on), with each hit read in context. Of 102 keyword-searched files, about 75 had flagged passages read in context, and 26, mostly tooltip, map and format specs, were searched only, so an oddity described in other words may be missing. Nearly all of this comes from reading the original's code, not from watching it; the maintainer has confirmed only the entries that say so. Section numbers are from the spec named.

## Asked for by the maintainer

- **A headquarters' supply tooltip could show its subordinates' use.** The unit panel's supply and fuel tooltips give only the unit's own use. For a headquarters the maintainer wants its own consumption and, separately, that of all the units under it. `unitpanel.md`, 10. *Unfinished.* Risk: none.
- **A returning unit's subordinates stay behind.** An expeditionary unit goes home alone, and everything under it stays with the host as the host's own. The maintainer wants this changed. `expeditions.md`, 3. *Oversight.* Risk: small; saves hold ownership.

## Land combat

- **Toughness counted twice.** A technology's toughness is added twice, and a unit's is double the sum of its brigades'. `units.md`, 2; `landcombat.md`, 2; `mapunits.md`, 4. *Bug.* Risk: large; mods are tuned to it.
- **Technology cannot change repair cost.** `repair_cost_multiplier` is never added when technologies are summed. `units.md`, 2. *Dead.* Risk: only files that set it.
- **Two traits do nothing in a land battle.** `fort_defence` is reached only for an attacker, and `surprise_chance` is read and thrown away. `landcombat.md`; `leaders.md`, oddity 12. *Dead.* Risk: moderate.
- **Support attack counts its hours twice,** in its own hour as well as the battle's. `landorders.md`, 10. *Bug.* Risk: small.
- **AI support attack dropped.** The way into a province with no battle goes with the order, so a support attack where nobody fights yet is lost. `landorders.md`, 10. *Oversight.* Risk: AI attacks.
- **Strategic redeployment restarts its attack delay** every hour on the way, drawing on the generator each time. `landorders.md`, 10. *Oversight.* Risk: small; rolls move.
- **Redeployment offered where impossible.** The row asks nothing about distance or a way, so it is open for a province it cannot reach and the order drops the hour it is given. Confirmed by the maintainer. `landorders.md`, 10. *Display.* Risk: none.
- **Amphibious defence dead, landing penalty a hundredth off.** `extra_amphibious_defence` is never read, and with no landing craft the penalty is -70.70%. `transport.md`, "What looks wrong", 6. *Dead.* Risk: moderate.
- **A route is not always the cheapest.** A province is settled the first time a step reaches it, and a cheaper way found later does not replace that. `movement.md`, 3. *Oversight.* Risk: marches and AI paths.
- **Rebels have no organisation.** Their country has no officers, so a brigade's ceiling is nought after its first hour; experience starts at -1.0. Confirmed by the maintainer. `rebels.md`, 13. *Oversight.* Risk: rebels far stronger.

## Sea

- **An interceptor goes out once,** then lies in port under its order. On the maintainer's "may be bugs" list. `navalmissions.md`, 5 and 12. *Bug.* Risk: moderate.
- **A sortie from away from base stalls,** given a path that does not begin where the fleet is. `navalmissions.md`, 6 and 12. *Bug.* Risk: small.
- **Friendly fire always ends on the last ship.** The walk that picks a friend does not stop at its roll; a raider's fleet of nine lost about a tenth of its strength in a month. `navalcombat.md`, 4; `navalmissions.md`, 12. *Bug.* Risk: moderate.
- **Reserve ignores priority 4,** and treats an aggressive fleet as a defensive one. `navalmissions.md`, 2 and 12. *Oversight.* Risk: small.
- **A patrol sinks convoys** every twelve hours, like a raider. On the maintainer's "may be bugs" list. `navalmissions.md`, 3 and 12. *Oversight.* Risk: moderate.
- **An escort hesitates an hour** at the first end of its beat, the first time. `navalmissions.md`, 7 and 12. *Oversight.* Risk: negligible.
- **Ships have no defence.** `sea_defence` and `surface_defence` are read by nothing in the fight; only `hull` protects. `navalcombat.md`, 5. *Dead.* Risk: large.
- **`reduction_modifier` does nothing.** Read once, as a fleet leaves a battle, and thrown away. `navalmissions.md`, 10. *Dead.* Risk: only if brought to life.
- **The pride gains experience per reinforcement,** before the watering down. `upkeep.md`, 4. *Bug.* Risk: small.
- **Pride-sunk dissent define unused.** The dialog's 2 is a literal, and `PRIDE_SUNK_DISSENT_IMPACT` is read by nothing. `brigadecard.md`, 6. *Dead.* Risk: none.
- **A carrier's room counts wings twice** in the reorganise window. `reorganise.md`, 7. *Bug.* Risk: small.
- **Carried supplies lost in landing.** A unit's days of supplies go into the sea it leaves; a paratrooper's seven days come from the province it last walked out of. `transport.md`, "What looks wrong", 4 and 14. *Bug.* Risk: small.
- **The invasion mission mixes units and brigades,** and sends every passenger to the flanks while under them. `transport.md`, "What looks wrong", 8. *Bug.* Risk: landings.
- **One passenger ends the invasion,** by being refused or having nowhere left. `transport.md`, "What looks wrong", 9. *Oversight.* Risk: small.
- **Boarding is not checked.** The command puts a unit aboard wherever the two are; only the panel limits it. `transport.md`, "What looks wrong", 1 and 13. *Oversight.* Risk: none in normal play.
- **Loading several units tests the last alone,** and unloading asks nothing of where the fleets are. `transport.md`, "What looks wrong", 11. *Oversight.* Risk: none.

## Air

- **A wing's defence does nothing.** Neither its defence nor `air_defence` counts. The maintainer: "Yes thats a bug in the original." `aircombat.md`, 8 and 9. *Bug.* Risk: large.
- **Nothing defends a bombing target.** No air defence, fort or screen protects it. `bombing.md`, 12. *Bug.* Risk: large.
- **A worn air superiority unit shuttles,** an hour out and an hour home, and can be fought while out. `aircombat.md`, 3 and 9. *Bug.* Risk: small.
- **A beaten air unit flies out to sea,** to the water with the most ports around it, not to its base. The maintainer did not know whether it is so. `aircombat.md`, 8 and 9. *Bug.* Risk: small.
- **A worn unit at its base cannot be attacked.** `aircombat.md`, 7 and 9. *Oversight.* Risk: moderate.
- **The intercept scores without a war test.** `aircombat.md`, 5 and 9. *Oversight.* Risk: small.
- **A leaderless bomber almost never picks ships,** so naval and port strikes wear the port and hardly touch the fleet. `bombing.md`, 12. *Oversight.* Risk: moderate.
- **A fleet in port fires its air attack three times.** `bombing.md`, 12. *Bug.* Risk: moderate.
- **Two units cannot bomb one province's ground.** The second finds nobody to be against and turns home. `bombing.md`, 12. *Oversight.* Risk: moderate.
- **A wing with no strategic attack still bombs,** one run an hour. `bombing.md`, 12. *Oversight.* Risk: small.
- **Installation strike bombs radar only.** `bombing.md`, 12. *Unfinished.* Risk: moderate if widened.
- **Interdiction takes the last province of its list** its country sees into, whether anybody is there or not. `bombing.md`, 12. *Oversight.* Risk: small.
- **The air orders' priority is never sent,** though the window shows, steps and remembers it. `fleetsandair.md`, "The air window". *Dead.* Risk: only if honoured.
- **Transport planes wrecked by a drop,** left at a hundredth of their organisation, on an enemy or not. `transport.md`, "What looks wrong", 15. *Oversight.* Risk: small.
- **Air supply's accounting.** It counts its load before capping by what lies there, picks the last short province and not the neediest, and delivers whether the target is still a friend's or not. `transport.md`, "What looks wrong", 18. *Oversight.* Risk: small.

## Leaders

- **Later history is ignored.** Nothing after a leader's first rank applies, and a dated `skill` does nothing. `leaders.md`, 1, oddities 2 and 8. *Dead.* Risk: historical data would suddenly apply.
- **An order of battle's leader goes on anybody,** another country's or the wrong rank included. `leaders.md`, oddity 1. *Oversight.* Risk: small.
- **One-brigade divisions are given no leader** by the choosing. `leaders.md`, oddity 3. *Oversight.* Risk: small.
- **`paradrop_mission` never counts** in the choice of a general. `leaders.md`, oddity 4. *Bug.* Risk: negligible.
- **`defence_modifier` suits only units slower than 3,** so militia at exactly 3 miss out. `leaders.md`, oddity 5. *Oversight.* Risk: small.
- **Command reach falls off a cliff,** from half to nothing at exactly twice the radius. `leaders.md`, 8 and oddity 9. *Bug.* Risk: moderate.
- **An under-ranked leader loses his chain's bonus,** offence, defence and ground traits, while other traits still reach the unit. `leaders.md`, oddity 11. *Oversight.* Risk: moderate.
- **A tactic's `trait` condition ignores the chain.** `leaders.md`, oddity 13. *Oversight.* Risk: small.
- **`dissent_impact` works on land only.** `leaders.md`, 10 and oddity 15. *Dead.* Risk: small.
- **`seawolf` cannot be earned by raiding** (*inferred*). `leaders.md`, 11 and oddity 16. *Bug.* Risk: small.
- **`has_combined_arms = no` holds where `= yes` does.** `leaders.md`, oddity 17. *Bug.* Risk: conditions change.
- **`is_winner` holds for both sides** of a battle broken off. `leaders.md`, oddity 18. *Oversight.* Risk: small.
- **The portrait bypasses the greyed button** (*inferred*), so a leader can be changed in a battle by clicking his picture. `leaders.md`, 12 and oddity 20. *Bug.* Risk: none.
- **Other countries' leaders can be changed** from their units' panels. `leaders.md`, 12 and oddity 21. *Oversight.* Risk: none.
- **Promoting or demoting takes the general off his unit,** and the window's buttons go on acting on him. `leaders.md`, 12 and oddity 23. *Oversight.* Risk: small.
- **The starting stockpile ignores leaders,** being reckoned before they are chosen. `leaders.md`, 5 and oddity 10. *Oversight.* Risk: start-save checks.
- **Skill 11's progress bar reads past its table.** `unitpanel.md`, 10.5. *Bug.* Risk: none.
- **"Remove all" closes the window before it is answered.** `leaders.md`, oddity 25. *Oversight.* Risk: none.
- **A lent unit's leader button is enabled again** by the routine that disables it. The normal engine already greys it, by the maintainer's choice. `compatibility.md`, entry 23. *Bug.* Risk: none.

## Diplomacy and war

- **Casus belli made in play do nothing.** Each is of the null type, and the condition answers by the types' `is_valid`. `war.md`, 9 and oddity 6. *Dead.* Risk: war goals.
- **Casus belli fields unused:** `months`, `truce_months`, `peace_cost_factor`, `penalty_factor`, `prerequisites`, `is_valid_join`, `mutual`, `war_name` and others. `war.md`, 2. *Dead.* Risk: only if brought to life.
- **`call_ally` is read and does nothing.** `war.md`, 9. *Dead.* Risk: small.
- **A widened war is second-class:** no threat, name or limited flag, while the relation hit and influence are still paid. `war.md`, oddity 1. *Oversight.* Risk: small.
- **Guarantors cannot be asked as allies.** `war.md`, oddity 2. *Oversight.* Risk: small.
- **A call with `value` unset is accepted for the player** without asking. `war.md`, oddity 4. *Bug.* Risk: none.
- **A truce counts from the last change of membership,** not a war's end. `war.md`, oddity 7. *Oversight.* Risk: small.
- **A faction leader joining as defender never leads.** `war.md`, oddity 9. *Oversight.* Risk: small.
- **A war can outlive its other side,** and `PEACEACCEPT` reaches a player who took no part. `war.md`, oddity 15; see `moving.md`. *Bug.* Risk: AI and war state.
- **`PEACESELF` and `PEACEDECLINE` mislead.** The first says "negotiating for themselves" and the whole war ends either way; the second's log line is about debt. `war.md`, oddities 16 and 17. *Display.* Risk: none.
- **Only the first attacker returns land at the peace.** `war.md`, oddity 13. *Oversight.* Risk: small.
- **A `handles_peace` goal runs everything anyway,** and the country fights on. `war.md`, oddity 23. *Bug.* Risk: moderate.
- **Annexed land goes to any occupier,** an ally of the annexed included; threat falls by a flat 9999; nothing marks the country dead. `war.md`, oddity 24. *Oversight.* Risk: moderate.
- **A puppet follows its faction leader, not its overlord;** with no faction it joins no wars. `war.md`, oddity 25. *Oversight.* Risk: moderate.
- **Surrender progress counts only owned cores.** One owning none is never near surrender. `war.md`, oddity 19. *Oversight.* Risk: large.
- **Dissolving an alliance costs an unlisted -30** of relation. `agreements.md`, oddity 2. *Oversight.* Risk: small.
- **Guarantees cannot be revoked by button.** `agreements.md`, oddities 3 and 5. *Unfinished.* Risk: small.
- **Guarantees end after twenty years** whatever their history says. `agreements.md`, oddity 9. *Oversight.* Risk: small.
- **Withdrawn access ends both ways,** and `MILACC_CANCEL_RELATION_CHANGE` is read by nothing. `agreements.md`, oddity 4. *Bug.* Risk: small.
- **Two tests disagree for alliance and embargo:** what greys a button and what decides an offer. `agreements.md`, oddity 6. *Oversight.* Risk: none.
- **"Unlikely" means never.** A chance of 10 to 30 reads "Unlikely" and is always declined. `agreements.md`, oddity 7. *Display.* Risk: none.
- **The null faction's progress overflows.** `factions.md`, 7 and oddity 10. *Bug.* Risk: none.
- **The join dialog prints the alliance's costs.** The faction's action charges nothing. `factions.md`, oddity 1. *Display.* Risk: none.
- **Debt is repaid for ever.** It is never lowered, and one under 18.250 is never repaid. `aid.md`, 1 and 5. *Bug.* Risk: moderate.
- **Revoking debt charges the debtor,** and the message names the wrong party. `aid.md`, 5. *Bug.* Risk: small.
- **The lend-lease cap is enforced by nothing;** over it the receiver gets nothing that day. `aid.md`, 5. *Oversight.* Risk: small.
- **Sharing theories collapses wholesale.** A new sharer of one theory ends every theory the old one gave. `aid.md`, 5. *Oversight.* Risk: small.
- **A Retrieve's date disagrees with its delay.** `expeditions.md`, 1. *Display.* Risk: none.
- **An empty expedition offer.** Clearing a fleet's box leaves Accept up, posting nothing useful. `expeditions.md`, 1. *Bug.* Risk: none.

## Politics and espionage

- **No province ever defects.** The original's choice was compiled to an empty loop. `rebels.md`, 12. *Dead.* Risk: rebel outcomes.
- **Rebels make no demands.** `can_enforce_trigger` and `demands_enforced_effect` are never asked. `rebels.md`, 13. *Unfinished.* Risk: new system.
- **Risings all but never fire,** a one in 365 on top of the risk's own draw. `rebels.md`, 3. *Oversight.* Risk: large.
- **Revolt risk never decays** once the war ends. `revolt.md`, "What looks wrong". *Oversight.* Risk: moderate.
- **Suppression counts a province's own units twice** and reaches two edges, across the sea too. `revolt.md`, "What looks wrong". *Bug.* Risk: moderate.
- **Winning partisans hand land straight back.** `rebels.md`, "What looks wrong". *Oversight.* Risk: small.
- **Underground figures unused:** `UNDERGROUND_PARTISAN_STRENGTH` and `local_underground`. `undergrounds.md`, 1. *Dead.* Risk: none.
- **Organized Revolt uses a different threshold** from the day and the buttons. `undergrounds.md`, "What looks wrong". *Oversight.* Risk: small.
- **Occupation policy is barely checked,** and outlives the war and the peace. `occupation.md`, "What looks wrong". *Oversight.* Risk: small.
- **Spy bands short-change countries.** The cursor never wraps within a day, and a presence in a country that does not exist thins everyone else's share. `espionage.md`, "What looks wrong". *Bug.* Risk: moderate.
- **Missions at home run every mission,** and threat raised at home is everybody's threat from the country itself. `espionage.md`, "What looks wrong". *Oversight.* Risk: moderate.
- **`espionage_bonus` helps the wrong way.** The target's bonus lowers the chance of catching spies, its own at home included. `espionage.md`, "What looks wrong". *Bug.* Risk: moderate.
- **Foreign counter-espionage catches the target's own spies at home.** `espionage.md`, "What looks wrong". *Bug.* Risk: small.
- **`modify_spies` adds a count it never read,** and `has_hostile_spy_mission` compares with a figure nothing sets. `espionage.md`, 13. *Bug.* Risk: only scripts using them.
- **Minister intelligence effects do nothing:** `peace_offmap_intel` and `offmap_political_intel`. `intelligence.md`, 1. *Dead.* Risk: small.
- **Radar stays on the list when lost,** and the far radar ring is never reached. `intelligence.md`, "What looks wrong". *Bug.* Risk: small.
- **The law dialog's Ok checks nothing.** `politics.md`, 11. *Oversight.* Risk: small; whether the command refuses was not confirmed.

## Economy and production

- **The null technology charges budget for nothing.** `notech` costs 125 and takes 1250 of a new game's head start. `research.md`, 9. *Bug.* Risk: start-save checks.
- **Building orders are barely checked,** and a new line's size is never capped. `buildinglines.md`, "What looks wrong". *Oversight.* Risk: small.
- **A line's price ignores its size,** and an order joining a line is not priced again. `buildinglines.md`, "What looks wrong". *Oversight.* Risk: moderate.
- **The escort rule is enforced by a button only.** `convoylines.md`, "What looks wrong", 1 and 2. *Oversight.* Risk: small.
- **A trade's cancel cost is shown and never charged.** `trade.md`, 15. *Display.* Risk: small if charged.
- **A route with no convoy is accepted and dies the next day.** `trade.md`, 3 and 15. *Oversight.* Risk: small.
- **Trade runs only in the `from` country's day.** One owning no province never trades. `trade.md`, 15. *Oversight.* Risk: small.
- **A seller is paid money nobody gave.** `trade.md`, 15. *Bug.* Risk: small.
- **Britain and France trade over land.** Two countries on one continent need no convoy, whatever lies between. `trade.md`, 15. *Oversight.* Risk: moderate.
- **Depot step 4 is nearly dead,** and may name a province that is not that area's depot. `supply.md`, 5. *Bug.* Risk: breaks the exact match with saves.
- **Fuel and supplies use different days,** and a share cut by one province's room stays cut for those after. `supply.md`, 12. *Bug.* Risk: large against saves.
- **Exile manpower replaces the whole sum,** where adding may be meant; not settled. `manpower.md`. *Bug.* Risk: small.

## AI

- **The AI loses multi-level research** from its queue the moment a level finishes. `ministers.md`, 9. *Bug.* Risk: AI research.
- **The lend-lease figure given to the script is always 0.** `ministers.md`, 9. *Bug.* Risk: AI lend-lease.
- **`GetRandomUnderGroundTarget` is a fixed pick.** `ministers.md`, 9. *Bug.* Risk: small.
- **The foreign minister's targets never come off.** `foreignminister.md`, 11. *Oversight.* Risk: AI diplomacy.
- **An agent with no unit ends the stance pass** for the hour. `agents.md`, 11. *Bug.* Risk: AI land.
- **A submarine in a mixed fleet always ends alone.** `agents.md`, 11. *Bug.* Risk: AI fleets.
- **A garrison is rated higher the weaker it is.** `moving.md`, 18. *Bug.* Risk: AI land.
- **The terrain figure's neighbour term re-reads the province's own terrain.** `moving.md`, 18. *Bug.* Risk: AI attacks.
- **The AI's air sunset ignores longitude.** `moving.md`, 17.4 and 18. *Oversight.* Risk: small.
- **Multi-role wings alone fly nothing.** `moving.md`, 17.4 and 18. *Oversight.* Risk: AI air.
- **AI transport planes are never rebased.** `moving.md`, 17.1, 17.3 and 18. *Bug.* Risk: AI air.
- **The theatre alignment test counts x twice and never y.** `theatres.md`, 11. *Bug.* Risk: AI theatres.
- **A rival's distance is halved, not scaled.** `aistrategy.md`, 11. *Oversight.* Risk: breaks the exact match with the start save.
- **Prepared wars are dropped and prepared again.** `aistrategy.md`, 11. *Oversight.* Risk: AI war timing.

## Interface

All of these are risk-free but for what the entry says.

- **Influence's colour never reaches the screen.** `topbar.md`, 7. *Bug.*
- **Tribute sent to a master shows green with a plus.** `topbar.md`, 14. *Display.*
- **Diplomacy rows go stale:** threat, name and flag. `diplomacyscreen.md`, 8. *Display.*
- **A zero balance reads "surplus"** on a red icon. `diplomacyscreen.md`, 8. *Display.*
- **The highest-threat tooltip describes the wrong thing.** `diplomacyscreen.md`, 8. *Display.*
- **The relation breakdown hides the largest cause,** the +2000 of an overlord and a puppet. `diplomacyscreen.md`, 8. *Display.*
- **`ruler_label` is never filled.** `diplomacyscreen.md`, 8. *Unfinished.*
- **The intelligence screen's `self_button` does nothing.** `espionagescreen.md`. *Dead.*
- **Estimates repeat by month,** every target sharing a month's draws. `espionagescreen.md`. *Bug.*
- **IC is exact though called an estimate,** and the pie and ministers show everything. `espionagescreen.md`. *Display.* Fixing it reveals less.
- **The spies' tooltip says ten whatever the define.** `espionagescreen.md`. *Display.*
- **The province view goes stale** until reselected. `provinceview.md`. *Display.*
- **The claims box shows cores,** the owner's among them. `provinceview.md`. *Display.*
- **Intelligence keys are one out:** `INTEL_2` is "Intel lvl 3". `provinceview.md`. *Display.*
- **The port tooltip misstates the rule.** `provinceview.md`. *Display.*
- **The statistics graph section is dead.** `statistics.md`, 2. *Unfinished.*
- **The victory alert names only the last faction.** `endgame.md`, 6. *Bug.*
- **Closing the menu leaves the game paused** after opening any of its windows. `ingamemenu.md`, 2. *Bug.*
- **Message settings half-work.** A click on a name does nothing, no tab starts ticked, and Reset leaves the rows. `ingamemenu.md`, 4. *Oversight.*
- **A right click with several fleets or air units moves nobody.** `selection.md`, 8. *Oversight.*
- **Attrition is shown a hundred times too small,** and a terrain's own is left out. `builders.md`; `brigadecard.md`, 6. *Display.*
- **The positioning penalty lacks its hundredfold.** `reorganise.md`, 7. *Display.*
- **Tooltips can leave the screen** at the left or top. `tooltips.md`, oddity 2. *Oversight.*
- **The air map misses updates,** and a count of 128 battles hides the icon. `airmap.md`, oddities 1 and 2. *Bug.*
- **The second-target hint is backwards.** `fleetsandair.md`, "The air window". *Display.*
- **Script text errors.** REMOVE_SPIES_EFFECT says "Add", SPIES_INFILTRATED says "Grk", about fifty keys show as their names. `scripttext.md`, 5. *Display.* Mods may ship their own.
- **A fleet's passenger list goes stale.** `transport.md`, "What looks wrong", 2. *Display.*
- **Effects of nothing are red,** and more encirclement bonus is red. `leaders.md`, oddity 27. *Display.*
- **The elections line says `YEARS` though it counts months.** `politics.md`, 11. *Display.*
- **Sliders with nothing needed show a hundred times the IC.** `sliders.md`. *Display.*
- **Revolt tooltip mismatches, and its alert is never shown.** `revolt.md`, 7. *Unfinished.*
- **Offers past the tenth are invisible,** and still decline themselves after a week. `agreements.md`, oddity 11. *Oversight.*
- **The trade list goes stale** when one route ends as another starts. `trade.md`, 15. *Oversight.*

## Script, events and weather

- **A spent fire-once event blocks its on_action list.** In the base game 620 never fires behind 619, nor 720, 870 or 970 behind theirs. `events.md`, 8. *Bug.* Risk: events that never fired begin firing.
- **On_action weights are never read.** `events.md`, 8. *Dead.* Risk: event balance.
- **Province events with a mean time never fire on their own.** `events.md`, 8. *Dead.* Risk: dormant events begin firing.
- **Political decisions are read and never offered.** `events.md`, 9. *Unfinished.* Risk: content would appear.
- **`limit` inside a scope is ignored, and the effects beside it run.** BlackICE writes it 63 times. `script.md`, 6. *Bug.* Risk: mods' behaviour changes.
- **A `<unit type> = N` condition holds only for N of nought or less.** `triggers.md`. *Bug.* Risk: script results.
- **The south has no season.** Latitude is nought everywhere south of the equator. `weather.md`. *Oversight.* Risk: large for southern theatres.
- **Season 5's doubling is dead** while `TEMPERATURECHANGESPEED` is positive. `weather.md`, "What looks wrong", 3. *Dead.* Risk: small.
- **Decision rows are never re-enabled** until the screen is reopened. `events.md`, 12. *Oversight.* Risk: none.

## Existing departures that could become switches

- **A released puppet joins its master's supply at once** here, where the original takes some days. `compatibility.md`, entry 9. Risk: event chains timed on it.
