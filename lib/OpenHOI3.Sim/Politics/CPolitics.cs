using System;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Politics;

/// <summary>
/// A new game's politics, once the countries have their history and their orders of battle: each party's two
/// figures from the history, raw; the pool and its empty posts filled; a country with no ruling party given its
/// head of state's; the election dates; and the seats. See <c>docs/mechanics/politics.md</c>, section 2.
/// </summary>
public static class CPolitics
{
	public static void SetUpAtStart(CGameState state, Action<string>? warn = null)
	{
		int count = state.Ideologies.Count;
		int unknown = 0;
		foreach (CCountry country in state.Countries)
		{
			country.Popularity = new CPartyFigures(count);
			country.Organisation = new CPartyFigures(count);
			country.Cabinet = new int[count];
			unknown += FromHistory(state, country.HistoryPopularity, country.Popularity);
			unknown += FromHistory(state, country.HistoryOrganisation, country.Organisation);
			// Measured, not read: the start save has an existing country's figures normalised twice and one that owns
			// nothing once - one drift's and this (politics.md, section 2).
			if (Countries.CCountryLists.OwnedCount(state, country.Tag) > 0)
			{
				country.Popularity.NormaliseToHundred();
				country.Organisation.NormaliseToHundred();
			}

			CMinisterPosts.SetUpPool(state, country);
			CMinisterPosts.Fill(state, country, refillAll: false, electedOnly: false);
			if (state.Ideology(country.RulingIdeology) is null
				&& state.GovernmentPositions.Count > 0
				&& country.Government.TryGetValue(state.GovernmentPositions[0].Name, out CMinister? head))
			{
				country.RulingIdeology = head.Ideology;
			}
			CElections.SetUpAtStart(state, country);
			CCabinet.Allot(state, country);
		}
		if (unknown > 0)
		{
			warn?.Invoke($"country histories give {unknown} party figures for ideologies that are not defined");
		}
	}

	/// <summary>The history's figures by number, raw and unnormalised; answers how many named no ideology.</summary>
	private static int FromHistory(CGameState state, System.Collections.Generic.Dictionary<string, CFixedPoint> history,
		CPartyFigures figures)
	{
		int unknown = 0;
		foreach ((string name, CFixedPoint figure) in history)
		{
			if (state.Ideology(name) is { } ideology)
			{
				figures.Set(ideology.Index, figure.Raw, CPartyFigures.HistorySlot);
			}
			else
			{
				unknown++;
			}
		}
		return unknown;
	}
}
