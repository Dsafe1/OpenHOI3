using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Espionage;

/// <summary>
/// The spies a new game gives one country in another, worked out in the loop that gives the relations nobody gave, from
/// the relation the install's Lua answered and what the two are to each other. See <c>docs/mechanics/espionage.md</c>,
/// section 2.
/// </summary>
public static class CSpyStart
{
	/// <summary>
	/// <paramref name="country"/>'s level in <paramref name="other"/>, both owning a province and neither REB, with
	/// <paramref name="relation"/> the whole number the Lua answered for the pair.
	/// </summary>
	public static void Give(CGameState state, CCountry country, CCountry other, int relation)
	{
		long score = Score(state, country, other, relation);
		CGameState.Presence(country, other).Level = System.Math.Clamp(score * 1000, 0, CSpyPresence.MostLevel);
	}

	/// <summary>The score in whole spies, halved toward nought for a country that is not a major.</summary>
	public static long Score(CGameState state, CCountry country, CCountry other, int relation)
	{
		long score = 0;
		if (relation < -100)
		{
			score++;
		}
		if (relation < 0)
		{
			score++;
		}
		bool self = other == country;
		if (CCountryFacts.Of(state).AreNeighbours(country.Tag, other.Tag))
		{
			score++;
		}
		if (state.Province(country.ActingCapital).Continent is { Length: > 0 } continent
			&& continent == state.Province(other.ActingCapital).Continent)
		{
			score++;
		}
		if (COfferRules.RealFaction(country) && COfferRules.RealFaction(other) && country.Faction != other.Faction)
		{
			score += 2;
		}
		if (country.RulingIdeology != other.RulingIdeology)
		{
			score++;
		}
		if (CCountryLists.AtWar(state, country.Tag, other.Tag))
		{
			score += 2;
		}
		if (CCountryLists.HasAlliance(state, country.Tag, other.Tag))
		{
			score -= 2;
		}
		if (self)
		{
			score += 3;
		}
		return COfferRules.IsMajor(country) ? score : score / 2;
	}
}
