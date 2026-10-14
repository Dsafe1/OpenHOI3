using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Espionage;

/// <summary>
/// The espionage share's day for one country: the spies it trained go to its free spies, and whole spies are handed
/// out of them to its presences by priority band, 3, then 2, then 1, each band from where it left off. See
/// <c>docs/mechanics/espionage.md</c>, section 4.
/// </summary>
public static class CSpyPlacing
{
	public const string MostSpies = "MAX_NUMBER_OF_SPIES";

	/// <summary>The share of the most at which a presence drops a band, and what is added before it is made whole.</summary>
	private const double DropAt = 0.7;
	private const double DropSlack = 0.01;

	public static void Run(CGameState state, CCountry country, CFixedPoint trained)
	{
		country.FreeSpies += trained;

		long most = state.Defines["country", MostSpies].Raw / 1000;
		long dropAt = (long)(most * DropAt + DropSlack) * 1000;
		IReadOnlyList<CCountry> countries = state.Countries;

		// The bands are counted with no test that a country owns a province.
		var buckets = new int[4];
		foreach (CCountry other in countries)
		{
			CSpyPresence presence = CGameState.Presence(country, other);
			if (presence.Level < most * 1000)
			{
				buckets[Band(presence, dropAt)]++;
			}
		}

		CCountryFacts facts = CCountryFacts.Of(state);
		long pool = country.FreeSpies.Raw;
		for (int band = 3; band > 0; band--)
		{
			if (pool / 1000 < 1 || buckets[band] == 0)
			{
				continue;
			}
			long each = Math.Max(1, pool / 1000 / buckets[band]);
			bool placed = false;
			for (int i = country.SpyCursors[band]; i < countries.Count; i++)
			{
				CCountry other = countries[i];
				if (facts.OwnedCount(other.Tag) <= 0)
				{
					continue;
				}
				CSpyPresence presence = CGameState.Presence(country, other);
				if (Band(presence, dropAt) != band || presence.Level >= most * 1000)
				{
					continue;
				}
				if (pool / 1000 < 1)
				{
					break;
				}
				long give = Math.Min(most - presence.Level / 1000, each);
				if (give <= 0)
				{
					continue;
				}
				country.SpyCursors[band] = i + 1;
				placed = true;
				presence.AddLevel(give * 1000);
				// The whole of it, even where the ten-spy cap cut the level.
				pool -= give * 1000;
			}
			if (!placed || country.SpyCursors[band] >= countries.Count)
			{
				country.SpyCursors[band] = 0;
			}
		}
		country.FreeSpies = new CFixedPoint(pool);
	}

	/// <summary>The presence's priority, one lower from 70% of the most spies, held to 0..3.</summary>
	public static int Band(CSpyPresence presence, long dropAt)
	{
		int band = presence.Priority - (presence.Level >= dropAt ? 1 : 0);
		return Math.Clamp(band, 0, 3);
	}
}
