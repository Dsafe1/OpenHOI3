namespace OpenHOI3.Sim.Modifiers;

/// <summary>
/// A modifier an event gave a country or a province, and the hour it lasts to. An hour in year 1
/// is never reached: that is how the original keeps a modifier given for a negative time. See
/// <c>docs/mechanics/effects.md</c>, section 6.
/// </summary>
public sealed record CTimedModifier(CModifier Modifier, long UntilHour)
{
	/// <summary>A modifier given now for some days; for ever where the days are below nought.</summary>
	public static CTimedModifier Given(CGameState state, CModifier modifier, int days)
	{
		if (days >= 0)
		{
			return new CTimedModifier(modifier, state.TotalHours + days * (long)CGameState.HoursPerDay);
		}
		CDate today = state.Date;
		long yearOne = new CDate(1, today.Month, today.Day).DayNumber * (long)CGameState.HoursPerDay;
		return new CTimedModifier(modifier, yearOne + state.Hour);
	}

	/// <summary>Whether its time has passed. One that lasts to year 1 never has.</summary>
	public bool HasEnded(CGameState state)
	{
		return UntilHour < state.TotalHours && CDate.FromDayNumber((int)(UntilHour / CGameState.HoursPerDay)).Year > 1;
	}
}
