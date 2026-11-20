using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Events;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim;

/// <summary>
/// What one hour does, in the original's order: the hourly pass every hour, then the
/// daily, monthly and yearly passes on the hours that turn a day, a month or a year.
/// See <c>docs/mechanics/tick.md</c>, section 3.
///
/// The passes are where the systems go as they are built. Each one's doc comment names
/// the order the original runs its steps in, and a system joins at its place in it.
/// </summary>
public static class CTick
{
	public static void RunHour(CGameState state)
	{
		CDate before = state.Date;
		state.AdvanceOneHour();
		CDate now = state.Date;

		RunHourlyPass(state);
		if (now != before)
		{
			RunDailyPass(state);
		}
		if (now.Month != before.Month || now.Year != before.Year)
		{
			RunMonthlyPass(state);
		}
		if (now.Year != before.Year)
		{
			RunYearlyPass(state);
		}
		// The AI comes after all of the hour's passes, the day's among them, every country reading the same world: what it
		// posts runs when the queue is next drained, before the next hour (read in the running game, tick.md, section 3).
		RunAiPass(state);

		// Last of all the original removes the units queued for removal during the hour, so that nothing is freed in the
		// middle of a pass - remove_brigade's among them (effects.md, section 9).
		Step(state, CPendingUnitRemovals.Drain);
	}

	/// <summary>
	/// Order: each country's units check their orders and combat; every unit's hourly
	/// update; every country's; the AI's trade; every province's; combat.
	/// </summary>
	private static void RunHourlyPass(CGameState state)
	{
		// The first thing the hour does: the landings on the player's ground are looked at, and a new one is told
		// (docs/mechanics/transport.md, section 11).
		Step(state, Transport.CInvasionWarnings.RunHourly);
		// The weather's own hour comes next, and its draws are the hour's first: the high pressure spreads, fronts are
		// born and step on (docs/mechanics/weather.md, section 4).
		Step(state, Weather.CWeatherFronts.RunHourly);
		// Each country's units look at their orders, look for an enemy and move, one
		// after another; only then is every unit's own hour run, which is where its
		// organisation comes back. Right after each country's units, its theatres: made where marked, looked after where
		// not, before the next country's units move (theatres.md, section 1).
		// Ours: the theatres run in a replay too, being cheap and making and disbanding headquarters units of the game's
		// (docs/replay.md).
		Step(state, s => CMovement.RunHourly(s, country => Theatres.CTheatres.RunCountryHour(s, country)),
			"CMovement.RunHourly and CTheatres.RunCountryHour");
		Step(state, Units.CEmptyUnits.RunHourly);
		// The units go before their countries, so an hour's organisation is worked out
		// with the officer ratio the hour before left.
		Step(state, COrganisation.RunHourly);
		// In the same hour of every fleet, too much aboard (docs/mechanics/transport.md, section 6).
		Step(state, Transport.CTransport.CheckEveryOverload);
		// Every country's own hour: its lend-lease looked at (docs/mechanics/aid.md, section 3).
		Step(state, Aid.CLendLease.RunHourly);
		Step(state, Countries.CActingCapital.RunHourly);                 // ours: where in a country's hour beside lend-lease was not compared
		Step(state, COfficers.Update);
		// Every province's weather, after the units and the countries have had their hour and before the battles, which
		// alone see the hour's weather whole (weather.md, section 4).
		Step(state, Weather.CWeatherPass.RunHourly);
		// Each province's hour: a faction's progress is the points its members hold (docs/mechanics/factions.md, section 7).
		Step(state, Wars.CFactionProgress.RunHourly);
		// Then, in the same look, an owned province in no area whose holder has no ground of its own or a friend's beside it
		// goes to the enemy around it (war.md, section 11), and at its end one a building of which has moved has its
		// modifier made again (modifiers.md, section 3).
		Step(state, s => CProvinceModifiers.RebuildMoved(s, province => Wars.CStrandedLand.Run(s, province)),
			"CStrandedLand.Run and CProvinceModifiers.RebuildMoved");
		// The battles are fought last, each in turn.
		Step(state, CCombats.RunHourly);
		// An event left unanswered past its time answers itself (ours: the original's window counts
		// each frame, docs/mechanics/events.md section 7).
		Step(state, CEventPass.RunHourly);
		// Each AI country's trading, by the install's Lua (docs/mechanics/trade.md, section 9; ours:
		// which of the two runs first in the original's hour was not read).
		AiStep(state, Trade.CTradeAi.RunHourly);
		// Last, what countries know of the provinces units left and entered this hour (intelligence.md, section 5).
		Step(state, Intelligence.CProvinceIntel.RaiseHourly);
		// And the days out of supply of every province that changed hands in the hour (ours: the original counts each at its
		// change; supply.md, section 18).
		Step(state, Supply.COutOfSupplyDays.CountChangesOfHands);
	}

	/// <summary>
	/// What the AI does with the hour, after every pass the hour runs. The original runs it for every country at once on
	/// worker threads, each reading the same world, and runs what they posted afterwards; here the countries go in turn
	/// and what they post waits in the same way, so the one's commands are not seen by the next.
	/// </summary>
	private static void RunAiPass(CGameState state)
	{
		// Each AI country answers the offers made to it (docs/mechanics/agreements.md, section 2).
		AiStep(state, Agreements.CPactPasses.AnswerOffers);
		// Then its politics minister, in its own hour of the day (docs/mechanics/politics.md, section 12).
		AiStep(state, Politics.CPoliticsAi.RunHourly);
		// And its intelligence minister, in its own hour (espionage.md, section 15; ours: where beside the politics).
		AiStep(state, Espionage.CEspionageAi.RunHourly);
		// And its foreign minister's decisions and prepared wars (foreignminister.md, section 1; ours: where beside the
		// others in the hour).
		AiStep(state, Ai.CForeignAi.RunHourly);
		// And its production and tech ministers, each in an hour of its own, and the placing of its finished units
		// (ministers.md, sections 1 to 6; ours: where beside the others in the hour).
		AiStep(state, Economy.CProductionAi.RunHourly);
		AiStep(state, Research.CResearchAi.RunHourly);
		AiStep(state, Construction.CAiDeployment.RunHourly);
		// Then its unit agents: the order of battle under each theatre and each theatre's plan (agents.md, section 2; ours:
		// after the ministers, every country in turn).
		AiStep(state, Agents.CUnitAiPass.RunHourly);
	}

	/// <summary>
	/// Order: supply; each country's neighbours; each country's daily work, which
	/// includes its IC, its units and its politics; each province's daily work; events;
	/// intelligence.
	/// </summary>
	public static void RunDailyPass(CGameState state)
	{
		// A country's day, in the original's order: its manpower grows, its leadership is
		// worked out, its units lose the men who go home and draw their supplies, what its
		// provinces produced reaches the stockpile, its IC is worked out, and then its six
		// shares of IC are run and its four of leadership after them.
		Step(state, state.Warfare.DailyUpdate);                   // the very first thing the original's day does (strategicwarfare.md, section 2)
		Step(state, CStrategicResources.Update);                  // the first thing the original's day does
		// Supplies go out along the network before any country's own day, so a unit
		// draws on what reached its province this morning.
		Step(state, CSupplyPass.RunDaily);
		Step(state, CThreat.UpdateMostThreatening);               // before any country's day, as its neighbours are
		Step(state, Wars.CSurrender.CountVictoryPoints);          // and with them its cores' victory points
		Step(state, Countries.CWarRecord.CountDaily);             // and its war record and lost_IC (triggers.md, section 5.1)
		Step(state, CCountryModifiers.RebuildDaily);           // each country's triggered modifiers tested first
		Step(state, Politics.CNationalUnity.RunDaily);           // right after a country's modifier (politics.md, section 6)
		Step(state, Politics.CMinisterPosts.JoinDaily);          // a minister whose start date is today joins the pool
		AiStep(state, Ai.CAiStrategies.RunDaily);                   // before a country's day: its strategy, ten days old (aistrategy.md, 1)
		Step(state, Agreements.CNapBalance.UpdateDaily);          // before a country's day: its pacts' borders
		Step(state, Countries.CActingCapital.EnsureCapitals);     // at the head of a country's day: a capital it owns,
		Step(state, CNeutrality.RunDaily);                        // then neutrality
		Step(state, Wars.CExile.SwapDaily);                       // early in it, an exile made a puppet where it is held
		Step(state, Agreements.CExpeditions.RunDaily);           // a lent unit past its date goes home (expeditions.md, section 3)
		Step(state, Agreements.CPactPasses.RunDaily);            // its pacts that have run their course, and influence's upkeep
		Step(state, Aid.CTechnologySharing.CheckDaily);           // ours: where in the country's day was not placed exactly
		// Later in it, the alignment's step every interval and a day - run below, beside the surrender, because it reads
		// other countries' IC and factions as a day run a whole country at a time leaves them (alignment.md, section 3):
		// here every country's IC is noted before the day works it out.
		CIcAtDayStart icAtDayStart = CIcAtDayStart.Take(state);
		Step(state, CManpower.RunDaily);
		Step(state, CLeaders.ActivateDaily);                      // just after its manpower grows
		Step(state, CLeadership.Update);
		Step(state, CConvoyDay.Sail);                             // before a country's units draw
		Step(state, CMissions.RunDaily);                          // a unit's day begins with its order's
		Step(state, CDigIn.RunDaily);                             // and then with this
		Step(state, CUnitUpkeep.RunDaily);
		Step(state, Agents.CPlanWants.RefreshDaily);              // a headquarters' inactive plan, in the units' day (FINDINGS-liveread.md, section 76)
		Step(state, Transport.CTransport.CheckEveryOverload);    // the last of a fleet's day (transport.md, section 6)
		Step(state, CIntelGrants.CountDown);                     // what others were given to know of a unit, a day less
		Step(state, CResources.UpdateHomeProduction);
		// Then each country's economy, one country's whole stretch before the next's as the original's day runs it: its
		// production put in its stockpile, a puppet's dues (stockpile.md, section 7), what the sea cuts off of its land
		// gathered in a harbour, its debts repaid, its trade, its IC, its conversions and shares, and an exile's own pool
		// held within bounds (war.md, 14; ours: before its shares of leadership, which take none) - so a country's trade
		// sees the money of the countries before it made and not those after (tick.md, section 3; FINDINGS-liveread.md,
		// section 140).
		Step(state, RunEconomy, "CStockpile.AddHomeProduction to CExile.HoldPool, country by country");
		// Then, a country at a time: its shares of leadership, spies placed with the espionage share; its mission looked
		// at; its spies at work after the alignment's step (espionage.md, 5); its undergrounds (undergrounds.md, 3); the
		// roll for a rising (rebels.md, 3); an election where one is due and the seats where the ruling party holds none
		// (politics.md, 4-5) - one country's whole stretch before the next's, as the original's day runs, so each draws on
		// the game's generator where the original's does, and a spy placed in a country today is there for the catches of
		// the countries after it and not before (FINDINGS-liveread.md, sections 120 and 121).
		Step(state, RunLeadershipToSeats, "CLeadership to CCabinet, country by country");
		// Late in a country's day: the surrender. Each country's alignment step is taken just before its own surrender,
		// country by country, so that it sees the surrenders and the IC of the countries before it and not those after.
		Step(state, s => RunAlignmentAndSurrender(s, icAtDayStart), "CAlignment.RunDaily and CSurrender.RunDaily");
		Step(state, Wars.CExile.ReturnDaily);                     // and an exile leading its faction coming home
		// The convoys are made, unmade and manned at the end of each country's economy, above (RunEconomy).
		Step(state, Economy.CSpareManpower.UpdateDaily);          // at the very end of it, what it has to spare
		// Each province's own day, after every country's: what was bombed comes back, the suppression, the revolt risk's
		// step and land at peace back to its owner, a province at a time; and then the pool is held.
		Step(state, Weather.CClimate.RunDaily);                   // a province's day begins with its season (weather.md, 1)
		Step(state, CProvinceDay.RunDaily);
		Step(state, Revolts.CRebelFactionDay.RunDaily);           // each rebel faction's day, right after the provinces' (rebels.md, 9)
		Step(state, CStockpile.HoldWithinBounds);
		// Events, after the provinces' day; none while a game is still being made (ours: whether the
		// original asks them in a new game's first days was not read).
		if (!state.IsBeingMade)
		{
			Step(state, s => CEventPass.RunDaily(s), "CEventPass.RunDaily");
		}
		Step(state, Intelligence.CProvinceIntel.Rebuild);         // late in the original's day: what countries know
		Step(state, Wars.CVictoryConditions.RunDaily);            // and last, the victory conditions (factions.md, section 8)
	}

	/// <summary>
	/// What a new game runs before its first hour, in the original's order and with the
	/// clock standing still: a day; the filling of the provinces along the supply
	/// network; another day; a month's pass; and an hour's pass. It is why nothing on the
	/// first screen is what the history files say: two days of production, research and
	/// building are already behind it. All of it is run with the game "being made",
	/// which is when a convoy is manned for nothing and a brigade's organisation is held
	/// to its type's figure alone. See <c>docs/mechanics/tick.md</c>, section 6.
	/// </summary>
	/// <param name="weather">
	/// Whether the new game's weather is made first; a check that has given the provinces a save's weather leaves it out.
	/// </param>
	/// <returns>How many looks the Lua took at the laws once the days were run (<see cref="Politics.CPoliticsAi.RunNewGameLaws"/>).</returns>
	public static int RunFirstDays(CGameState state, bool weather = true)
	{
		bool was = state.IsBeingMade;
		state.IsBeingMade = true;
		RunEntryPass(state);
		// The shares run once with nothing to spend, as the original's entry runs them for every country: so every brigade
		// a doctrine behind its country gains a level before the first day (upkeep.md, section 5).
		CUpgrades.RunAtEntry(state);
		// As the original enters a new game, before its first day: every province with no revolt risk given its
		// partisan support, and made again (revolt.md, section 3); then the revolts the province history gives (rebels.md,
		// section 8).
		Revolts.CRevoltRisk.SetAtNewGame(state);
		CProvinceModifiers.Rebuild(state);
		Revolts.CRisings.AtNewGame(state);
		// Every province's temperature set to its target, and ninety-six hours of weather run with the clock standing
		// still, before the first day (weather.md, section 4).
		if (weather)
		{
			Weather.CWeatherPass.AtNewGame(state);
		}
		// Entering gives every province still without one a depot, before the days run, the history's changes of controller having
		// counted theirs once already (supply.md, section 18).
		Supply.COutOfSupplyDays.Enter(state);
		RunDailyPass(state);
		CSupplyStart.Fill(state);
		RunDailyPass(state);
		RunMonthlyPass(state);
		RunHourlyPass(state);
		// The entry's end: every country owning no province and not in exile taken off its wars and out of its faction, and
		// the played country's records of strategic warfare begun (war.md, section 4; strategicwarfare.md).
		Wars.CWarSides.RemoveLandless(state);
		state.Warfare.SetUpForNewGame(state);
		state.IsBeingMade = was;
		// Then the changes of law the install's Lua posted as the game was set up, which the original's queue runs once
		// its entry is over - after the days, and before the AI's first pass posts anything (politics.md, section 12).
		int lawPasses = Politics.CPoliticsAi.RunNewGameLaws(state);
		// What the first screen shows is worked out for the game as it now runs.
		RunEntryPass(state);
		return lawPasses;
	}

	/// <summary>
	/// What the original recomputes on entering a game, before any hour has passed, so
	/// that the first screen shows figures and not zeroes. It works the figures out and
	/// moves nothing: no stockpile changes here.
	/// </summary>
	public static void RunEntryPass(CGameState state)
	{
		CProvinceModifiers.Rebuild(state);
		CCountryModifiers.RebuildDaily(state);
		CLeadership.Update(state);
		CManpower.UpdateMost(state);
		CManpower.Update(state);
		CIndustrialCapacity.Update(state);
		// Each country's share of its home ports cut off, after its IC as the original's entry has it (modifiers.md, section 3).
		Provinces.CBlockade.UpdateLevels(state);
		// What each country made at home is left as its last day left it - a new game's second day, or the save's: nothing
		// the original's entering does writes it, the day's production being collected in a country's own day alone
		// (resources.md, section 3; reversing/findings/FINDINGS-savegame.md, A.5). So is what the units asked for, nothing in
		// entering writing it, and a new game's production, reinforcement and upgrade needs, its entry's run of the shares
		// having come before its days; a loaded game's three needs are that run's (supply.md, section 11; upkeep.md,
		// section 8; buildqueue.md, section 4).
		if (state.LoadedFromSave)
		{
			CProduction.RunNeedsAtLoad(state);
			// And the manpower each country has to spare, the one set-up a loaded game's entering gives every country, which
			// the production script's first day asks for (reversing/findings/FINDINGS-savegame.md, A.5;
			// FINDINGS-liveread.md, section 108). A save holds none of it.
			Economy.CSpareManpower.UpdateDaily(state);
		}
		COfficers.Update(state);
		// Every battle's sides given the hour's circumstances again, whatever its kind, attacker first, as the original's
		// entering does (reversing/findings/FINDINGS-savegame.md, A.5): what the AI's odds read before the battle's next
		// hour, and nothing a save keeps. Only a battle at sea or a bombing of ships saved before its first round draws -
		// the attacker's chance of a surprise, rolled again in that first round (savegame.md, section 20).
		foreach (CCombat combat in state.Combats)
		{
			CCombats.ApplyModifiers(state, combat);
		}
		Intelligence.CProvinceIntel.Rebuild(state);
		// Entering makes no country's candidate events: a new game's are the month's pass's among its first days, and a loaded
		// game has none until its month turns - the original's, its save keeping none (events.md, section 1).
		// The AI's first pass over its units runs on the first frame, before an hour is played: each country reaches its
		// gate and sorts its units into its theatres (agents.md, section 2.1). What it posts is run before the first frame.
		// A loaded game has its agents made before that pass and so gives its orders in it (savegame.md, section 2).
		if (state.LoadedFromSave)
		{
			// Every strategy is made again as a saved game is entered, so its ten days start here (savegame.md, section 16).
			foreach (Countries.CCountry country in state.Countries)
			{
				Ai.CAiStrategies.Make(state, country);
			}
			Agents.CUnitAiPass.MakeAgentsOfLoad(state);
			RunLoadAiPass(state);
			return;
		}
		Agents.CUnitAiPass.RunHourly(state);
	}

	/// <summary>
	/// A loaded game's second AI pass before its first hour, at the save's hour as the first: the running original makes
	/// two (<c>docs/mechanics/savegame.md</c>, section 2).
	/// </summary>
	public static void RunSecondOfLoad(CGameState state)
	{
		Agents.CUnitAiPass.ForgetLastPass(state);
		RunLoadAiPass(state);
	}

	/// <summary>
	/// One of a loaded game's two AI passes before its first hour: the whole of the hour's AI pass, each country's offers
	/// answered, its ministers counting down and working where the save's hour is theirs, its finished units placed and its
	/// agents, as the original's pass is the whole of a country's AI (savegame.md, section 2; FINDINGS-liveread.md,
	/// section 106). Run outside the replay's steps: a recording takes the AI's place from the first hour, entering running
	/// again as it ran.
	/// </summary>
	private static void RunLoadAiPass(CGameState state)
	{
		Agreements.CPactPasses.AnswerOffers(state);
		Politics.CPoliticsAi.RunHourly(state);
		Espionage.CEspionageAi.RunHourly(state);
		Ai.CForeignAi.RunHourly(state);
		Economy.CProductionAi.RunHourly(state);
		Research.CResearchAi.RunHourly(state);
		Construction.CAiDeployment.RunHourly(state);
		Agents.CUnitAiPass.RunHourly(state);
	}

	/// <summary>
	/// Order: each province's modifiers and the decay of its nationalism; each
	/// country's monthly work, which is where technology decays and its most manpower is
	/// worked out; the rebuild of every country's list of candidate events.
	/// </summary>
	private static void RunMonthlyPass(CGameState state)
	{
		// First, every province's modifier made again, and then its nationalism a twelfth of a year less - the month
		// before a new game's first hour included, which the start save holds (modifiers.md, section 3; occupation.md,
		// section 5).
		Step(state, CProvinceModifiers.Rebuild);
		Step(state, Provinces.CNationalism.DecayMonthly);
		Step(state, Missions.CNukeMission.RunMonthly);            // the bombs its technology makes (nukes.md, section 1)
		// The month's pass a new game runs before its first hour decays nothing: the
		// original takes nothing off an ability until a game is under way.
		if (!state.IsBeingMade)
		{
			Step(state, CResearch.DecayMonthly);
		}
		// War exhaustion after the decay, as the original's country month has it - and in the month before a new game's
		// first hour too (warexhaustion.md, section 2).
		Step(state, Wars.CWarExhaustion.RunMonthly);
		// The parties drift every month, the one before a new game's first hour included: the start save holds exactly
		// one drift (politics.md, section 3).
		Step(state, Politics.CParties.RunMonthly);
		// The relations' drift is not held back: the month before the first hour is what makes
		// a new game's relations the same both ways (docs/mechanics/diplomacy.md, section 2).
		Step(state, CRelations.RunMonth);
		Step(state, CManpower.UpdateMost);
		Step(state, Provinces.CBlockade.UpdateLevels);            // the share of home ports cut off (modifiers.md, section 3)
		Step(state, Espionage.CSpyMissions.ResetCaught);         // the spies caught this month, back to nought
		Step(state, CEventPass.RebuildCandidates);
		// Last, each rebel faction's month: its independence (rebels.md, section 12).
		Step(state, Revolts.CRebelFactionMonth.RunMonthly);
	}

	/// <summary>
	/// As a year begins: the sample of <c>nation_size_statistics</c>, the one thing of the original's yearly pass built
	/// (statistics.md, section 5); where the pass stands beside the day's and the month's was not established.
	/// </summary>
	private static void RunYearlyPass(CGameState state)
	{
		Step(state, Countries.CNationSize.SampleYearly);
	}

	/// <summary>
	/// Runs one of the AI's steps: as any other, or recorded, or played back in its place where the game is replaying a
	/// recording (<c>docs/replay.md</c>).
	/// </summary>
	private static void AiStep(CGameState state, Action<CGameState> step,
		[CallerArgumentExpression(nameof(step))] string name = "")
	{
		if (state.Replay is not { } replay)
		{
			Step(state, step, name);
			return;
		}
		replay.RunStep(state, name, s => Step(s, step, name));
	}

	/// <summary>
	/// Each country in turn, in the countries' order: its alignment step, where one is due, and then its surrender. The
	/// original runs a whole day for one country before the next, so a country's step reads the factions as the
	/// surrenders of the countries before it left them, and their IC as their day worked it out, while the countries
	/// after it still have what they had before the day; a surrender that puts a country into a faction on a step's day
	/// moves every later country's drift and no earlier one's. And a country whose day comes after a surrender of the
	/// day made its modifier and its effective neutrality at the head of that day with the threat the surrender's war
	/// goal spread: both are made again for it here, before its step (<c>alignment.md</c>, section 3).
	/// </summary>
	private static void RunAlignmentAndSurrender(CGameState state, CIcAtDayStart icAtDayStart)
	{
		bool surrendered = false;
		for (int i = 0; i < state.Countries.Count; i++)
		{
			Countries.CCountry country = state.Countries[i];
			if (surrendered)
			{
				CCountryModifiers.RebuildOne(state, country);
				CNeutrality.UpdateEffective(state, country);
			}
			CAlignment.RunDaily(state, country, icAtDayStart);
			surrendered |= Wars.CSurrender.RunDaily(state, country);
		}
	}

	/// <summary>
	/// The stretch of a country's day from its shares of leadership to its seats, a country at a time in the game's order:
	/// the shares, the mission, the spies' missions and counter-espionage, the undergrounds, the roll for a rising, an
	/// election and the seats.
	/// </summary>
	/// <summary>
	/// Every country's economy of the day, the whole of one country's before the next's (<c>docs/mechanics/tick.md</c>, section 3).
	/// </summary>
	public static void RunEconomy(CGameState state)
	{
		CTribute.ClearDaily(state);
		Wars.CExile.HandOverDaily(state);                         // the ledger of what exiles hand over, made anew (each
		                                                           // hands its supplies over in its own day, CProduction)
		Supply.CSupplyNetwork network = Supply.CSupplyNetworks.Refresh(state);
		bool[] owns = Diplomacy.CRelations.Owning(state);
		List<Provinces.CProvince>[] held = CResources.LandByHolder(state);
		CIndustrialCapacity.CDay ic = CIndustrialCapacity.Prepare(state);
		foreach (Countries.CCountry country in state.Countries)
		{
			// Its trade convoys looked at in its own day, after the earlier countries have traded (FINDINGS-liveread.md, 152).
			Convoys.CConvoyDay.LookAtTradeConvoys(state, country);
			CStockpile.AddHomeProduction(state, country);
			CTribute.RunCountry(state, country);
			CResources.GatherCutOff(state, network, country, held[country.Index]);
			Aid.CDebts.RepayCountry(state, country);
			if (owns[country.Index])
			{
				Trade.CTradeRoutes.RunCountry(state, network, country);
			}
			CIndustrialCapacity.Apply(state, ic, country);
			CProduction.RunCountry(state, country);
			Wars.CExile.HoldPool(country);
			// Its convoys made, unmade and manned at the end of its economy, before the later countries trade with them
			// (FINDINGS-liveread.md, section 152).
			Convoys.CConvoyDay.MaintainCountry(state, network, country);
			// Then its sliders, before the later countries' days trade with it (ministers.md, section 5). Ours: the
			// original weighs them last in the country's day, after its later stretches too, none of which was found to
			// change what the weighing reads.
			if (state.Replay is { } replay)
			{
				replay.RunCalledStep(state, "CProductionAi.BalanceCountry " + country.Tag.Value,
					s => Economy.CProductionAi.BalanceCountry(s, country));
			}
			else
			{
				Economy.CProductionAi.BalanceCountry(state, country);
			}
		}
	}

	private static void RunLeadershipToSeats(CGameState state)
	{
		Countries.CCountryFacts facts = Countries.CCountryFacts.Of(state);
		bool undergrounds = Revolts.CUndergrounds.Building(state) is not null;
		foreach (Countries.CCountry country in state.Countries)
		{
			CLeadership.RunCountry(state, country);
			Countries.CCountryMission.RunCountry(state, facts, country);
			Espionage.CSpyMissions.RunCountry(state, facts, country);
			if (undergrounds)
			{
				Revolts.CUndergrounds.Run(state, country);
			}
			if (!state.IsBeingMade)
			{
				Revolts.CRevoltRoll.Roll(state, country);
			}
			Politics.CElections.RunCountry(state, country);
			Politics.CCabinet.RunCountry(state, country);
		}
	}

	/// <summary>Runs one step of a pass, timed where a tool has asked for a profile.</summary>
	private static void Step(CGameState state, Action<CGameState> step,
		[CallerArgumentExpression(nameof(step))] string name = "")
	{
		if (state.Profile is null)
		{
			step(state);
			return;
		}
		long start = Stopwatch.GetTimestamp();
		step(state);
		state.Profile.Add(name, Stopwatch.GetTimestamp() - start);
	}
}
