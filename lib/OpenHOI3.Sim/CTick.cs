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
	}

	/// <summary>
	/// Order: supply; each country's neighbours; each country's daily work, which
	/// includes its IC, its units and its politics; each province's daily work; events;
	/// intelligence.
	/// </summary>
	private static void RunDailyPass(CGameState state)
	{
		// A country's day, in the original's order: its manpower grows, its leadership is
		// worked out, what its provinces produced reaches the stockpile, its IC is worked
		// out, and then its six shares of IC are run and its four of leadership after them.
		CProvinceModifiers.Rebuild(state);
		CStrategicResources.Update(state);                  // the first thing the original's day does
		CCountryModifiers.Rebuild(state);
		CManpower.RunDaily(state);
		CLeadership.Update(state);
		CResources.UpdateHomeProduction(state);
		CStockpile.AddHomeProduction(state);
		CIndustrialCapacity.Update(state);
		CProduction.RunDaily(state);
		CLeadership.RunDaily(state);
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
	}

	/// <summary>
	/// Order: each province's modifiers and the decay of its nationalism; each
	/// country's monthly work, which is where technology decays and its most manpower is
	/// worked out; the rebuild of every country's list of candidate events.
	/// </summary>
	private static void RunMonthlyPass(CGameState state)
	{
		CResearch.DecayMonthly(state);
		CManpower.UpdateMost(state);
	}

	/// <summary>Not established for the original.</summary>
	private static void RunYearlyPass(CGameState state)
	{
	}
}
