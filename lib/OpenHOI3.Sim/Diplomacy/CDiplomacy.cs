using System;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Diplomacy;

/// <summary>
/// What entering a new game does to what countries hold of each other, after the history and before
/// the two days it runs: every country's effective neutrality, the relations nobody gave, and the
/// threat a country's history gives it, spread over the map from its capital. See
/// <c>docs/mechanics/diplomacy.md</c>, sections 2 and 5.3.
/// </summary>
public static class CDiplomacy
{
	public static void SetUpNewGame(CGameState state, Action<string> report)
	{
		foreach (CCountry country in state.Countries)
		{
			CNeutrality.UpdateEffective(state, country);
		}
		CRelations.SetAtStart(state, report);
	}

	/// <summary>
	/// The threat the country's history gives it, spread over the map from its acting capital at the end of its own turn
	/// of the original's set-up: so a country whose turn comes later resists none of it - its modifier not made yet, unless
	/// the history's wars mobilised it and made it then - and one whose turn comes later reads its effective neutrality with
	/// it (section 5.3; measured, the 1939 and 1941 bookmarks' start saves).
	/// </summary>
	public static void SpreadHistoryThreat(CGameState state, CCountry country)
	{
		if (country.HistoryThreat != CFixedPoint.Zero)
		{
			CThreat.SpreadOverMap(state, country, country.HistoryThreat.Raw, CThreat.Adds, state.Province(country.ActingCapital),
				holder => holder.Index <= country.Index || state.MobilisedByHistory.Contains(holder.Tag));
		}
	}
}
