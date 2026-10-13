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

		// Last of all the original removes the units queued for removal during the
		// hour, so that nothing is freed in the middle of a pass. There are no units yet.
	}

	/// <summary>
	/// Order: each country's units check their orders and combat; every unit's hourly
	/// update; every country's; the AI's trade; every province's; combat.
	/// </summary>
	private static void RunHourlyPass(CGameState state)
	{
		// Every roll of the hour starts from the game's seed and the hour it is.
		state.SeedHour();
		// Each country's units look at their orders, look for an enemy and move, one
		// after another; only then is every unit's own hour run, which is where its
		// organisation comes back.
		CMovement.RunHourly(state);
		// The units go before their countries, so an hour's organisation is worked out
		// with the officer ratio the hour before left.
		COrganisation.RunHourly(state);
		COfficers.Update(state);
		// The battles are fought last, each in turn.
		CCombats.RunHourly(state);
		// An event left unanswered past its time answers itself (ours: the original's window counts
		// each frame, docs/mechanics/events.md section 7).
		CEventPass.RunHourly(state);
		// Each AI country answers the offers made to it (docs/mechanics/agreements.md, section 2).
		Agreements.CPactPasses.AnswerOffers(state);
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
		CProvinceModifiers.Rebuild(state);
		CStrategicResources.Update(state);                  // the first thing the original's day does
		// Supplies go out along the network before any country's own day, so a unit
		// draws on what reached its province this morning.
		CSupplyPass.RunDaily(state);
		CCountryModifiers.Rebuild(state);
		CThreat.UpdateMostThreatening(state);               // before any country's day, as its neighbours are
		CNeutrality.RunDaily(state);                        // at the head of a country's day
		Agreements.CPactPasses.RunDaily(state);             // its pacts that have run their course, and influence's upkeep
		CAlignment.RunDaily(state);                         // later in it: a step every interval and a day
		CManpower.RunDaily(state);
		CLeaders.ActivateDaily(state);                      // just after its manpower grows
		CLeadership.Update(state);
		CConvoyDay.Sail(state);                             // before a country's units draw
		CMissions.RunDaily(state);                          // a unit's day begins with its order's
		CDigIn.RunDaily(state);                             // and then with this
		CUnitUpkeep.RunDaily(state);
		CResources.UpdateHomeProduction(state);
		CStockpile.AddHomeProduction(state);
		CResources.GatherCutOff(state);                     // what the sea cuts off waits in a harbour
		CIndustrialCapacity.Update(state);
		CProduction.RunDaily(state);
		CLeadership.RunDaily(state);
		CConvoyDay.Maintain(state);                         // late in a country's day
		CProvinceRepair.RunDaily(state);                    // each province's own day, after every country's:
		CStockpile.HoldWithinBounds(state);                 // what was bombed comes back, and the pool is held
		// Events, after the provinces' day; none while a game is still being made (ours: whether the
		// original asks them in a new game's first days was not read).
		if (!state.IsBeingMade)
		{
			CEventPass.RunDaily(state);
		}
		CNavalIntelligence.Refresh(state);                  // the last thing the original's day does
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
	public static void RunFirstDays(CGameState state)
	{
		bool was = state.IsBeingMade;
		state.IsBeingMade = true;
		RunEntryPass(state);
		RunDailyPass(state);
		CSupplyStart.Fill(state);
		RunDailyPass(state);
		RunMonthlyPass(state);
		RunHourlyPass(state);
		state.IsBeingMade = was;
		// What the first screen shows is worked out for the game as it now runs.
		RunEntryPass(state);
	}

	/// <summary>
	/// What the original recomputes on entering a game, before any hour has passed, so
	/// that the first screen shows figures and not zeroes. It works the figures out and
	/// moves nothing: no stockpile changes here.
	/// </summary>
	public static void RunEntryPass(CGameState state)
	{
		CProvinceModifiers.Rebuild(state);
		CCountryModifiers.Rebuild(state);
		CLeadership.Update(state);
		CManpower.UpdateMost(state);
		CManpower.Update(state);
		CIndustrialCapacity.Update(state);
		CResources.UpdateHomeProduction(state);
		CProduction.Estimate(state);
		COfficers.Update(state);
		CNavalIntelligence.Refresh(state);
		// Entering a game makes every country's candidate events, as the month does.
		CEventPass.RebuildCandidates(state);
	}

	/// <summary>
	/// Order: each province's modifiers and the decay of its nationalism; each
	/// country's monthly work, which is where technology decays and its most manpower is
	/// worked out; the rebuild of every country's list of candidate events.
	/// </summary>
	private static void RunMonthlyPass(CGameState state)
	{
		// The month's pass a new game runs before its first hour decays nothing: the
		// original takes nothing off an ability until a game is under way.
		if (!state.IsBeingMade)
		{
			CResearch.DecayMonthly(state);
		}
		// The relations' drift is not held back: the month before the first hour is what makes
		// a new game's relations the same both ways (docs/mechanics/diplomacy.md, section 2).
		CRelations.RunMonth(state);
		CManpower.UpdateMost(state);
		CEventPass.RebuildCandidates(state);
	}

	/// <summary>Not established for the original.</summary>
	private static void RunYearlyPass(CGameState state)
	{
	}
}
