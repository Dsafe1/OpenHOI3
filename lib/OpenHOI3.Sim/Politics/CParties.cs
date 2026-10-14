using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Politics;

/// <summary>
/// The parties' monthly drift: each party's popularity moved a share of the way to its organisation, the
/// ruling party's support added, a draw, and a penalty for a party with ministers in two posts or more; then
/// both figures normalised to a hundred and the seats allotted. See <c>docs/mechanics/politics.md</c>, section 3.
/// </summary>
public static class CParties
{
	public const string RulingPartySupport = "ruling_party_support";

	/// <summary>Every country's drift, existing or not, in their order.</summary>
	public static void RunMonthly(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			Drift(state, country);
		}
	}

	public static void Drift(CGameState state, CCountry country)
	{
		CPartyFigures popularity = country.Popularity;
		CPartyFigures organisation = country.Organisation;
		if (popularity.Count == 0)
		{
			return;
		}
		long rate = state.Defines[CCabinet.Section, "PARTY_POP_CHANGE_RATE"].Raw;
		long randomness = state.Defines[CCabinet.Section, "POPULARITIY_RANDOMNESS"].Raw;
		long penalty = state.Defines[CCabinet.Section, "ELECTION_MINISTER_DISSENT_PENALTY"].Raw;
		int ruling = state.Ideology(country.RulingIdeology)?.Index ?? -1;

		for (int i = 0; i < popularity.Count; i++)
		{
			long drift = rate != 0 ? (organisation[i] - popularity[i]) * 1000 / rate : 0;
			long support = i == ruling ? country.Modifier[RulingPartySupport].Raw : 0;
			long random = (state.Random.Next() % 1000) * randomness / 1000 - randomness * 1000 / 2000;
			int posts = PostsHeld(state, country, i);
			long minister = posts >= 2 ? country.Dissent.Raw * posts * penalty / 1000 : 0;
			long change = drift + support + random + minister;
			popularity.LastChange[i] = change;
			long next = popularity[i] + change;
			// A figure that would fall below nought is left as it was.
			if (next >= 0)
			{
				popularity.Values[i] = next;
				popularity.Tallies[i, CPartyFigures.DriftSlot] += next;
			}
		}

		popularity.NormaliseToHundred();
		organisation.NormaliseToHundred();
		CCabinet.Allot(state, country);
		organisation.ClearTallies();
		popularity.ClearTallies();
	}

	/// <summary>The cabinet posts held by ministers of a party, a man in two posts counted twice.</summary>
	public static int PostsHeld(CGameState state, CCountry country, int ideology)
	{
		int posts = 0;
		foreach (CMinister minister in country.Government.Values)
		{
			if (state.Ideology(minister.Ideology)?.Index == ideology)
			{
				posts++;
			}
		}
		return posts;
	}
}
