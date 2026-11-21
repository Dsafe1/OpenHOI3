using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// When a country's strategy is made, and its making: from nothing, in the original's order. See
/// <c>docs/mechanics/aistrategy.md</c>, sections 1 and 2.
/// </summary>
public static class CAiStrategies
{
	/// <summary>How many whole days a running game leaves a strategy before making it again.</summary>
	public const int Days = 10;

	/// <summary>Every country's, in their order, as a new game is made - before its weather and its two days.</summary>
	public static void MakeAtStart(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			Make(state, country);
		}
	}

	/// <summary>
	/// Before the countries' days: in a running game each strategy ten whole days old or more made again, and in any game
	/// one never made.
	/// </summary>
	public static void RunDaily(CGameState state)
	{
		long today = state.TotalHours / CGameState.HoursPerDay;
		foreach (CCountry country in state.Countries)
		{
			if (state.IsRunning && today - country.Strategy.MadeHour / CGameState.HoursPerDay >= Days)
			{
				Make(state, country);
				// and its theatres are made again in its next hour (theatres.md, section 1)
				country.Strategy.TheatresDirty = true;
			}
			if (!country.Strategy.IsMade)
			{
				Make(state, country);
			}
		}
	}

	/// <summary>
	/// <see cref="RunDaily"/> for a game that plays the AI's steps back from a recording - a replay, a machine following a
	/// host: the same strategies made again on the same days, their lists alone. The two things of a making that the game's
	/// hour reads, the theatres' mark and the prepared wars, come with the played-back step and are left as it set them
	/// (<c>CAiMarks</c>). So a look that asks a strategy - an offer's chance in a dialog - reads the host's lists
	/// (<c>docs/multiplayer.md</c>, section 12).
	/// </summary>
	public static void RunDailyFollowing(CGameState state)
	{
		long today = state.TotalHours / CGameState.HoursPerDay;
		foreach (CCountry country in state.Countries)
		{
			if ((state.IsRunning && today - country.Strategy.MadeHour / CGameState.HoursPerDay >= Days) || !country.Strategy.IsMade)
			{
				Make(state, country, listsOnly: true);
			}
		}
	}

	/// <summary>Made again at once - a change of war or a peace - in a running game only.</summary>
	public static void Remake(CGameState state, CCountry country)
	{
		if (state.IsRunning && !state.IsBeingMade)
		{
			Make(state, country);
			Theatres.CTheatres.RebuildAtOnce(state, country);
		}
	}

	/// <summary>
	/// Made again at once where a change of government is the kind the original keeps in the history - the
	/// <c>government</c> effect, a coup by a country, a puppet made - with no test of the ten days or of a game running,
	/// and the theatres left as they are (<c>docs/mechanics/aistrategy.md</c>, section 4).
	/// </summary>
	public static void AfterGovernmentChanged(CGameState state, CCountry country)
	{
		Make(state, country);
	}

	/// <summary>A faction's new member's strategy made again, then each of its neighbours', in a running game only.</summary>
	public static void AfterFactionJoined(CGameState state, CCountry country)
	{
		if (!state.IsRunning || state.IsBeingMade)
		{
			return;
		}
		Make(state, country);
		Theatres.CTheatres.RebuildAtOnce(state, country);
		foreach (CCountry neighbour in CCountryLists.Neighbours(state, country))
		{
			Make(state, neighbour);
			Theatres.CTheatres.RebuildAtOnce(state, neighbour);
		}
	}

	/// <summary>
	/// The country's strategy made from nothing: its personality, the victory conditions walked for a country nobody plays,
	/// the lists in order each scaled after its making, and its prepared wars checked. Nothing for the rebels, a country
	/// owning no province, or one whose acting capital is in no area of the supply network. Answers whether it was made.
	/// </summary>
	public static bool Make(CGameState state, CCountry country, bool listsOnly = false)
	{
		if (country.Tag.Value == "REB" || COwnedCounts.Of(state).Count(country.Tag) <= 0
			|| CSupplyNetworks.Refresh(state).AreaOf(country.ActingCapital) is null)
		{
			return false;
		}
		CAiStrategy strategy = country.Strategy;
		strategy.Clear();
		strategy.MadeHour = state.TotalHours;
		strategy.Personality = CStrategyPersonality.Choose(state, country);
		// A played country's walk runs while its production is handed to the AI - the original's flag, odd as it is
		// (frontend.md, section 4).
		if (!state.Played.Contains(country.Tag) || state.Automation.Has(Settings.EAutomation.Production))
		{
			CStrategyWalk.Walk(state, country, strategy);
		}
		CStrategyProvinces.Conquer(state, country, strategy);
		CStrategyProvinces.Defend(state, country, strategy);
		CStrategyThreats.Make(state, country, strategy);
		strategy.Threat.ScaleTo400();
		CStrategyAntagonism.Make(state, country, strategy);
		strategy.Antagonize.ScaleTo400();
		strategy.Rival.ScaleTo400();
		CStrategyFriends.Make(state, country, strategy);
		strategy.Befriend.ScaleTo400();
		strategy.Vassal.ScaleTo400();
		CStrategyProtection.Make(state, country, strategy);
		strategy.Protect.ScaleTo400();
		if (!listsOnly)
		{
			CWarTargets.Recheck(state, country);
		}
		strategy.IsMade = true;
		return true;
	}
}
